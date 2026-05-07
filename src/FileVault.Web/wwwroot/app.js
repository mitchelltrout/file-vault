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

// ── Unlocked state (stub — expanded in Tasks 6–7) ──────
function renderUnlocked(displayName) {
  const layout = document.createElement('div');
  layout.className = 'app-layout';
  const p = document.createElement('p');
  p.style.cssText = 'padding:2rem;color:#999';
  p.textContent = `Vault unlocked: ${displayName} — UI coming soon`;
  layout.appendChild(p);
  render(layout);
}

// ── Init ───────────────────────────────────────────────
renderLocked();
