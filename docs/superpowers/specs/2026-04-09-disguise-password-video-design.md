# FileVault — Vault Disguise, Password Generator UX, and Video Playback

**Date:** 2026-04-09
**Status:** Approved design, not yet implemented
**Scope:** Three additive features for the existing FileVault Windows app.

## Goals

1. **Vault disguise.** A vault file can optionally be disguised as a real, openable JPEG image. Double-clicking it in Explorer opens the chosen picture in the default image viewer; the FileVault app still recognizes and opens it as a vault.
2. **Collapsible advanced password options.** The create-vault and change-password dialogs hide the password generator's advanced controls behind an "Advanced options ▸" toggle, keeping the default dialog small.
3. **Video playback.** Videos stored in a vault can be played inside the app with the same fullscreen overlay design used for images. Decryption is streaming and never touches disk.

## Non-goals

- PNG / WebP / other disguise formats (JPEG only for v1).
- Disguise as non-image file types (PDF, video, etc.).
- Re-encoding or transcoding video.
- Editing the cover image inside the FileVault app.
- Audio-only file playback.
- Migration tooling for old single-blob files into chunked storage (old files keep working as-is).

## Architecture overview

Three independent features sharing the existing Service ↔ UI named-pipe pattern:

- **Vault disguise** lives in the Service container-I/O layer. The UI just picks an image file and passes its bytes during create / edit-settings. Encryption/key/header layout is unchanged.
- **Collapsible advanced password options** is a pure UI change in `CreateVaultDialog` and `ChangePasswordDialog`. No new view models, no new IPC.
- **Video playback** adds a new viewer in the UI plus a chunked-read IPC path so LibVLC can pull bytes on demand without buffering the whole file. To make range reads efficient, the vault file format is extended (additively) to store large files as multiple independently encrypted chunks.

---

## 1. Vault disguise

### File layout

A disguised vault file is structured as:

```
[ JPEG image bytes ............................ FFD9 (EOI marker) ]
[ FVLT magic ][ vault payload exactly as today ]
```

An undisguised vault is unchanged: it begins with `FVLT`.

### Detection on open

When the Service opens a vault file:

1. Read the first 4 bytes.
2. If they are `46 56 4C 54` ("FVLT"), the vault is undisguised. Base offset = 0. Done.
3. Otherwise: read the **last 16 bytes** of the file. The disguise format reserves a fixed-size trailer:
   ```
   [ ... vault payload ... ][ "FVDT" magic : 4 ][ base_offset : 8 (LE int64) ][ "FVDT" magic : 4 ]
   ```
   "FVDT" = FileVault Disguise Trailer. Two copies of the magic word bracket the offset for robustness against accidental matches.
4. If both trailer magics are present, read `base_offset` and use it directly. Validate by reading 4 bytes at `base_offset` and confirming they equal `FVLT`. If not, fail with corruption error.
5. If trailer magics are absent, the file is not a recognized vault — return an error.

The trailer makes detection **O(1)** instead of scanning the whole JPEG, and removes the false-positive risk that a JPEG could contain `FFD9 FVLT` in its EXIF or thumbnail bytes. The trailer is appended *after* the vault payload and is harmless to image viewers (which stop at the JPEG `FFD9` marker near the file's beginning).

### `VaultStream` — transparent prefix

A new `VaultStream` class wraps a `FileStream` and holds a `BaseOffset`. It overrides `Position`, `Seek`, and `Length` to subtract / add the base offset. All reads and writes pass through unchanged. With this wrapper in place, every existing component (`ContainerHeader`, `HeaderBlock`, `VaultContainerIo` chunk read/write, `VfsIndex`) continues to work without changes — they see "offset 0" as the start of `FVLT`.

### `VaultPrefix` — helper

A new static class with two methods:

- `DetectBaseOffset(Stream stream) → long` — implements the trailer-based detection above. Returns 0 for undisguised files.
- `WriteDisguisedFile(string targetPath, byte[]? coverImageBytes, Stream vaultPayload)` — writes the cover bytes (if any), then the vault payload, then the 16-byte FVDT trailer with `base_offset = coverImageBytes.Length`. Writes to `targetPath.tmp`, then atomically swaps:
  - If `targetPath` does not exist: `File.Move(tmp, target)`.
  - If `targetPath` exists: `File.Replace(tmp, target, destinationBackupFileName: null)`.

### Cover image storage

The cover image **bytes** (not a path) are stored inside the encrypted `HeaderBlock`. This means:

- The disguise survives moving/deleting the original image source.
- "Edit vault settings → re-disguise" doesn't require the user to reselect the cover.
- A SHA-256 of the cover bytes is also stored in the header so we can detect external tampering on open and warn the user.

`HeaderBlock` gains two new fields:
- `CoverImageBytes: byte[]` — empty if undisguised.
- `CoverImageHash: byte[]` — SHA-256, 32 bytes; empty if undisguised.

Cover images are typically tens to a few hundred KB. The header is decrypted exactly once on unlock, so the size cost is one-time and small.

### Edit settings flow

Changing the cover image rewrites the prefix:

1. UI picks new image (or "remove cover").
2. Service computes new cover bytes + hash, updates the in-memory `HeaderBlock`, and re-encrypts it.
3. Service writes a new file: `targetPath.tmp = newCoverBytes + currentVaultPayload` (the vault payload is copied through; the encrypted header inside it is rewritten with the new cover bytes/hash).
4. `File.Replace(targetPath.tmp, targetPath, backup: null)` swaps atomically.

Yes, this rewrites the entire file. It only happens on disguise change, not on every save, and the user explicitly asked for it.

### Extension

- Disguised: `.jpg`
- Undisguised: `.vault`

The dialog tells the user the extension will change. The Service tolerates either extension on open (it relies on the byte signature, not the extension).

### Cover image validation

Before accepting a user-selected cover image, the dialog validates:

1. **File reads successfully.** Inline error otherwise.
2. **First two bytes are `FF D8`** (JPEG SOI marker). Otherwise: *"Cover image must be a JPEG file."*
3. **Last two bytes are `FF D9`** (JPEG EOI marker). Otherwise: *"Cover image is malformed or has trailing data — please re-save it from an image editor and try again."*
4. **Total size ≤ 8 MB.** Otherwise: *"Cover image is too large (max 8 MB)."*

These checks are done in the UI before sending to the Service. The Service repeats checks 2 and 4 as a defense-in-depth measure.

### Tampering warning

The cover hash check happens **after** the encrypted header has been successfully decrypted (the hash lives inside the encrypted header):

1. Detect base offset via trailer.
2. Decrypt header block at base offset.
3. Compute `SHA256` over the file's leading bytes `[0, base_offset)` — i.e., the cover image bytes.
4. Compare to `storedCoverImageHash`. If different, surface a non-blocking warning post-unlock: *"This vault's cover image has been modified outside FileVault. The vault data is intact, but you may want to re-save the cover."*

If a tool also stripped or rewrote the trailer/payload, the unlock fails earlier at step 4 of detection or at header decrypt.

### Sharp edge: external image editing

Any tool that "optimizes" or "re-saves" the JPEG (some image viewers do this on rotate; some cloud sync clients do it for thumbnails) will strip or rewrite the bytes after `FFD9`, destroying the appended vault payload. The create/edit dialogs surface this prominently:

> **Don't edit the cover image with other apps.** Doing so will corrupt the vault.

### IPC changes

- `CreateVaultRequest` gains `CoverImageBytes: byte[]` (optional, may be empty).
- New message type: `UpdateVaultSettingsRequest { VaultPath, CoverImageBytes }` and `UpdateVaultSettingsResponse`.

### UI changes

- `CreateVaultDialog` gains a "Cover image (optional)…" file picker row, plus the warning text above.
- A new `EditVaultSettingsDialog` is reachable from the existing per-vault right-click menu's "Edit settings" item (currently a stub). Lets the user change the cover image or remove it.

---

## 2. Collapsible advanced password options

Pure UI change in `CreateVaultDialog.xaml` and `ChangePasswordDialog.xaml`. Layout becomes:

```
[ Password: ____________ ] [ Generate ]
[ Confirm:  ____________ ]
[ ▸ Advanced options ]               ← collapsed by default
```

When expanded:

```
[ ▾ Advanced options ]
  ( ) Random characters     (•) Memorable passphrase
  Length: [====|====] 20
  [✓] Uppercase  [✓] Lowercase  [✓] Digits  [✓] Symbols
  [ Generate ] [ Use this ]
```

### Mechanics

- The `▸ / ▾` toggle is a styled `ToggleButton`.
- The advanced panel below is a `StackPanel` with `Visibility` bound to the toggle's `IsChecked` via `BooleanToVisibilityConverter`.
- The "Generate" button at the top (next to the password field) generates with the *current* advanced settings — even when collapsed — using sensible defaults: **24 characters, all character classes enabled** (matches the Random Characters default in the original FileVault spec). First-click without expanding gives the user a usable password instantly.
- All generator logic already exists in `PasswordDialogViewModel` from a prior round of work. This is purely a layout / binding change. No new view models, no new IPC.
- The same collapsible block is added to `ChangePasswordDialog` for consistency.

---

## 3. Video playback

### Dependencies

- `LibVLCSharp` (managed wrapper) added to `FileVault.UI.csproj`.
- `VideoLAN.LibVLC.Windows` (native binaries) added to `FileVault.UI.csproj`. Self-contained — no separate VLC install required. Adds ~40 MB to the install footprint; acceptable trade-off for preserving the in-memory-only guarantee.

### Streaming model

LibVLC supports custom media via callbacks (`open`, `read`, `seek`, `close`). We implement a `VaultMediaInput : MediaInput` whose callbacks pull bytes from the vault on demand:

- **`open`** — record total plaintext length (from `FileItemModel.PlaintextLength`), return success.
- **`read(buffer, len)`** — return bytes starting at `position`, advance `position`. Implementation details below.
- **`seek(offset)`** — set internal `position = offset`. Invalidate the read-ahead cache if the seek crosses a chunk boundary.
- **`close`** — drop the read-ahead buffer and release the client reference.

#### Threading and sync-over-async

LibVLC invokes media callbacks **synchronously from native worker threads**, not from the UI thread. The `read` callback must block until bytes are available. We bridge to the async `IServiceClient` carefully:

1. `VaultMediaInput.Read` calls `_client.ReadFileRangeAsync(...).ConfigureAwait(false).GetAwaiter().GetResult()`.
2. The `ConfigureAwait(false)` ensures continuations do **not** post back to the WPF `SynchronizationContext`, so there is no UI-thread deadlock.
3. The named-pipe `ServiceClient` is single-request-at-a-time per connection. `VaultMediaInput` opens **its own dedicated `ServiceClient` connection** for the lifetime of one video, separate from the main UI connection. This:
   - prevents video reads from blocking unrelated UI requests,
   - guarantees that LibVLC's serialized read callbacks map onto a single pipe with no contention,
   - and lets the connection be torn down cleanly on `close` without affecting the main UI client.
4. LibVLC by default issues only one outstanding `read` per media at a time, which matches our single-request pipe.

#### Read-ahead cache

A naive implementation that calls `ReadFileRangeAsync` per `read` would round-trip to the service for every 32–64 KB LibVLC asks for, with each round-trip decrypting a full 1 MB chunk and discarding most of it. Instead, `VaultMediaInput` keeps a small in-process cache:

```
cache:    byte[] | null     // decrypted chunk-aligned slab
cacheStart: long            // logical file offset of cache[0]
cacheLen:   int
```

On each `read(buffer, len)`:
1. If `[position, position+len)` is fully inside `[cacheStart, cacheStart+cacheLen)`, copy from cache. Done.
2. Otherwise, request a chunk-aligned 1 MB slab starting at `floor(position / 1MB) * 1MB` via `ReadFileRangeAsync`. Replace the cache. Copy from cache.
3. On `seek` that lands outside the current cache, do not pre-fetch — wait for the next `read`.

The cache is exactly one decrypted chunk (~1 MB) at a time. Sequential playback turns into one IPC round-trip per chunk, not per LibVLC read.

**Legacy single-blob files:** A pre-existing large file with no `Chunks` list is read as one giant chunk. Streaming such a file via `VaultMediaInput` will decrypt the whole blob on every cache miss (effectively, every seek that crosses the slab boundary). This is acceptable because legacy files are pre-existing and rare; users who want efficient video playback should re-import the file so it picks up chunked storage.

#### Range request size cap

`IServiceClient.ReadFileRangeAsync` caps the requested length at **2 MB** server-side. Anything larger is clamped. The client should request ≤ 1 MB in normal operation; the cap exists to bound buffer allocations and IPC payload sizes against bugs or malicious clients.

#### Plaintext lives in the UI process — acknowledged trade-off

The original FileVault security model requires that **only the Service decrypts** and that the UI is a thin client. Video streaming requires plaintext bytes (decoded video frames, and raw decrypted chunks) to live in the **UI process** so LibVLC can decode them. This is a real architectural softening:

- The UI process now holds decrypted vault content in memory while a video is playing.
- LibVLC allocates its own decode buffers, which we do not control and cannot pin against paging.
- On Windows with the system page file enabled, those buffers may be swapped to disk.

**Mitigations applied:**
- The read-ahead cache is zeroed (`Array.Clear`) on every replace and on `close`.
- The `MediaPlayer` and `LibVLC` instances are disposed promptly on overlay close, vault lock, and app exit.
- The UI never writes decrypted bytes to a file path; the streaming path is in-memory only from the UI's perspective.

**Residual risk:** LibVLC's internal buffers may be page-swapped. Users who require absolute "no plaintext on disk, ever" should avoid the video feature on systems with a page file. This is documented in the user-facing notes for the feature.

#### Concurrency with vault writes

A vault may have an active video stream while the user attempts to import, delete, rename, or move other files. The Service serializes per-vault access via a `ReaderWriterLockSlim` (added in this change):

- `ReadFileRangeAsync` and `ReadFileAsync` acquire **read locks**.
- `Import`, `Delete`, `Rename`, `Move`, `CreateFolder`, and `UpdateVaultSettings` acquire **write locks**.
- A write request waits for in-flight reads to complete; new reads wait for in-flight writes to complete.
- Streamed video reads do **not** hold the read lock for the lifetime of the stream — they acquire and release it per `ReadFileRangeAsync` call. This means a write op can interleave between two video chunk fetches; that is intentional, because long-held read locks would block all UI operations during playback.
- If a write op truncates or invalidates the file currently being streamed (e.g., the user deletes the file being played), the next `ReadFileRangeAsync` call returns an error, `VaultMediaInput.Read` returns 0 (EOF) to LibVLC, and the player stops gracefully. The viewer's error handler closes the overlay.

### New IPC: `ReadFileRangeRequest` / `Response`

```
ReadFileRangeRequest  { VaultPath, VaultNodePath, Offset: long, Length: int }
ReadFileRangeResponse { Bytes: byte[] }
```

Service-side handler:
1. Looks up the file node in the in-memory index for the open vault.
2. Clamps `Length` to `min(Length, 2_097_152)` (2 MB hard cap).
3. Clamps `[Offset, Offset+Length)` to `[0, file.PlaintextLength)`. If `Offset >= file.PlaintextLength`, returns an empty array (EOF).
4. Identifies which chunks overlap the (clamped) range using the chunk index.
5. Decrypts only those chunks (verifying GCM tag with `AAD = FileId || ChunkIndex`).
6. Slices out the requested bytes and returns them.

Acquires the per-vault read lock for the duration of the call (see "Concurrency with vault writes" above).

### Chunked file storage (vault format change)

To make range reads efficient, large files are split into independently encrypted chunks at import time. Without this, every range read would have to decrypt the entire file, defeating the point of streaming.

#### Schema change

`FileNode` in the VFS index gains:

```
FileId        : Guid             // stable per-file identifier (new field, generated at import)
Chunks        : List<ChunkRef>

ChunkRef = {
  ContainerOffset    : long      // byte offset within the FVLT-relative stream where this chunk's [nonce|ciphertext|tag] begins
  CiphertextLength   : int       // bytes of ciphertext (== PlaintextLength for AES-GCM)
  PlaintextLength    : int       // useful plaintext bytes in this chunk
}
```

The chunk's *logical* start within the file (used by range reads) is **not** stored — it is computed at load time as the cumulative sum of preceding `PlaintextLength`s. This avoids storing redundant data and prevents inconsistency.

The existing single `Offset` / `PlaintextLength` fields on `FileNode` stay for backward compatibility. On read, a node with no `Chunks` list (or an empty list) is treated as a synthetic one-chunk list pointing at the legacy fields, with a deterministic synthetic `FileId` derived from the legacy offset (the legacy file is read-only-ish: range reads still decrypt the whole blob). **No migration needed**; old vaults keep working.

#### On-disk chunk layout

Each chunk on disk is exactly:

```
[ nonce : 12 ][ ciphertext : N ][ gcm_tag : 16 ]
```

Total: `N + 28` bytes. There is no separate length prefix — `CiphertextLength` in the index is authoritative.

#### Nonce scheme and AAD binding

- **Nonce:** 12 random bytes from a CSPRNG, generated fresh per chunk per write. Random nonces are safe for AES-GCM up to ~2⁳² messages per key, which is far beyond any plausible vault size. The vault key never changes for the lifetime of the container, so a single nonce space applies.
- **AAD:** every chunk encryption binds the following Associated Data:
  ```
  AAD = FileId (16 bytes) || ChunkIndex (4 bytes, LE u32)
  ```
  This prevents an attacker (who can read but not decrypt) from reordering chunks within a file, swapping chunks between files, or replaying an old chunk into a new position. Decrypting a chunk with the wrong `(FileId, ChunkIndex)` fails the GCM tag check.
- The header block and VFS index continue to use their existing AAD-less encryption (unchanged from the current design).

#### Chunk size and threshold

- **Chunk size: 1 MB plaintext.**
- **Threshold:** files ≤ **1 MB** stay single-chunk; files larger than 1 MB are split into 1 MB chunks (final chunk may be short). Using one threshold equal to the chunk size avoids the "4.01 MB takes 5 decrypts" cliff and keeps the policy uniform.

#### Import atomicity

Chunked imports of multi-GB videos can leave many MB of orphan ciphertext if the process crashes between `AppendFileChunk` calls and the final index rewrite. Today's vault has the same problem at smaller scale; chunked imports make it visible.

For v1: documented behavior, not engineered around.
- The import operation writes all chunks, then rewrites the index in one operation.
- If a crash happens before the index rewrite, the new chunks are unreferenced ciphertext at the end of the container — they cannot be decrypted (the index has no `ChunkRef`s pointing at them) and they consume disk space until a future "compact" operation reclaims them.
- A future "compact vault" command (out of scope for this spec) will scan the index, identify reachable chunks, and rewrite the container without orphans.

#### Imports

`ImportFileOperation` reads the source file in 1 MB blocks, calls `AppendFileChunk` per block, accumulates `ChunkRef`s, and creates the `FileNode` with the full chunk list. Index rewritten once at the end.

#### Reads

- `ReadFileAsync` (whole file) iterates chunks and concatenates — semantically identical to today, just a loop.
- `ReadFileRangeAsync(offset, length)` (new):
  1. Find chunks where `[chunk.start, chunk.start + chunk.PlaintextLength)` overlaps `[offset, offset + length)`.
  2. Decrypt only those chunks.
  3. Slice out the requested range from the concatenated plaintext.
  4. Return the slice.

### Video viewer UI

#### `VideoViewer.xaml` — new view

Sibling to `FullscreenViewer`, in `Views/`. Layout:

- Black background.
- Center: `LibVLCSharp.WPF.VideoView` host bound to a `MediaPlayer`.
- Bottom toolbar: play/pause, seek slider (`Slider` bound to current/total time), current/total time labels, volume slider, fullscreen toggle.
- Keyboard: `Space` play/pause, `←/→` seek ±5s, `F` fullscreen, `Esc` close (handled by parent).

#### `VideoViewerViewModel`

- Owns one `LibVLC` and one `MediaPlayer` instance per overlay open. Disposed on close.
- `OpenAsync(IServiceClient, vaultPath, FileItemModel)` — creates a `Media` from a `VaultMediaInput` whose callbacks call `client.ReadFileRangeAsync`. Plays.
- Exposes `IsPlaying`, `Position`, `Duration`, `Volume` as observable properties bound to the toolbar.

### `MediaViewerOverlay` — unified navigator

The current `FullscreenViewer` does two jobs (display image + handle prev/next). It is split:

- **`FullscreenViewer`** keeps doing the image display and zoom controls only.
- **`MediaViewerOverlay`** (new) owns:
  - The current item index in a *unified* `playable` list (`items.Where(i => !i.IsDirectory && (i.IsImage || i.IsVideo))`).
  - The constant header (filename + close button).
  - The constant prev/next arrows (always visible regardless of inner viewer).
  - A reference to the `IServiceClient` and the active vault path (lifetime of the overlay).
  - The inner content control. On navigate, it disposes the current inner viewer (releasing LibVLC handles or clearing the image), then constructs a **fresh** instance of the appropriate inner viewer and calls `OpenAsync` on it.

**State transfer:** Inner viewers are not pooled and do not preserve state across navigation. A fresh `FullscreenViewer` always starts at "fit-or-1:1" zoom; a fresh `VideoViewer` always starts paused at position 0. This is the simplest model and matches user expectations from typical gallery viewers.

Each inner viewer keeps its own type-specific toolbar (image: zoom controls; video: play/seek/volume).

### File-type routing

`FileItemModel` already has `IsImage`. Add `IsVideo` (extension-sniffed: `.mp4 .mkv .webm .mov .avi .m4v`).

`OnFileOpened` in `MainWindow.xaml.cs`:
- image or video → open `MediaViewerOverlay`
- other → no-op for now

### Lifecycle

- Locking the active vault (already handled in `MainWindow.LockVaultRequested`) also disposes the video player by hiding the overlay, which triggers its cleanup.
- Closing the overlay disposes the `MediaPlayer` and `LibVLC` so the native handles are released and the IPC `ReadFileRange` calls stop.

---

## Error handling

| Failure | Behavior |
|---|---|
| Disguised vault, FVDT trailer missing or `base_offset` doesn't point at `FVLT` | Unlock fails: *"Vault file appears to be corrupted or modified by another application."* |
| Disguised vault, cover hash mismatch but signature found | Unlock succeeds; UI shows non-blocking warning. |
| `ReadFileRangeAsync` request out of bounds | Service returns empty buffer; LibVLC treats as EOF. |
| Unsupported video codec | LibVLC raises an error event; `VideoViewer` shows *"Cannot play this video"* overlay instead of crashing. |
| LibVLC native init failure | Caught at first video open, shown as *"Video playback unavailable: <message>"*. Image viewer keeps working. |
| Cover image file unreadable when picked in dialog | Dialog shows inline error; user can pick again or proceed without cover. |

## Testing

### Service unit tests

- `VaultPrefix.DetectBaseOffset`: undisguised file (returns 0); disguised file (returns offset of `FVLT`); malformed file with `FFD9` but no `FVLT` (throws); empty file (throws).
- `VaultStream`: position translation in both directions; `Seek` from each `SeekOrigin`; `Length` reporting.
- `ReadFileRangeAsync`: range entirely in one chunk; range spanning two chunks; range spanning many chunks; range at file start; range at file end; single-chunk legacy file; out-of-range request returns empty.
- Round-trip: chunked import of a 10 MB file → whole-file read returns identical bytes.
- Round-trip: create disguised vault → reopen → header block decrypts correctly.
- Round-trip: edit settings to change cover → reopen → new cover bytes match.

### UI unit tests

- `FakeServiceClient` gains `ReadFileRangeAsync` returning slices of an in-memory dictionary.
- `VideoViewerViewModel` open/close lifecycle disposes the player.
- `MediaViewerOverlay` prev/next navigation across a mixed image/video list swaps the inner viewer correctly.

### Manual test plan

- Disguise round-trip: create vault with cover → close app → verify Windows Explorer shows the cover image as the file thumbnail and double-clicking opens it in the default image viewer → reopen in FileVault and verify contents intact.
- External tamper: re-save the disguised JPEG in MS Paint → reopen in FileVault → verify the corruption error appears.
- Advanced password panel: expand/collapse, generate with default settings (collapsed), change settings and generate again.
- Video playback: import a 100 MB MP4 → open from list → verify play, pause, seek to middle, seek to end, scrub backwards.
- Mixed navigation: folder containing two images and two videos → open first image → press next four times → verify each item plays/displays in the correct viewer.
- Lock active vault while video is playing → verify player stops and overlay closes.

## Out of scope (explicit reminders)

- The video viewer does not support subtitles, audio track switching, or playback speed control in v1.
- The disguise feature does not support multi-image cover art or cycling thumbnails.
- The chunked storage refactor does not retroactively re-chunk existing single-blob files.
