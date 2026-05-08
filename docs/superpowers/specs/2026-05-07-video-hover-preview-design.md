# Video Hover Preview — Design Spec

**Date:** 2026-05-07  
**Scope:** `FileVault.Web` only (`wwwroot/app.js`, `wwwroot/style.css`)  
**Backend changes:** None

---

## Goal

Video files in the file grid currently show a `🎬` emoji placeholder. Replace this with a live muted video preview that plays on hover and shows a still frame when idle.

---

## Behaviour

### Idle state (not hovered)
- A `<video>` element renders in the grid tile at the same size as image thumbnails.
- After metadata loads the player seeks to `Math.min(5, duration * 0.1)` seconds so the still frame is non-black content rather than the opening frame.
- A semi-transparent `▶` play-icon overlay is centred on the tile.
- `preload="metadata"` — the browser fetches only enough data to read duration/dimensions; the full video is not downloaded until hover.

### Hover state
- `mouseenter`: `video.currentTime = 0; video.play()` — plays from the beginning, muted, looping.
- The `▶` overlay hides while the video is playing.
- `mouseleave`: `video.pause()` — returns to the still-frame position (`Math.min(5, duration * 0.1)`), overlay re-appears.

### Error / unsupported format
- If the video element fires an `error` event (e.g. `.mkv` unsupported by the browser), the wrapper is replaced with the existing `🎬` emoji fallback — identical to today's behaviour.

---

## Files changed

### `app.js` — `renderFileItem`

Replace the `isVideo` branch (currently emitting a `div.file-icon` with `🎬`) with:

```
┌─ div.file-item ─────────────────────────┐
│  ┌─ div.video-thumb-wrapper ───────────┐│
│  │  <video muted loop preload=metadata>││
│  │  <div.video-play-overlay>▶</div>    ││
│  └─────────────────────────────────────┘│
│  <div.file-name>…</div>                 │
└─────────────────────────────────────────┘
```

Logic:
1. Create `video` element: `muted=true`, `loop=true`, `preload="metadata"`, `src=streamUrl(fullPath)`.
2. `video.addEventListener('loadedmetadata', ...)` → seek to still position.
3. `wrapper.addEventListener('mouseenter', ...)` → `video.currentTime=0; video.play(); overlay.style.opacity='0'`.
4. `wrapper.addEventListener('mouseleave', ...)` → `video.pause(); video.currentTime=stillPos; overlay.style.opacity='1'`.
5. `video.addEventListener('error', ...)` → replace wrapper with emoji fallback.

### `style.css`

```css
.video-thumb-wrapper {
  position: relative;
  width: 100%;
  aspect-ratio: 16/9;   /* matches standard video; overridden by object-fit */
  overflow: hidden;
}

.video-thumb-wrapper video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.video-play-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  color: #fff;
  background: rgba(0,0,0,0.25);
  pointer-events: none;
  transition: opacity 0.15s;
}
```

---

## What does not change

- Image thumbnails — unchanged (`<img class="file-thumb" src=streamUrl>`).
- The media viewer overlay that opens on double-click — unchanged.
- All backend routes — unchanged.
- The `🎬` emoji path for error/unsupported formats — preserved as fallback.

---

## Out of scope

- Audio on hover (explicitly muted by design).
- Server-side thumbnail extraction (not needed; browser decodes natively).
- List-view thumbnails (list view shows icon glyphs only, consistent with image behaviour).
- Lazy IntersectionObserver deferral (not needed; `preload="metadata"` is already lightweight).
