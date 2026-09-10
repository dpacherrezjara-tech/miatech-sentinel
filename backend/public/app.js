// === MIATECH SENTINEL DASHBOARD APP.JS ===
const API = '';  // Same origin (served by Express)

// ──────────────────────────────────────────
// STATE
// ──────────────────────────────────────────
let state = {
  token: null,
  user: null,
  currentPage: 'dashboard',
  findings: [],
  agents: [],
  events: [],
  stats: {}
};

// ──────────────────────────────────────────
// UTILS
// ──────────────────────────────────────────
function timeAgo(dateStr) {
  const diff = Date.now() - new Date(dateStr).getTime();
  const m = Math.floor(diff / 60000);
  const h = Math.floor(diff / 3600000);
  const d = Math.floor(diff / 86400000);
  if (d > 0) return d + 'd ago';
  if (h > 0) return h + 'h ago';
  if (m > 0) return m + 'm ago';
  return 'Ahora';
}

function sevClass(sev) {
  if (!sev) return '';
  const s = sev.toLowerCase();
  if (s === 'high' || s === 'critica' || s === 'alta') return 'sev-high';
  if (s === 'medium' || s === 'media') return 'sev-medium';
  return 'sev-low';
}

function sevLabel(sev) {
  if (!sev) return 'Info';
  const s = sev.toLowerCase();
  if (s === 'high' || s === 'alta') return 'Alta';
  if (s === 'medium' || s === 'media') return 'Media';
  if (s === 'low' || s === 'baja') return 'Baja';
  return sev;
}

function basename(p) {
  if (!p) return '';
  return p.replace(/\\/g, '/').split('/').pop();
}

async function apiFetch(endpoint, opts = {}) {
  try {
    const res = await fetch(API + endpoint, {
      ...opts,
      headers: { 'Content-Type': 'application/json', ...(opts.headers || {}) }
    });
    return await res.json();
  } catch (e) {
    console.error('API error:', endpoint, e);
    return null;
  }
}

// ──────────────────────────────────────────
// AUTH
// ──────────────────────────────────────────
document.getElementById('loginForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const email = document.getElementById('loginEmail').value;
  const pass  = document.getElementById('loginPassword').value;
  const btn   = document.getElementById('loginBtn');
  const err   = document.getElementById('loginError');
  btn.disabled = true;
  btn.querySelector('span').textContent = 'Ingresando...';
  err.classList.add('hidden');

  const data = await apiFetch('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password: pass })
  });

  if (data && data.success) {
    state.token = data.token;
    state.user  = data.user;
    document.getElementById('userDisplayName').textContent = data.user.username || 'Admin';
    document.getElementById('loginScreen').classList.add('hidden');
    document.getElementById('app').classList.remove('hidden');
    loadAllData();
    startPolling();
  } else {
    err.classList.remove('hidden');
    btn.disabled = false;
    btn.querySelector('span').textContent = 'Iniciar Sesion';
  }
});

document.getElementById('logoutBtn').addEventListener('click', () => {
  stopPolling();
  state.token = null;
  document.getElementById('app').classList.add('hidden');
  document.getElementById('loginScreen').classList.remove('hidden');
});

// ──────────────────────────────────────────
// NAVIGATION
// ──────────────────────────────────────────
const pageConfig = {
  dashboard: { title: 'Dashboard', subtitle: 'Vista general del sistema' },
  findings:  { title: 'Hallazgos', subtitle: 'Credenciales y datos sensibles detectados' },
  agents:    { title: 'Agentes', subtitle: 'Equipos monitoreados' },
  events:    { title: 'Eventos', subtitle: 'Registro de actividad del sistema' }
};

function navigateTo(page) {
  document.querySelectorAll('.page').forEach(p => p.classList.add('hidden'));
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
  const target = document.getElementById('page-' + page);
  if (target) { target.classList.remove('hidden'); target.classList.add('active'); }
  const navBtn = document.getElementById('nav-' + page);
  if (navBtn) navBtn.classList.add('active');
  state.currentPage = page;
  const cfg = pageConfig[page] || {};
  document.getElementById('pageTitle').textContent = cfg.title || page;
  document.getElementById('pageSubtitle').textContent = cfg.subtitle || '';
  renderPage(page);
}

document.querySelectorAll('.nav-item').forEach(btn => {
  btn.addEventListener('click', () => navigateTo(btn.dataset.page));
});

document.querySelectorAll('.btn-link').forEach(btn => {
  btn.addEventListener('click', () => { if (btn.dataset.page) navigateTo(btn.dataset.page); });
});

document.getElementById('refreshBtn').addEventListener('click', loadAllData);

// ──────────────────────────────────────────
// DATA LOADING
// ──────────────────────────────────────────
async function loadAllData() {
  const [stats, findings, agents, events] = await Promise.all([
    apiFetch('/api/v1/stats'),
    apiFetch('/api/v1/findings'),
    apiFetch('/api/v1/agents'),
    apiFetch('/api/v1/events')
  ]);
  if (stats)    state.stats    = stats;
  if (findings) state.findings = findings;
  if (agents)   state.agents   = agents;
  if (events)   state.events   = events;
  renderPage(state.currentPage);
  updateStats();
  updateBadges();
}

function updateStats() {
  const s = state.stats;
  document.getElementById('stat-critical').textContent = s.criticalFindings ?? 0;
  document.getElementById('stat-findings').textContent = s.totalFindings ?? 0;
  document.getElementById('stat-agents').textContent   = s.activeAgents ?? 0;
  document.getElementById('stat-events').textContent   = s.totalEvents ?? 0;
}

function updateBadges() {
  document.getElementById('findingsBadge').textContent = state.findings.length;
}

// ──────────────────────────────────────────
// RENDER FUNCTIONS
// ──────────────────────────────────────────
function renderPage(page) {
  if (page === 'dashboard') {
    renderRecentFindings();
    renderAgentsStatus();
    renderRecentEvents();
  } else if (page === 'findings') {
    renderAllFindings();
  } else if (page === 'agents') {
    renderAllAgents();
  } else if (page === 'events') {
    renderAllEvents();
  }
}

function renderRecentFindings() {
  const tbody = document.getElementById('recentFindingsBody');
  const recent = state.findings.slice(0, 8);
  if (!recent.length) { tbody.innerHTML = '<tr><td colspan="5" class="empty-state">Sin hallazgos</td></tr>'; return; }
  tbody.innerHTML = recent.map(f => `
    <tr>
      <td class="hostname">${f.hostname || f.agentId || '-'}</td>
      <td class="filepath" title="${f.filePath}">${basename(f.filePath)}</td>
      <td><span class="rule">${f.ruleId || '-'}</span></td>
      <td><span class="sev-badge ${sevClass(f.severity)}">${sevLabel(f.severity)}</span></td>
      <td>${timeAgo(f.detectedAt)}</td>
    </tr>`).join('');
}

function renderAllFindings() {
  const filter = document.getElementById('findingSeverityFilter').value;
  const tbody  = document.getElementById('allFindingsBody');
  let data = state.findings;
  if (filter) data = data.filter(f => f.severity === filter);
  if (!data.length) { tbody.innerHTML = '<tr><td colspan="6" class="empty-state">Sin hallazgos</td></tr>'; return; }
  tbody.innerHTML = data.map(f => `
    <tr>
      <td class="hostname">${f.hostname || f.agentId || '-'}</td>
      <td>${f.username || '-'}</td>
      <td class="filepath" title="${f.filePath}">${f.filePath || '-'}</td>
      <td><span class="rule">${f.ruleId || '-'}</span></td>
      <td><span class="sev-badge ${sevClass(f.severity)}">${sevLabel(f.severity)}</span></td>
      <td>${timeAgo(f.detectedAt)}</td>
    </tr>`).join('');
}

function renderAgentsStatus() {
  const container = document.getElementById('agentsStatusList');
  if (!state.agents.length) { container.innerHTML = '<div class="empty-state">Sin agentes</div>'; return; }
  container.innerHTML = state.agents.map(a => `
    <div class="agent-row">
      <span class="agent-status-dot ${a.status === 'active' ? 'status-active' : 'status-inactive'}"></span>
      <div class="agent-info">
        <div class="agent-host">${a.hostname}</div>
        <div class="agent-user">${a.username || ''}</div>
      </div>
      <span class="agent-ping">${timeAgo(a.lastPing)}</span>
    </div>`).join('');
}

function renderAllAgents() {
  const tbody = document.getElementById('allAgentsBody');
  if (!state.agents.length) { tbody.innerHTML = '<tr><td colspan="6" class="empty-state">Sin agentes</td></tr>'; return; }
  tbody.innerHTML = state.agents.map(a => `
    <tr>
      <td><span class="status-dot ${a.status === 'active' ? 'status-active' : 'status-inactive'}"></span>${a.status === 'active' ? 'Activo' : 'Inactivo'}</td>
      <td class="hostname">${a.hostname}</td>
      <td>${a.username || '-'}</td>
      <td>${a.ipAddress || '-'}</td>
      <td>${a.version || '-'}</td>
      <td>${timeAgo(a.lastPing)}</td>
    </tr>`).join('');
}

function eventDotClass(severity) {
  const s = (severity || '').toLowerCase();
  if (s === 'alert' || s === 'error') return 'event-alert';
  if (s === 'warn' || s === 'warning') return 'event-warn';
  return 'event-info';
}

function renderRecentEvents() {
  const container = document.getElementById('recentEventsList');
  const recent = state.events.slice(0, 10);
  if (!recent.length) { container.innerHTML = '<div class="empty-state">Sin eventos</div>'; return; }
  container.innerHTML = recent.map(e => `
    <div class="event-row">
      <span class="event-dot ${eventDotClass(e.severity)}"></span>
      <div class="event-body">
        <div class="event-desc">${e.description || e.eventType || '-'}</div>
        <div class="event-meta">${e.hostname || e.agentId || '-'} &bull; ${timeAgo(e.createdAt)}</div>
      </div>
    </div>`).join('');
}

function renderAllEvents() {
  const container = document.getElementById('allEventsList');
  if (!state.events.length) { container.innerHTML = '<div class="empty-state">Sin eventos</div>'; return; }
  container.innerHTML = state.events.map(e => `
    <div class="event-row">
      <span class="event-dot ${eventDotClass(e.severity)}"></span>
      <div class="event-body">
        <div class="event-desc">${e.description || e.eventType || '-'}</div>
        <div class="event-meta">${e.hostname || e.agentId || '-'} &bull; ${timeAgo(e.createdAt)}</div>
      </div>
    </div>`).join('');
}

// Filter on findings page
document.getElementById('findingSeverityFilter').addEventListener('change', () => renderAllFindings());

// ──────────────────────────────────────────
// POLLING (5s interval)
// ──────────────────────────────────────────
let pollTimer = null;
function startPolling() { pollTimer = setInterval(loadAllData, 5000); }
function stopPolling()  { if (pollTimer) { clearInterval(pollTimer); pollTimer = null; } }
