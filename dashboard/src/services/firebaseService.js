import { 
  collection, 
  onSnapshot, 
  query, 
  orderBy, 
  limit, 
  addDoc, 
  doc, 
  setDoc, 
  serverTimestamp 
} from 'firebase/firestore';
import { db } from '../firebase/config';

// Mock initial state for offline/demo mode when Firebase credentials are default
const MOCK_AGENTS = [
  { id: 'LAP-DPACHERREZ', hostname: 'LAP-DPACHERREZ', username: 'dpacherrez', ipAddress: '10.0.0.25', version: '1.0.0', status: 'active', lastPing: new Date().toISOString() },
  { id: 'PC-DEV-02', hostname: 'PC-DEV-02', username: 'mrodriguez', ipAddress: '10.0.0.42', version: '1.0.0', status: 'active', lastPing: new Date().toISOString() },
  { id: 'PC-FINANCE-07', hostname: 'PC-FINANCE-07', username: 'cgonzalez', ipAddress: '10.0.0.88', version: '0.9.8', status: 'inactive', lastPing: new Date(Date.now() - 3600000 * 5).toISOString() },
  { id: 'PC-QA-01', hostname: 'PC-QA-01', username: 'jmartinez', ipAddress: '10.0.0.12', version: '1.0.0', status: 'active', lastPing: new Date().toISOString() }
];

const MOCK_FINDINGS = [
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
  },
  { 
    id: 'f3', 
    agentId: 'LAP-DPACHERREZ', 
    hostname: 'LAP-DPACHERREZ', 
    username: 'dpacherrez', 
    filePath: 'C:\\Users\\dpacherrez\\Documents\\db_setup.sql', 
    ruleId: 'sql-script', 
    description: 'Script SQL con credenciales', 
    severity: 'Medium', 
    secret: 'usr_mi_dpacherrez', 
    lineNumber: 2, 
    fullMatch: 'GRANT ALL ON db_main TO usr_mi_dpacherrez IDENTIFIED BY "pass123"', 
    detectedAt: new Date(Date.now() - 3600000 * 2).toISOString() 
  }
];

const MOCK_EVENTS = [
  { id: 'e1', agentId: 'LAP-DPACHERREZ', hostname: 'LAP-DPACHERREZ', eventType: 'startup', severity: 'INFO', description: 'Aplicación iniciada correctamente', createdAt: new Date(Date.now() - 3600000 * 3).toISOString() },
  { id: 'e2', agentId: 'LAP-DPACHERREZ', hostname: 'LAP-DPACHERREZ', eventType: 'credential_found', severity: 'ALERT', description: 'Hallazgo en D:\\config.json (password-equals)', createdAt: new Date(Date.now() - 60000 * 5).toISOString() },
  { id: 'e3', agentId: 'PC-DEV-02', hostname: 'PC-DEV-02', eventType: 'monitoring_started', severity: 'INFO', description: 'Monitoreo iniciado en 3 rutas', createdAt: new Date(Date.now() - 3600000 * 6).toISOString() },
  { id: 'e4', agentId: 'PC-FINANCE-07', hostname: 'PC-FINANCE-07', eventType: 'unauthorized_attempt', severity: 'WARNING', description: 'Intento de cierre de app sin token válido', createdAt: new Date(Date.now() - 3600000 * 5).toISOString() }
];

export function subscribeToAgents(callback) {
  try {
    const q = query(collection(db, 'agents'));
    return onSnapshot(q, (snapshot) => {
      const agents = snapshot.docs.map(doc => ({ id: doc.id, ...doc.data() }));
      if (agents.length > 0) {
        callback(agents);
      } else {
        callback(MOCK_AGENTS);
      }
    }, (error) => {
      console.warn("Firestore subscriptor agents falló o usando modo demo:", error.message);
      callback(MOCK_AGENTS);
    });
  } catch (err) {
    callback(MOCK_AGENTS);
    return () => {};
  }
}

export function subscribeToFindings(callback) {
  try {
    const q = query(collection(db, 'findings'), orderBy('detectedAt', 'desc'), limit(50));
    return onSnapshot(q, (snapshot) => {
      const findings = snapshot.docs.map(doc => ({ id: doc.id, ...doc.data() }));
      if (findings.length > 0) {
        callback(findings);
      } else {
        callback(MOCK_FINDINGS);
      }
    }, (error) => {
      console.warn("Firestore subscriptor findings falló o usando modo demo:", error.message);
      callback(MOCK_FINDINGS);
    });
  } catch (err) {
    callback(MOCK_FINDINGS);
    return () => {};
  }
}

export function subscribeToEvents(callback) {
  try {
    const q = query(collection(db, 'events'), orderBy('createdAt', 'desc'), limit(50));
    return onSnapshot(q, (snapshot) => {
      const events = snapshot.docs.map(doc => ({ id: doc.id, ...doc.data() }));
      if (events.length > 0) {
        callback(events);
      } else {
        callback(MOCK_EVENTS);
      }
    }, (error) => {
      console.warn("Firestore subscriptor events falló o usando modo demo:", error.message);
      callback(MOCK_EVENTS);
    });
  } catch (err) {
    callback(MOCK_EVENTS);
    return () => {};
  }
}

// Helper to inject a simulated finding for demo / verification
export async function injectSimulatedFinding(agentId, filePath, ruleId, secret) {
  const newFinding = {
    agentId: agentId || 'LAP-DPACHERREZ',
    hostname: agentId || 'LAP-DPACHERREZ',
    username: 'dpacherrez',
    filePath: filePath || 'D:\\proyectos\\mi_clave.txt',
    ruleId: ruleId || 'contrasena-colon',
    description: 'Contraseña en texto plano detectada',
    severity: 'High',
    secret: secret || 'Qwerty2026!',
    lineNumber: 1,
    fullMatch: `Contraseña: ${secret || 'Qwerty2026!'}`,
    detectedAt: new Date().toISOString()
  };

  try {
    await addDoc(collection(db, 'findings'), {
      ...newFinding,
      detectedAt: serverTimestamp()
    });
  } catch (err) {
    console.log("Mocking simulated finding locally:", newFinding);
  }
  return newFinding;
}
