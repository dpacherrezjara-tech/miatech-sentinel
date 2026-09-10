import React, { useState } from 'react';
import StatCard from '../components/StatCard';
import { 
  Monitor, 
  ShieldAlert, 
  KeyRound, 
  Activity, 
  Plus, 
  ExternalLink, 
  Clock,
  Laptop
} from 'lucide-react';
import { injectSimulatedFinding } from '../services/firebaseService';

export default function OverviewPage({ agents, findings, events, onSelectFinding }) {
  const [isSimulating, setIsSimulating] = useState(false);

  const activeAgentsCount = agents.filter(a => a.status === 'active').length;
  const criticalFindingsCount = findings.filter(f => f.severity === 'High').length;
  const totalFindingsCount = findings.length;

  const handleSimulate = async () => {
    setIsSimulating(true);
    await injectSimulatedFinding('LAP-DPACHERREZ', 'D:\\sentinel_test\\passwords.txt', 'contrasena-colon', 'SuperSecret2026!');
    setTimeout(() => setIsSimulating(false), 800);
  };

  return (
    <div>
      {/* Top Header */}
      <div className="top-bar">
        <div className="page-title-group">
          <h1>Panel de Control Principal</h1>
          <p>Visión general del estado de seguridad y credenciales en estaciones de trabajo</p>
        </div>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <button 
            className="btn btn-primary" 
            onClick={handleSimulate} 
            disabled={isSimulating}
            style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}
          >
            <Plus size={16} />
            <span>{isSimulating ? 'Simulando...' : 'Simular Alerta de Prueba'}</span>
          </button>

          <div className="system-status-indicator">
            <div className="status-dot active"></div>
            <span>Monitoreo Activo</span>
          </div>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="stats-grid">
        <StatCard 
          title="Equipos Activos" 
          value={`${activeAgentsCount} / ${agents.length}`} 
          subtitle="Agentes conectados enviando ping" 
          icon={Monitor} 
          colorTheme="emerald" 
        />
        <StatCard 
          title="Credenciales Halladas" 
          value={totalFindingsCount} 
          subtitle="En archivos de texto / config" 
          icon={KeyRound} 
          colorTheme="rose" 
        />
        <StatCard 
          title="Alertas Críticas (High)" 
          value={criticalFindingsCount} 
          subtitle="Requieren acción o eliminación" 
          icon={ShieldAlert} 
          colorTheme="amber" 
        />
        <StatCard 
          title="Eventos Recientes" 
          value={events.length} 
          subtitle="Auditoría del sistema" 
          icon={Activity} 
          colorTheme="blue" 
        />
      </div>

      {/* Content Columns Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '1.5rem' }}>
        {/* Left Column: Recent Findings */}
        <div className="content-section">
          <div className="section-header">
            <div className="section-title">
              <KeyRound size={20} color="var(--accent-rose)" />
              <span>Últimas Credenciales Detectadas</span>
            </div>
            <span className="badge badge-rose">{findings.length} Hallazgos</span>
          </div>

          <div className="custom-table-container">
            <table className="custom-table">
              <thead>
                <tr>
                  <th>Severidad</th>
                  <th>Equipo / Usuario</th>
                  <th>Archivo</th>
                  <th>Regla</th>
                  <th>Secreto</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                {findings.slice(0, 6).map((finding, idx) => (
                  <tr key={finding.id || idx}>
                    <td>
                      <span className={`badge badge-${finding.severity?.toLowerCase() || 'high'}`}>
                        {finding.severity || 'High'}
                      </span>
                    </td>
                    <td>
                      <div style={{ fontWeight: 700, color: 'var(--text-primary)' }}>{finding.hostname}</div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{finding.username}</div>
                    </td>
                    <td>
                      <span className="file-path">{finding.filePath}</span>
                    </td>
                    <td>
                      <span style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--accent-cyan)' }}>
                        {finding.ruleId}
                      </span>
                    </td>
                    <td>
                      <span className="secret-box">{finding.secret}</span>
                    </td>
                    <td>
                      <button 
                        className="btn btn-secondary text-xs" 
                        onClick={() => onSelectFinding(finding)}
                        style={{ padding: '0.3rem 0.6rem' }}
                      >
                        <ExternalLink size={14} />
                      </button>
                    </td>
                  </tr>
                ))}
                {findings.length === 0 && (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
                      No se han detectado credenciales expuestas en los equipos.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right Column: Active Agents Summary & Events */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          {/* Active Agents Widget */}
          <div className="content-section" style={{ marginBottom: 0 }}>
            <div className="section-header">
              <div className="section-title">
                <Laptop size={18} color="var(--accent-emerald)" />
                <span>Estado de Equipos</span>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {agents.map((agent, idx) => (
                <div 
                  key={agent.id || idx} 
                  style={{ 
                    display: 'flex', 
                    justifyContent: 'space-between', 
                    alignItems: 'center',
                    padding: '0.75rem 0.9rem',
                    background: 'rgba(0,0,0,0.25)',
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--border-color)'
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                    <div className={`status-dot ${agent.status === 'active' ? 'active' : 'warning'}`}></div>
                    <div>
                      <div style={{ fontSize: '0.88rem', fontWeight: 700 }}>{agent.hostname}</div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>IP: {agent.ipAddress}</div>
                    </div>
                  </div>
                  <span className={`badge ${agent.status === 'active' ? 'badge-emerald' : 'badge-inactive'}`}>
                    {agent.status}
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* Audit Events Widget */}
          <div className="content-section">
            <div className="section-header">
              <div className="section-title">
                <Clock size={18} color="var(--accent-blue)" />
                <span>Eventos de Sistema</span>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>
              {events.slice(0, 4).map((evt, idx) => (
                <div 
                  key={evt.id || idx}
                  style={{
                    fontSize: '0.82rem',
                    padding: '0.6rem 0.75rem',
                    borderLeft: `3px solid ${evt.severity === 'ALERT' ? 'var(--accent-rose)' : evt.severity === 'WARNING' ? 'var(--accent-amber)' : 'var(--accent-blue)'}`,
                    background: 'rgba(0,0,0,0.2)',
                    borderRadius: '0 var(--radius-sm) var(--radius-sm) 0'
                  }}
                >
                  <div style={{ fontWeight: 600, color: 'var(--text-primary)', display: 'flex', justifyContent: 'space-between' }}>
                    <span>{evt.hostname}</span>
                    <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>{evt.eventType}</span>
                  </div>
                  <div style={{ color: 'var(--text-secondary)', marginTop: '0.2rem' }}>{evt.description}</div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
