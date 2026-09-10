const express = require('express');
const cors = require('cors');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

// Enable CORS and JSON body parser
app.use(cors());
app.use(express.json());

// Paths
const DATA_DIR = path.join(__dirname, '..', 'data');
const DB_FILE = path.join(DATA_DIR, 'db.json');
const PUBLIC_DIR = path.join(__dirname, '..', 'public');

// Ensure data directory exists
if (!fs.existsSync(DATA_DIR)) {
  fs.mkdirSync(DATA_DIR, { recursive: true });
}

// Initial seed data
const initialDb = {
  agents: [
    { id: 'LAP-DPACHERREZ', hostname: 'LAP-DPACHERREZ', username: 'dpacherrez', ipAddress: '10.0.0.25', version: '1.0.0', status: 'active', lastPing: new Date().toISOString() },
    { id: 'PC-DEV-02', hostname: 'PC-DEV-02', username: 'mrodriguez', ipAddress: '10.0.0.42', version: '1.0.0', status: 'active', lastPing: new Date().toISOString() },
    { id: 'PC-FINANCE-07', hostname: 'PC-FINANCE-07', username: 'cgonzalez', ipAddress: '10.0.0.88', version: '0.9.8', status: 'inactive', lastPing: new Date(Date.now() - 3600000 * 5).toISOString() }
  ],
  findings: [
    { 
      id: 'f1', 
      agentId: 'LAP-DPACHERREZ', 
      hostname: 'LAP-DPACHERREZ', 
      username: 'dpacherrez', 
      filePath: 'D:\\config.json', 
      ruleId: 'password-equals', 
      description: 'Coincidencia con password=', 
      severity: 'High', 
      secret: 'admin123', 
      lineNumber: 15, 
      fullMatch: 'password="admin123"', 
      detectedAt: new Date(Date.now() - 60000 * 5).toISOString() 
    },
    { 
      id: 'f2', 
      agentId: 'PC-DEV-02', 
      hostname: 'PC-DEV-02', 
      username: 'mrodriguez', 
      filePath: 'C:\\Users\\mrodriguez\\.env', 
      ruleId: 'api-key', 
      description: 'Llave API detectada', 
      severity: 'High', 
      secret: 'sk_live_9921abc...', 
      lineNumber: 4, 
      fullMatch: 'API_KEY=sk_live_9921abc...', 
      detectedAt: new Date(Date.now() - 60000 * 42).toISOString() 
    }
  ],
  events: [
    { id: 'e1', agentId: 'LAP-DPACHERREZ', hostname: 'LAP-DPACHERREZ', eventType: 'startup', severity: 'INFO', description: 'Aplicación iniciada correctamente', createdAt: new Date(Date.now() - 3600000 * 3).toISOString() },
    { id: 'e2', agentId: 'LAP-DPACHERREZ', hostname: 'LAP-DPACHERREZ', eventType: 'credential_found', severity: 'ALERT', description: 'Hallazgo en D:\\config.json (password-equals)', createdAt: new Date(Date.now() - 60000 * 5).toISOString() }
  ]
};

function loadDb() {
  try {
    if (fs.existsSync(DB_FILE)) {
      const data = fs.readFileSync(DB_FILE, 'utf8');
      return JSON.parse(data);
    }
  } catch (e) {
    console.error("Error cargando DB, reiniciando:", e.message);
  }
  saveDb(initialDb);
  return initialDb;
}

function saveDb(db) {
  try {
    fs.writeFileSync(DB_FILE, JSON.stringify(db, null, 2), 'utf8');
  } catch (e) {
    console.error("Error guardando DB:", e.message);
  }
}

// Serve static frontend Dashboard files
app.use(express.static(PUBLIC_DIR));

// ==========================================
// API REST ENDPOINTS
// ==========================================

// 1. GET /api/v1/agents
app.get('/api/v1/agents', (req, res) => {
  const dbData = loadDb();
  const now = Date.now();
  dbData.agents.forEach(a => {
    const pingTime = new Date(a.lastPing).getTime();
    a.status = (now - pingTime > 120000) ? 'inactive' : 'active';
  });
  res.json(dbData.agents);
});

// 2. POST /api/v1/agents/heartbeat & /register
const handleHeartbeat = (req, res) => {
  const dbData = loadDb();
  const payload = req.body || {};
  const hostname = payload.hostname || payload.agentId || 'DESCONOCIDO';
  const existingIndex = dbData.agents.findIndex(a => a.hostname === hostname || a.id === hostname);

  const agentObj = {
    id: hostname,
    hostname: hostname,
    username: payload.username || 'Usuario',
    ipAddress: payload.ipAddress || '127.0.0.1',
    version: payload.version || '1.0.0',
    status: 'active',
    lastPing: new Date().toISOString()
  };

  if (existingIndex >= 0) {
    dbData.agents[existingIndex] = { ...dbData.agents[existingIndex], ...agentObj };
  } else {
    dbData.agents.push(agentObj);
  }

  saveDb(dbData);
  res.json({ success: true, agent: agentObj });
};

app.post('/api/v1/agents/heartbeat', handleHeartbeat);
app.post('/api/v1/agents/register', handleHeartbeat);

// 3. POST /api/v1/agents/findings & /findings
const handleFindings = (req, res) => {
  const dbData = loadDb();
  const payload = req.body || {};
  const newFinding = {
    id: 'f_' + Date.now() + '_' + Math.floor(Math.random() * 1000),
    agentId: payload.hostname || payload.agentId || 'LAP-DPACHERREZ',
    hostname: payload.hostname || payload.agentId || 'LAP-DPACHERREZ',
    username: payload.username || 'dpacherrez',
    filePath: payload.filePath || 'D:\\archivo.txt',
    ruleId: payload.ruleId || 'password-equals',
    description: payload.description || 'Credencial detectada',
    severity: payload.severity || 'High',
    secret: payload.secret || 'secreto',
    lineNumber: payload.lineNumber || 1,
    fullMatch: payload.fullMatch || payload.secret || 'password="***"',
    detectedAt: new Date().toISOString()
  };

  dbData.findings.unshift(newFinding);

  // Auto record audit event
  dbData.events.unshift({
    id: 'e_' + Date.now(),
    agentId: newFinding.hostname,
    hostname: newFinding.hostname,
    eventType: 'credential_found',
    severity: 'ALERT',
    description: `Hallazgo en ${path.basename(newFinding.filePath)} (${newFinding.ruleId})`,
    createdAt: new Date().toISOString()
  });

  saveDb(dbData);
  res.status(201).json({ success: true, finding: newFinding });
};

app.post('/api/v1/agents/findings', handleFindings);
app.post('/api/v1/findings', handleFindings);

// 4. GET /api/v1/findings
app.get('/api/v1/findings', (req, res) => {
  const dbData = loadDb();
  res.json(dbData.findings);
});

// 5. POST /api/v1/agents/events & /events
const handleEvents = (req, res) => {
  const dbData = loadDb();
  const payload = req.body || {};
  const newEvent = {
    id: 'e_' + Date.now(),
    agentId: payload.hostname || payload.agentId || 'AGENT',
    hostname: payload.hostname || payload.agentId || 'AGENT',
    eventType: payload.eventType || 'info',
    severity: payload.severity || 'INFO',
    description: payload.description || 'Evento registrado',
    createdAt: new Date().toISOString()
  };

  dbData.events.unshift(newEvent);
  saveDb(dbData);
  res.status(201).json({ success: true, event: newEvent });
};

app.post('/api/v1/agents/events', handleEvents);
app.post('/api/v1/events', handleEvents);

// 6. GET /api/v1/events
app.get('/api/v1/events', (req, res) => {
  const dbData = loadDb();
  res.json(dbData.events);
});

// 7. GET /api/v1/stats
app.get('/api/v1/stats', (req, res) => {
  const dbData = loadDb();
  const activeAgents = dbData.agents.filter(a => a.status === 'active').length;
  const criticalFindings = dbData.findings.filter(f => f.severity === 'High').length;
  res.json({
    activeAgents,
    totalAgents: dbData.agents.length,
    totalFindings: dbData.findings.length,
    criticalFindings,
    totalEvents: dbData.events.length
  });
});

// 8. POST /api/v1/auth/login
app.post('/api/v1/auth/login', (req, res) => {
  const payload = req.body || {};
  res.json({
    success: true,
    token: 'jwt_mock_token_sentinel_2026',
    user: {
      username: payload.email ? payload.email.split('@')[0] : 'Admin',
      email: payload.email || 'admin@miatech.com',
      role: payload.role || 'Administrator'
    }
  });
});

// Catch-all route to serve Dashboard SPA
app.get('*', (req, res) => {
  res.sendFile(path.join(PUBLIC_DIR, 'index.html'));
});

// Start Express Server
app.listen(PORT, () => {
  console.log(`\n==================================================`);
  console.log(`🛡️  MIATECH SENTINEL - Servidor API & Dashboard Web`);
  console.log(`==================================================`);
  console.log(`🟢 Servidor Express activo en: http://localhost:${PORT}`);
  console.log(`📡 REST API Endpoint:        http://localhost:${PORT}/api/v1/agents/heartbeat`);
  console.log(`==================================================\n`);
});
