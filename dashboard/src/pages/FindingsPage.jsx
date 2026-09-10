import React, { useState } from 'react';
import { KeyRound, Search, ExternalLink, Filter, ShieldAlert } from 'lucide-react';

export default function FindingsPage({ findings, onSelectFinding }) {
  const [search, setSearch] = useState('');
  const [severityFilter, setSeverityFilter] = useState('ALL');

  const filteredFindings = findings.filter(f => {
    const matchesSearch = 
      f.hostname.toLowerCase().includes(search.toLowerCase()) ||
      f.username.toLowerCase().includes(search.toLowerCase()) ||
      f.filePath.toLowerCase().includes(search.toLowerCase()) ||
      f.ruleId.toLowerCase().includes(search.toLowerCase()) ||
      f.secret.toLowerCase().includes(search.toLowerCase());

    const matchesSeverity = severityFilter === 'ALL' || f.severity === severityFilter;

    return matchesSearch && matchesSeverity;
  });

  return (
    <div>
      <div className="top-bar">
        <div className="page-title-group">
          <h1>Credenciales Detectadas</h1>
          <p>Registro centralizado de secretos, tokens, llaves y contraseñas expuestas</p>
        </div>

        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <div className="search-wrapper">
            <Search size={16} className="search-icon" />
            <input 
              type="text" 
              placeholder="Buscar archivo, regla o secreto..." 
              className="search-input"
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>
        </div>
      </div>

      <div className="content-section">
        <div className="section-header">
          <div className="section-title">
            <KeyRound size={20} color="var(--accent-rose)" />
            <span>Hallazgos de Credenciales ({filteredFindings.length})</span>
          </div>

          <div style={{ display: 'flex', gap: '0.5rem' }}>
            {['ALL', 'High', 'Medium', 'Low'].map(sev => (
              <button
                key={sev}
                className={`btn text-xs ${severityFilter === sev ? 'btn-primary' : 'btn-secondary'}`}
                onClick={() => setSeverityFilter(sev)}
                style={{ padding: '0.35rem 0.75rem' }}
              >
                {sev === 'ALL' ? 'Todos' : sev}
              </button>
            ))}
          </div>
        </div>

        <div className="custom-table-container">
          <table className="custom-table">
            <thead>
              <tr>
                <th>Severidad</th>
                <th>Regla</th>
                <th>Equipo / Usuario</th>
                <th>Archivo Exposición</th>
                <th>Línea</th>
                <th>Fragmento Secreto</th>
                <th>Fecha Detección</th>
                <th>Ver</th>
              </tr>
            </thead>
            <tbody>
              {filteredFindings.map((finding, idx) => (
                <tr key={finding.id || idx}>
                  <td>
                    <span className={`badge badge-${finding.severity?.toLowerCase() || 'high'}`}>
                      {finding.severity || 'High'}
                    </span>
                  </td>
                  <td>
                    <strong style={{ color: 'var(--accent-cyan)', fontSize: '0.85rem' }}>{finding.ruleId}</strong>
                  </td>
                  <td>
                    <div style={{ fontWeight: 700 }}>{finding.hostname}</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{finding.username}</div>
                  </td>
                  <td>
                    <span className="file-path">{finding.filePath}</span>
                  </td>
                  <td>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8rem' }}>L{finding.lineNumber || 1}</span>
                  </td>
                  <td>
                    <span className="secret-box">{finding.secret}</span>
                  </td>
                  <td>
                    <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>
                      {new Date(finding.detectedAt).toLocaleString()}
                    </span>
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
              {filteredFindings.length === 0 && (
                <tr>
                  <td colSpan={8} style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
                    No se encontraron credenciales que coincidan con los filtros aplicados.
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
