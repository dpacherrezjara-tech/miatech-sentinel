import React, { useState } from 'react';
import { Laptop, Search, RefreshCw, ShieldCheck, AlertCircle } from 'lucide-react';

export default function AgentsPage({ agents, findings }) {
  const [search, setSearch] = useState('');

  const filteredAgents = agents.filter(agent => 
    agent.hostname.toLowerCase().includes(search.toLowerCase()) ||
    agent.username.toLowerCase().includes(search.toLowerCase()) ||
    agent.ipAddress.includes(search)
  );

  const getFindingCount = (agentId) => {
    return findings.filter(f => f.agentId === agentId || f.hostname === agentId).length;
  };

  return (
    <div>
      <div className="top-bar">
        <div className="page-title-group">
          <h1>Equipos Monitoreados</h1>
          <p>Estaciones de trabajo registradas y estado en tiempo real del agente Sentinel</p>
        </div>

        <div className="search-wrapper">
          <Search size={16} className="search-icon" />
          <input 
            type="text" 
            placeholder="Buscar por equipo, usuario o IP..." 
            className="search-input"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      <div className="content-section">
        <div className="section-header">
          <div className="section-title">
            <Laptop size={20} color="var(--accent-cyan)" />
            <span>Lista de Agentes Registrados</span>
          </div>
          <span className="badge badge-blue">{filteredAgents.length} Equipos</span>
        </div>

        <div className="custom-table-container">
          <table className="custom-table">
            <thead>
              <tr>
                <th>Estado</th>
                <th>Nombre del Equipo (Host)</th>
                <th>Usuario Windows</th>
                <th>Dirección IP</th>
                <th>Versión Agente</th>
                <th>Último Ping</th>
                <th>Credenciales Halladas</th>
              </tr>
            </thead>
            <tbody>
              {filteredAgents.map((agent, idx) => {
                const count = getFindingCount(agent.hostname);
                return (
                  <tr key={agent.id || idx}>
                    <td>
                      <span className={`badge ${agent.status === 'active' ? 'badge-emerald' : 'badge-inactive'}`}>
                        {agent.status === 'active' ? '● Conectado' : '○ Desconectado'}
                      </span>
                    </td>
                    <td>
                      <strong style={{ color: 'var(--text-primary)', fontSize: '0.95rem' }}>{agent.hostname}</strong>
                    </td>
                    <td>{agent.username}</td>
                    <td>
                      <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.85rem' }}>{agent.ipAddress}</span>
                    </td>
                    <td>
                      <span style={{ background: 'rgba(255,255,255,0.06)', padding: '0.2rem 0.5rem', borderRadius: '4px', fontSize: '0.78rem' }}>
                        v{agent.version}
                      </span>
                    </td>
                    <td>
                      <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                        {new Date(agent.lastPing).toLocaleTimeString()}
                      </span>
                    </td>
                    <td>
                      {count > 0 ? (
                        <span className="badge badge-rose" style={{ gap: '0.3rem' }}>
                          <AlertCircle size={12} /> {count} Expuestas
                        </span>
                      ) : (
                        <span className="badge badge-emerald" style={{ gap: '0.3rem' }}>
                          <ShieldCheck size={12} /> Seguro
                        </span>
                      )}
                    </td>
                  </tr>
                );
              })}
              {filteredAgents.length === 0 && (
                <tr>
                  <td colSpan={7} style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
                    No se encontraron agentes que coincidan con la búsqueda.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
