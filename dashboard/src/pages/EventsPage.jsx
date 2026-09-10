import React, { useState } from 'react';
import { History, Search, Activity, ShieldAlert, AlertTriangle, Info } from 'lucide-react';

export default function EventsPage({ events }) {
  const [search, setSearch] = useState('');

  const filteredEvents = events.filter(evt =>
    evt.hostname.toLowerCase().includes(search.toLowerCase()) ||
    evt.eventType.toLowerCase().includes(search.toLowerCase()) ||
    evt.description.toLowerCase().includes(search.toLowerCase())
  );

  const getSeverityBadge = (severity) => {
    switch(severity) {
      case 'ALERT': return 'badge-rose';
      case 'WARNING': return 'badge-amber';
      case 'INFO': return 'badge-blue';
      default: return 'badge-blue';
    }
  };

  return (
    <div>
      <div className="top-bar">
        <div className="page-title-group">
          <h1>Historial de Eventos de Auditoría</h1>
          <p>Registro cronológico de aperturas, cierres, alertas e intentos de manipulación</p>
        </div>

        <div className="search-wrapper">
          <Search size={16} className="search-icon" />
          <input 
            type="text" 
            placeholder="Buscar por evento o descripción..." 
            className="search-input"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      <div className="content-section">
        <div className="section-header">
          <div className="section-title">
            <History size={20} color="var(--accent-blue)" />
            <span>Eventos de Seguridad Monitoreados ({filteredEvents.length})</span>
          </div>
        </div>

        <div className="custom-table-container">
          <table className="custom-table">
            <thead>
              <tr>
                <th>Nivel</th>
                <th>Tipo de Evento</th>
                <th>Equipo Host</th>
                <th>Descripción / Mensaje</th>
                <th>Fecha y Hora</th>
              </tr>
            </thead>
            <tbody>
              {filteredEvents.map((evt, idx) => (
                <tr key={evt.id || idx}>
                  <td>
                    <span className={`badge ${getSeverityBadge(evt.severity)}`}>
                      {evt.severity}
                    </span>
                  </td>
                  <td>
                    <strong style={{ color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontSize: '0.85rem' }}>
                      {evt.eventType}
                    </strong>
                  </td>
                  <td>
                    <strong style={{ color: 'var(--accent-cyan)' }}>{evt.hostname}</strong>
                  </td>
                  <td>{evt.description}</td>
                  <td>
                    <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                      {new Date(evt.createdAt || evt.timestamp || Date.now()).toLocaleString()}
                    </span>
                  </td>
                </tr>
              ))}
              {filteredEvents.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
                    No se encontraron eventos registrados.
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
