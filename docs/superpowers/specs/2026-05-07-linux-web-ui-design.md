# FileVault Linux Web UI — Design Spec

**Date:** 2026-05-07
**Status:** Approved

## Problem

The Windows FileVault app used a persistent background service (named pipe server) + a WPF UI. On Linux, we want an on-demand app: run it, unlock the vault, do work, close it. No systemd unit, no autostart, no background process.

## Goal

A local web server (`FileVault.Web`) that the user launches on demand. Opens in the browser at `localhost:5000`. All existing vault format/crypto code (`FileVault.Service`) reused as-is.

---

## Architecture

### Projects

```
src/
  FileVault.Service/    ← unchanged (crypto, VaultFormat, VfsTree, VaultManager)
  FileVault.Web/        ← new (ASP.NET Core minimal API + static frontend)
    Program.cs
    wwwroot/
      index.html
      app.js
      style.css
```

`FileVault.Shared` (IPC named-pipe messages) is not used in the Linux app.

### Lifecycle

1. User runs: `dotnet run --project src/FileVault.Web`
2. Server starts on `127.0.0.1:5000`, prints the startup token to the terminal
3. User opens `http://localhost:5000` in their browser
4. User enters vault path + password → vault unlocked
5. User browses, imports, exports, organizes files
6. User hits Ctrl+C (or closes terminal) → process exits, vault file lock released

### Session State

One `VaultSession` held in-process via the existing `VaultManager`. No database, no disk writes beyond the vault file itself.

---

## API

All routes under `/api/`. Every request must include the `X-Vault-Token` header (see Security).

| Method   | Path                        | Purpose                                      |
|----------|-----------------------------|----------------------------------------------|
| `POST`   | `/api/vault/create`         | `{ path, displayName, password }` → creates new vault |
| `POST`   | `/api/vault/unlock`         | `{ path, password }` → opens session         |
| `POST`   | `/api/vault/lock`           | Closes session                               |
| `POST`   | `/api/vault/change-password`| `{ currentPassword, newPassword }`           |
| `GET`    | `/api/files/list?path=/`    | List folder contents (name, size, type, date)|
| `GET`    | `/api/files/stream?path=...`| Stream file in-memory (media viewing)        |
| `POST`   | `/api/files/import`         | Multipart upload → encrypted into vault      |
| `GET`    | `/api/files/export?path=...`| Decrypt → browser download                   |
| `POST`   | `/api/files/mkdir`          | `{ path }` → create folder                  |
| `DELETE` | `/api/files?path=...`       | Delete file or folder                        |
| `POST`   | `/api/files/rename`         | `{ path, newName }`                          |
| `POST`   | `/api/files/move`           | `{ sourcePath, destFolder }`                 |

`/api/files/stream` supports HTTP range requests (needed for video seek). All vault content responses include `Cache-Control: no-store`.

---

## Frontend UI

Single HTML page (`index.html`), plain ES modules — no framework, no build step.

### Locked State

Centered form with two modes toggled by tabs or links:

- **Unlock**: vault path input + password input + Unlock button. Inline error on wrong password.
- **Create**: vault path input + display name input + password input + confirm password input + Create button. On success, immediately unlocks and enters the unlocked state.

### Unlocked State

Two-panel layout:

- **Left sidebar**: collapsible folder tree. "Lock" button at top.
- **Main area**: file grid with image thumbnails and file-type icons for other files.
  - Toolbar: Import, Export (on selection), New Folder, Delete (on selection).
- **Media viewer overlay**: clicking an image or video opens it fullscreen.
  - Images: `<img src="/api/files/stream?path=...">` 
  - Video: `<video>` with range-request streaming, seek support.
  - Closes on Escape or outside click.

### Import Flow

Browser file picker (`<input type="file" multiple>`), uploaded via `fetch` with `multipart/form-data`. Progress shown inline per file.

### Export Flow

`GET /api/files/export?path=...` → `Content-Disposition: attachment` → browser Downloads folder.

### Token Injection

The startup token is rendered into `index.html` server-side (injected into a `<meta>` tag or a small inline `<script>`). The JS reads it once and attaches it as `X-Vault-Token` to every fetch request.

---

## Security

| Concern | Mitigation |
|---------|-----------|
| Other localhost processes hitting the API | Random 32-byte hex startup token required as `X-Vault-Token` header on every request |
| Media cached to browser disk cache | `Cache-Control: no-store` on all `/api/files/stream` and `/api/files/export` responses |
| Media written to server disk | Stream endpoint decrypts chunks in memory, pipes directly to HTTP response — no temp files |
| External network access | Server binds to `127.0.0.1` only |
| Vault accessed by other processes while open | `VaultManager.UnlockAsync` opens with `FileShare.None` |
| Session surviving process restart | `VaultSession` lives only in process memory; restart requires re-entering password |

---

## Out of Scope

- Cover image / disguised vault display (existing feature, not surfaced in web UI)
- Multi-vault support (one vault per session)
- Authentication beyond the vault password
