import React from 'react';
import { X, ShieldAlert, FileText, User, Laptop, Calendar, AlertTriangle } from 'lucide-react';

export default function ModalDetail({ finding, onClose }) {
  if (!finding) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
            <ShieldAlert size={22} className="text-rose-500" color="#f43f5e" />
            <h3 style={{ fontSize: '1.2rem', fontWeight: 800 }}>Detalle del Hallazgo</h3>
          </div>
          <button className="modal-close" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className={`badge badge-${finding.severity?.toLowerCase() || 'high'}`}>
              Severidad: {finding.severity || 'High'}
            </span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              Regla: <strong>{finding.ruleId}</strong>
            </span>
          </div>

          <div style={{ background: 'rgba(0,0,0,0.3)', padding: '1rem', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)' }}>
            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '0.4rem', fontWeight: 700 }}>
              Descripción de la Regla
            </div>
            <div style={{ fontSize: '0.95rem', fontWeight: 600, color: 'var(--text-primary)' }}>
              {finding.description || 'Credential Exposure Detected'}
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
              <Laptop size={18} color="var(--accent-blue)" />
              <div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Equipo / Estación</div>
                <div style={{ fontSize: '0.88rem', fontWeight: 700 }}>{finding.hostname}</div>
              </div>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
              <User size={18} color="var(--accent-cyan)" />
              <div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Usuario Windows</div>
                <div style={{ fontSize: '0.88rem', fontWeight: 700 }}>{finding.username}</div>
              </div>
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.3rem', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              <FileText size={14} /> Archivo Afectado
            </div>
            <div className="file-path" style={{ padding: '0.6rem', background: 'rgba(0,0,0,0.4)', borderRadius: 'var(--radius-sm)' }}>
              {finding.filePath}
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.3rem', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              <AlertTriangle size={14} color="var(--accent-amber)" /> Secreto / Fragmento Coincidente (Línea {finding.lineNumber || 1})
            </div>
            <div className="secret-box" style={{ padding: '0.75rem', fontSize: '0.9rem' }}>
              {finding.fullMatch || finding.secret}
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingTop: '0.75rem', borderTop: '1px solid var(--border-color)', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              <Calendar size={14} /> Detectado: {new Date(finding.detectedAt).toLocaleString()}
            </span>
            <button className="btn btn-secondary text-xs" onClick={onClose}>Cerrar</button>
          </div>
        </div>
      </div>
    </div>
  );
}
