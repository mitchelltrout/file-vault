// ── State ──────────────────────────────────────────────
const TOKEN = window.VAULT_TOKEN;
let vaultPath = '';
let currentPath = '/';
let selected = new Set();

// ── API helper ─────────────────────────────────────────
async function api(method, url, body) {
  const opts = { method, headers: { 'X-Vault-Token': TOKEN } };
  if (body instanceof FormData) {
    opts.body = body;
  } else if (body !== undefined) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(url, opts);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? res.statusText);
  }
  const ct = res.headers.get('Content-Type') ?? '';
  if (ct.includes('application/json')) return res.json();
  return res;
}

// ── Recent vaults (localStorage) ──────────────────────
const RECENTS_KEY = 'filevault_recents';
const RECENTS_MAX = 5;

function loadRecents() {
  try { return JSON.parse(localStorage.getItem(RECENTS_KEY) || '[]'); }
  catch { return []; }
}

function addRecent(path, displayName) {
  const list = loadRecents().filter(r => r.path !== path);
  list.unshift({ path, displayName });
  localStorage.setItem(RECENTS_KEY, JSON.stringify(list.slice(0, RECENTS_MAX)));
}

function removeRecent(path) {
  const list = loadRecents().filter(r => r.path !== path);
  localStorage.setItem(RECENTS_KEY, JSON.stringify(list));
}

function buildRecentsSection(pathInput, passInput) {
  const recents = loadRecents();
  if (recents.length === 0) return null;

  const section = document.createElement('div');
  section.className = 'recents-section';

  const lbl = document.createElement('div');
  lbl.className = 'recents-label';
  lbl.textContent = 'Recent vaults';
  section.appendChild(lbl);

  const list = document.createElement('div');
  list.className = 'recents-list';

  for (const r of recents) {
    const item = document.createElement('div');
    item.className = 'recents-item';

    const info = document.createElement('div');
    info.className = 'recents-info';
    info.onclick = () => { pathInput.value = r.path; passInput.focus(); };

    const name = document.createElement('div');
    name.className = 'recents-name';
    name.textContent = r.displayName;

    const pathEl = document.createElement('div');
    pathEl.className = 'recents-path';
    pathEl.textContent = r.path;

    info.appendChild(name);
    info.appendChild(pathEl);

    const rmBtn = document.createElement('button');
    rmBtn.className = 'recents-remove';
    rmBtn.textContent = '×';
    rmBtn.title = 'Remove from recents';
    rmBtn.onclick = e => {
      e.stopPropagation();
      removeRecent(r.path);
      item.remove();
      if (list.children.length === 0) section.remove();
    };

    item.appendChild(info);
    item.appendChild(rmBtn);
    list.appendChild(item);
  }

  section.appendChild(list);
  return section;
}

// ── Stream URL (uses ?token= for <img>/<video> src) ────
function streamUrl(filePath) {
  return `/api/files/stream?vaultPath=${enc(vaultPath)}&path=${enc(filePath)}&token=${enc(TOKEN)}`;
}

function enc(s) { return encodeURIComponent(s); }

// ── Root render ────────────────────────────────────────
const app = document.getElementById('app');

function render(el) {
  app.innerHTML = '';
  app.appendChild(el);
}

// ── Locked state ───────────────────────────────────────
function renderLocked() {
  let mode = 'unlock';

  const screen = document.createElement('div');
  screen.className = 'locked-screen';

  const card = document.createElement('div');
  card.className = 'card';
  screen.appendChild(card);

  function buildCard() {
    card.innerHTML = `<h1>🔒 FileVault</h1>`;

    const tabs = document.createElement('div');
    tabs.className = 'tabs';
    ['Unlock', 'Create'].forEach(label => {
      const btn = document.createElement('button');
      btn.className = 'tab' + (label.toLowerCase() === mode ? ' active' : '');
      btn.textContent = label;
      btn.onclick = () => { mode = label.toLowerCase(); buildCard(); };
      tabs.appendChild(btn);
    });
    card.appendChild(tabs);

    const errorEl = document.createElement('div');
    errorEl.className = 'error';
    card.appendChild(errorEl);

    if (mode === 'unlock') buildUnlockForm(card, errorEl);
    else buildCreateForm(card, errorEl);
  }

  buildCard();
  render(screen);
}

function buildUnlockForm(card, errorEl) {
  const pathField = fieldWithBrowse('Vault path', 'open', '/home/user/Documents/MyVault.vault');
  const passField = field('Password', 'password', '');
  const btn = document.createElement('button');
  btn.className = 'btn btn-primary';
  btn.textContent = 'Unlock';

  const recentsSection = buildRecentsSection(pathField.input, passField.input);
  if (recentsSection) card.appendChild(recentsSection);
  [pathField.el, passField.el, errorEl, btn].forEach(e => card.appendChild(e));

  const submitOnEnter = e => { if (e.key === 'Enter') btn.click(); };
  pathField.input.addEventListener('keydown', submitOnEnter);
  passField.input.addEventListener('keydown', submitOnEnter);
  btn.onclick = async () => {
    errorEl.textContent = '';
    btn.disabled = true;
    try {
      const data = await api('POST', '/api/vault/unlock',
        { path: pathField.input.value, password: passField.input.value });
      addRecent(pathField.input.value, data.displayName);
      vaultPath = pathField.input.value;
      currentPath = '/';
      renderUnlocked(data.displayName);
    } catch (e) {
      errorEl.textContent = e.message;
    } finally {
      btn.disabled = false;
    }
  };
}

function buildCreateForm(card, errorEl) {
  const pathField = fieldWithBrowse('Vault path', 'save', '/home/user/Documents/NewVault.vault');
  const nameField = field('Display name', 'text', '');
  const passField = field('Password', 'password', '');
  const confirmField = field('Confirm password', 'password', '');
  const btn = document.createElement('button');
  btn.className = 'btn btn-primary';
  btn.textContent = 'Create';

  [pathField.el, nameField.el, passField.el, confirmField.el, errorEl, btn]
    .forEach(e => card.appendChild(e));

  btn.onclick = async () => {
    errorEl.textContent = '';
    if (passField.input.value !== confirmField.input.value) {
      errorEl.textContent = 'Passwords do not match.';
      return;
    }
    btn.disabled = true;
    try {
      const data = await api('POST', '/api/vault/create', {
        path: pathField.input.value,
        displayName: nameField.input.value,
        password: passField.input.value,
      });
      addRecent(pathField.input.value, data.displayName);
      vaultPath = pathField.input.value;
      currentPath = '/';
      renderUnlocked(data.displayName);
    } catch (e) {
      errorEl.textContent = e.message;
    } finally {
      btn.disabled = false;
    }
  };
}

function field(labelText, type, placeholder) {
  const el = document.createElement('div');
  el.className = 'field';
  const lbl = document.createElement('label');
  lbl.textContent = labelText;
  const input = document.createElement('input');
  input.type = type;
  input.placeholder = placeholder;
  el.appendChild(lbl);
  el.appendChild(input);
  return { el, input };
}

function fieldWithBrowse(labelText, mode, placeholder) {
  const el = document.createElement('div');
  el.className = 'field';
  const lbl = document.createElement('label');
  lbl.textContent = labelText;
  const row = document.createElement('div');
  row.style.cssText = 'display:flex;gap:0.5rem;align-items:center';
  const input = document.createElement('input');
  input.type = 'text';
  input.placeholder = placeholder;
  input.style.cssText = 'flex:1;min-width:0';
  const browseBtn = document.createElement('button');
  browseBtn.type = 'button';
  browseBtn.className = 'btn btn-secondary';
  browseBtn.textContent = '📂';
  browseBtn.title = 'Browse…';
  browseBtn.onclick = async () => {
    const chosen = await showFsBrowserModal(mode, input.value);
    if (chosen) input.value = chosen;
  };
  row.appendChild(input);
  row.appendChild(browseBtn);
  el.appendChild(lbl);
  el.appendChild(row);
  return { el, input };
}

// ── Unlocked state ─────────────────────────────────────
async function renderUnlocked(displayName) {
  selected.clear();

  const layout = document.createElement('div');
  layout.className = 'app-layout';

  // Sidebar
  const sidebar = document.createElement('div');
  sidebar.className = 'sidebar';
  sidebar.innerHTML = `
    <div class="sidebar-header">
      <h2></h2>
      <button class="btn btn-secondary" id="btn-lock" title="Lock vault">🔒</button>
    </div>
    <div class="folder-tree" id="folder-tree"></div>
  `;
  sidebar.querySelector('h2').textContent = displayName;

  // Main area
  const main = document.createElement('div');
  main.className = 'main';
  main.innerHTML = `
    <div class="toolbar">
      <div class="breadcrumb" id="breadcrumb"></div>
      <button class="btn btn-secondary" id="btn-import">⬆ Import</button>
      <button class="btn btn-secondary" id="btn-mkdir">📁 New Folder</button>
      <button class="btn btn-secondary" id="btn-export" disabled>⬇ Export</button>
      <button class="btn btn-secondary" id="btn-rename" disabled>✏ Rename</button>
      <button class="btn btn-secondary" id="btn-move" disabled>📦 Move</button>
      <button class="btn btn-danger" id="btn-delete" disabled>🗑 Delete</button>
    </div>
    <div class="file-grid" id="file-grid"></div>
    <input type="file" id="file-input" multiple style="display:none" />
  `;

  layout.appendChild(sidebar);
  layout.appendChild(main);
  render(layout);

  // Wire toolbar buttons
  document.getElementById('btn-lock').onclick = async () => {
    await api('POST', '/api/vault/lock', { path: vaultPath });
    vaultPath = '';
    renderLocked();
  };

  document.getElementById('btn-import').onclick = () =>
    document.getElementById('file-input').click();
  document.getElementById('file-input').onchange = e => importFiles(e.target.files);

  document.getElementById('btn-mkdir').onclick = () => promptMkDir();
  document.getElementById('btn-export').onclick = () => exportSelected();
  document.getElementById('btn-rename').onclick = () => promptRename();
  document.getElementById('btn-move').onclick = () => promptMove();
  document.getElementById('btn-delete').onclick = () => confirmDelete();

  await loadFolder(currentPath);
}

async function loadFolder(path) {
  currentPath = path;
  selected.clear();
  updateToolbarButtons();
  renderBreadcrumb(path);

  const grid = document.getElementById('file-grid');
  grid.innerHTML = '<span style="color:#666;padding:.5rem">Loading…</span>';

  try {
    const items = await api('GET',
      `/api/files/list?vaultPath=${enc(vaultPath)}&path=${enc(path)}`);
    grid.innerHTML = '';

    // Sort: folders first, then files, both alphabetically
    items.sort((a, b) => {
      if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
      return a.name.localeCompare(b.name);
    });

    items.forEach(item => grid.appendChild(renderFileItem(item, path)));

    // Refresh folder tree
    await renderFolderTree();
  } catch (e) {
    grid.innerHTML = `<span class="error"></span>`;
    grid.querySelector('.error').textContent = e.message;
  }
}

function renderFileItem(item, parentPath) {
  const fullPath = parentPath.replace(/\/$/, '') + '/' + item.name;
  const el = document.createElement('div');
  el.className = 'file-item';
  el.dataset.path = fullPath;

  if (item.isDirectory) {
    const icon = document.createElement('div');
    icon.className = 'file-icon';
    icon.textContent = '📁';
    const name = document.createElement('div');
    name.className = 'file-name';
    name.textContent = item.name;
    el.appendChild(icon);
    el.appendChild(name);
    el.ondblclick = () => loadFolder(fullPath);
    el.onclick = e => toggleSelect(el, fullPath, e);
  } else {
    const ext = item.name.split('.').pop().toLowerCase();
    const isImage = ['jpg','jpeg','png','gif','webp'].includes(ext);
    const isVideo = ['mp4','webm','mov','mkv'].includes(ext);

    if (isImage) {
      const thumb = document.createElement('img');
      thumb.className = 'file-thumb';
      thumb.src = streamUrl(fullPath);
      thumb.loading = 'lazy';
      thumb.alt = item.name;
      const name = document.createElement('div');
      name.className = 'file-name';
      name.textContent = item.name;
      el.appendChild(thumb);
      el.appendChild(name);
    } else {
      const icon = document.createElement('div');
      icon.className = 'file-icon';
      icon.textContent = isVideo ? '🎬' : fileIcon(ext);
      const name = document.createElement('div');
      name.className = 'file-name';
      name.textContent = item.name;
      el.appendChild(icon);
      el.appendChild(name);
    }

    el.onclick = e => toggleSelect(el, fullPath, e);
    el.ondblclick = () => {
      if (isImage || isVideo) openMediaViewer(fullPath, isVideo);
      else exportFile(fullPath, item.name);
    };
  }

  return el;
}

function fileIcon(ext) {
  const map = {
    pdf: '📄', doc: '📝', docx: '📝', txt: '📝', mp3: '🎵', wav: '🎵',
    zip: '🗜', rar: '🗜', '7z': '🗜', xls: '📊', xlsx: '📊', ppt: '📊',
  };
  return map[ext] ?? '📎';
}

function toggleSelect(el, path, e) {
  if (e.shiftKey || e.ctrlKey || e.metaKey) {
    if (selected.has(path)) { selected.delete(path); el.classList.remove('selected'); }
    else { selected.add(path); el.classList.add('selected'); }
  } else {
    document.querySelectorAll('.file-item.selected').forEach(i => i.classList.remove('selected'));
    selected.clear();
    selected.add(path);
    el.classList.add('selected');
  }
  updateToolbarButtons();
}

function updateToolbarButtons() {
  const count = selected.size;
  const exportBtn = document.getElementById('btn-export');
  const renameBtn = document.getElementById('btn-rename');
  const moveBtn = document.getElementById('btn-move');
  const deleteBtn = document.getElementById('btn-delete');
  if (!exportBtn || !renameBtn || !moveBtn || !deleteBtn) return;
  exportBtn.disabled = count === 0;
  renameBtn.disabled = count !== 1;
  moveBtn.disabled = count === 0;
  deleteBtn.disabled = count === 0;
}

function renderBreadcrumb(path) {
  const bc = document.getElementById('breadcrumb');
  if (!bc) return;
  const parts = path.split('/').filter(Boolean);
  bc.innerHTML = '';

  const rootSpan = document.createElement('span');
  rootSpan.textContent = '🏠 /';
  rootSpan.onclick = () => loadFolder('/');
  bc.appendChild(rootSpan);

  let accumulated = '';
  parts.forEach((part, i) => {
    accumulated += '/' + part;
    const sep = document.createTextNode(' › ');
    bc.appendChild(sep);
    if (i < parts.length - 1) {
      const span = document.createElement('span');
      span.textContent = part;
      const cap = accumulated;
      span.onclick = () => loadFolder(cap);
      bc.appendChild(span);
    } else {
      bc.appendChild(document.createTextNode(part));
    }
  });
}

async function renderFolderTree() {
  const tree = document.getElementById('folder-tree');
  if (!tree) return;
  tree.innerHTML = '';
  await buildTreeNode(tree, '/', 0);
}

const TREE_MAX_DEPTH = 4;

async function buildTreeNode(container, path, depth) {
  if (depth >= TREE_MAX_DEPTH) return;
  try {
    const items = await api('GET',
      `/api/files/list?vaultPath=${enc(vaultPath)}&path=${enc(path)}`);
    const folders = items.filter(i => i.isDirectory);

    for (const f of folders) {
      const fullPath = path.replace(/\/$/, '') + '/' + f.name;
      const item = document.createElement('div');
      item.className = 'tree-item' + (currentPath === fullPath ? ' active' : '');
      item.style.paddingLeft = `${1 + depth * 1}rem`;
      const icon = document.createElement('span');
      icon.className = 'tree-icon';
      icon.textContent = '📁';
      item.appendChild(icon);
      item.appendChild(document.createTextNode(' ' + f.name));
      item.onclick = () => loadFolder(fullPath);
      container.appendChild(item);
      await buildTreeNode(container, fullPath, depth + 1);
    }
  } catch { /* ignore tree errors silently */ }
}

// ── Media viewer overlay ───────────────────────────────
function openMediaViewer(filePath, isVideo) {
  const overlay = document.createElement('div');
  overlay.className = 'overlay';

  function close() {
    overlay.remove();
    document.removeEventListener('keydown', onKey);
  }

  function onKey(e) {
    if (e.key === 'Escape') close();
  }
  document.addEventListener('keydown', onKey);

  const closeBtn = document.createElement('button');
  closeBtn.className = 'overlay-close';
  closeBtn.textContent = '×';
  closeBtn.onclick = close;
  overlay.appendChild(closeBtn);

  if (isVideo) {
    const video = document.createElement('video');
    video.src = streamUrl(filePath);
    video.controls = true;
    video.autoplay = true;
    overlay.appendChild(video);
  } else {
    const img = document.createElement('img');
    img.src = streamUrl(filePath);
    overlay.appendChild(img);
  }

  overlay.onclick = e => { if (e.target === overlay) close(); };
  document.body.appendChild(overlay);
}

// ── Export ─────────────────────────────────────────────
function exportSelected() {
  for (const path of selected) exportFile(path, path.split('/').pop());
}

function exportFile(filePath, filename) {
  const a = document.createElement('a');
  a.href = `/api/files/export?vaultPath=${enc(vaultPath)}&path=${enc(filePath)}&token=${enc(TOKEN)}`;
  a.download = filename;
  a.click();
}

// ── Import ─────────────────────────────────────────────
async function importFiles(fileList) {
  if (!fileList.length) return;
  const form = new FormData();
  form.append('vaultPath', vaultPath);
  form.append('folder', currentPath);
  for (const f of fileList) form.append('files', f, f.name);

  try {
    await api('POST', '/api/files/import', form);
    await loadFolder(currentPath);
  } catch (e) {
    alert('Import failed: ' + e.message);
  } finally {
    const fileInput = document.getElementById('file-input');
    if (fileInput) fileInput.value = '';
  }
}

// ── Mkdir ──────────────────────────────────────────────
async function promptMkDir() {
  const name = await showInputModal('New Folder', 'Folder name', '');
  if (!name) return;
  try {
    await api('POST', '/api/files/mkdir', {
      vaultPath, path: currentPath.replace(/\/$/, '') + '/' + name
    });
    await loadFolder(currentPath);
  } catch (e) {
    alert('Error: ' + e.message);
  }
}

// ── Rename ─────────────────────────────────────────────
async function promptRename() {
  const [path] = selected;
  const oldName = path.split('/').pop();
  const newName = await showInputModal('Rename', 'New name', oldName);
  if (!newName || newName === oldName) return;
  try {
    await api('POST', '/api/files/rename', { vaultPath, path, newName });
    await loadFolder(currentPath);
  } catch (e) {
    alert('Error: ' + e.message);
  }
}

// ── Move ───────────────────────────────────────────────
async function promptMove() {
  const destFolder = await showInputModal('Move', 'Destination folder path', currentPath);
  if (!destFolder) return;
  for (const sourcePath of selected) {
    try {
      await api('POST', '/api/files/move', { vaultPath, sourcePath, destFolder });
    } catch (e) {
      alert(`Error moving ${sourcePath.split('/').pop()}: ${e.message}`);
    }
  }
  await loadFolder(currentPath);
}

// ── Delete ─────────────────────────────────────────────
async function confirmDelete() {
  const names = [...selected].map(p => p.split('/').pop()).join(', ');
  if (!confirm(`Delete ${names}?`)) return;
  for (const path of selected) {
    try {
      await api('DELETE', `/api/files?vaultPath=${enc(vaultPath)}&path=${enc(path)}`);
    } catch (e) {
      alert(`Error deleting ${path.split('/').pop()}: ${e.message}`);
    }
  }
  await loadFolder(currentPath);
}

// ── Input modal helper ─────────────────────────────────
function showInputModal(title, label, defaultValue) {
  return new Promise(resolve => {
    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop';
    const modal = document.createElement('div');
    modal.className = 'modal';

    const h3 = document.createElement('h3');
    h3.textContent = title;
    modal.appendChild(h3);

    const fieldDiv = document.createElement('div');
    fieldDiv.className = 'field';
    const lbl = document.createElement('label');
    lbl.textContent = label;
    const input = document.createElement('input');
    input.type = 'text';
    input.value = defaultValue;
    fieldDiv.appendChild(lbl);
    fieldDiv.appendChild(input);
    modal.appendChild(fieldDiv);

    const actions = document.createElement('div');
    actions.className = 'modal-actions';
    const cancelBtn = document.createElement('button');
    cancelBtn.className = 'btn btn-secondary';
    cancelBtn.textContent = 'Cancel';
    const okBtn = document.createElement('button');
    okBtn.className = 'btn btn-primary';
    okBtn.textContent = 'OK';
    actions.appendChild(cancelBtn);
    actions.appendChild(okBtn);
    modal.appendChild(actions);

    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);

    input.focus();
    input.select();

    const done = val => { backdrop.remove(); resolve(val); };
    cancelBtn.onclick = () => done(null);
    okBtn.onclick = () => done(input.value.trim() || null);
    input.addEventListener('keydown', e => {
      if (e.key === 'Enter') done(input.value.trim() || null);
      if (e.key === 'Escape') done(null);
    });
  });
}

// ── Filesystem browser modal ───────────────────────────
// mode 'open'  → user picks an existing .vault file; resolves full path string or null
// mode 'save'  → user picks a directory + types filename; resolves full path string or null
function showFsBrowserModal(mode, initialPath) {
  return new Promise(resolve => {
    // Derive starting directory from the current input value
    let startDir = '';
    if (initialPath) {
      const lastSlash = initialPath.lastIndexOf('/');
      startDir = lastSlash > 0 ? initialPath.substring(0, lastSlash) : initialPath;
    }

    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop';
    const modal = document.createElement('div');
    modal.className = 'modal';
    modal.style.width = '480px';

    const h3 = document.createElement('h3');
    h3.textContent = mode === 'open' ? 'Select Vault File' : 'Select Destination Folder';
    modal.appendChild(h3);

    const pathDisplay = document.createElement('div');
    pathDisplay.className = 'fs-browser-path';
    modal.appendChild(pathDisplay);

    const listEl = document.createElement('div');
    listEl.className = 'fs-list';
    modal.appendChild(listEl);

    // 'save' mode: filename input
    let filenameInput = null;
    if (mode === 'save') {
      const fnField = document.createElement('div');
      fnField.className = 'field';
      const fnLbl = document.createElement('label');
      fnLbl.textContent = 'File name';
      filenameInput = document.createElement('input');
      filenameInput.type = 'text';
      filenameInput.placeholder = 'MyVault.vault';
      fnField.appendChild(fnLbl);
      fnField.appendChild(filenameInput);
      modal.appendChild(fnField);
    }

    const actions = document.createElement('div');
    actions.className = 'modal-actions';
    const cancelBtn = document.createElement('button');
    cancelBtn.className = 'btn btn-secondary';
    cancelBtn.textContent = 'Cancel';
    const okBtn = document.createElement('button');
    okBtn.className = 'btn btn-primary';
    okBtn.textContent = mode === 'open' ? 'Open' : 'Select';
    okBtn.disabled = true;
    actions.appendChild(cancelBtn);
    actions.appendChild(okBtn);
    modal.appendChild(actions);

    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);

    let currentDirPath = '';
    let selectedFile = null;

    const done = val => { backdrop.remove(); resolve(val); };
    cancelBtn.onclick = () => done(null);

    okBtn.onclick = () => {
      if (mode === 'open' && selectedFile) {
        done(currentDirPath.replace(/\/$/, '') + '/' + selectedFile);
      } else if (mode === 'save' && filenameInput) {
        const name = filenameInput.value.trim();
        if (name) done(currentDirPath.replace(/\/$/, '') + '/' + name);
      }
    };

    if (filenameInput) {
      filenameInput.addEventListener('input', () => {
        okBtn.disabled = !filenameInput.value.trim();
      });
      filenameInput.addEventListener('keydown', e => {
        if (e.key === 'Enter' && filenameInput.value.trim()) okBtn.click();
        if (e.key === 'Escape') done(null);
      });
    }

    async function navigate(dir) {
      currentDirPath = dir;
      selectedFile = null;
      if (mode === 'open') okBtn.disabled = true;

      listEl.innerHTML = '';
      const loading = document.createElement('div');
      loading.className = 'fs-list-empty';
      loading.textContent = 'Loading…';
      listEl.appendChild(loading);

      try {
        const data = await api('GET', `/api/fs/list?path=${enc(dir)}`);
        currentDirPath = data.path;
        pathDisplay.textContent = data.path;
        listEl.innerHTML = '';

        if (data.parent) {
          const item = document.createElement('div');
          item.className = 'fs-list-item';
          item.appendChild(document.createTextNode('📁 ..'));
          item.onclick = () => navigate(data.parent);
          listEl.appendChild(item);
        }

        for (const dirName of data.dirs) {
          const item = document.createElement('div');
          item.className = 'fs-list-item';
          item.appendChild(document.createTextNode('📁 ' + dirName));
          const target = data.path.replace(/\/$/, '') + '/' + dirName;
          item.onclick = () => navigate(target);
          listEl.appendChild(item);
        }

        if (mode === 'open') {
          for (const fileName of data.vaultFiles) {
            const item = document.createElement('div');
            item.className = 'fs-list-item';
            item.appendChild(document.createTextNode('🔒 ' + fileName));
            item.onclick = () => {
              listEl.querySelectorAll('.selected').forEach(i => i.classList.remove('selected'));
              item.classList.add('selected');
              selectedFile = fileName;
              okBtn.disabled = false;
            };
            item.ondblclick = () => done(data.path.replace(/\/$/, '') + '/' + fileName);
            listEl.appendChild(item);
          }
        }

        if (listEl.children.length === 0) {
          const empty = document.createElement('div');
          empty.className = 'fs-list-empty';
          empty.textContent = mode === 'open' ? 'No .vault files found here.' : 'Empty directory.';
          listEl.appendChild(empty);
        }

        if (mode === 'save' && filenameInput) filenameInput.focus();
      } catch (e) {
        listEl.innerHTML = '';
        const err = document.createElement('div');
        err.className = 'fs-list-empty';
        err.style.color = '#f87171';
        err.textContent = e.message;
        listEl.appendChild(err);
      }
    }

    navigate(startDir);
  });
}

// ── Init ───────────────────────────────────────────────
renderLocked();
