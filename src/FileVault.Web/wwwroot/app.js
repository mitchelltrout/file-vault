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
  const pathField = field('Vault path', 'text', '/home/user/Documents/MyVault.vault');
  const passField = field('Password', 'password', '');
  const btn = document.createElement('button');
  btn.className = 'btn btn-primary';
  btn.textContent = 'Unlock';

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
  const pathField = field('Vault path', 'text', '/home/user/Documents/NewVault.vault');
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

// ── Init ───────────────────────────────────────────────
renderLocked();
