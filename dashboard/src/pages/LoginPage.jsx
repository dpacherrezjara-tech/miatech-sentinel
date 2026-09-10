import React, { useState } from 'react';
import { ShieldAlert, Lock, User, ArrowRight } from 'lucide-react';

export default function LoginPage({ onLogin }) {
  const [username, setUsername] = useState('admin@miatech.com');
  const [password, setPassword] = useState('admin123');
  const [role, setRole] = useState('Administrator');

  const handleSubmit = (e) => {
    e.preventDefault();
    onLogin({
      username: username.split('@')[0] || 'Admin',
      email: username,
      role: role
    });
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '1.5rem',
      background: 'radial-gradient(circle at 50% 30%, rgba(37, 99, 235, 0.12) 0%, transparent 60%)'
    }}>
      <div style={{
        background: 'var(--bg-secondary)',
        border: '1px solid var(--border-color)',
        borderRadius: 'var(--radius-xl)',
        padding: '2.5rem',
        width: '100%',
        maxWidth: '440px',
        boxShadow: '0 20px 50px rgba(0,0,0,0.6)',
        backdropFilter: 'blur(16px)'
      }}>
        <div style={{ textAlign: 'center', marginBottom: '2rem' }}>
          <div style={{
            width: '54px',
            height: '54px',
            background: 'var(--gradient-blue)',
            borderRadius: 'var(--radius-lg)',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: '0 0 25px rgba(59, 130, 246, 0.4)',
            marginBottom: '1rem'
          }}>
            <ShieldAlert size={30} color="#ffffff" />
          </div>
          <h2 style={{ fontSize: '1.6rem', fontWeight: 800 }}>MIATECH SENTINEL</h2>
          <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
            Panel Empresarial de Monitoreo DLP y Credenciales
          </p>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <div>
            <label style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--text-secondary)', display: 'block', marginBottom: '0.4rem' }}>
              Correo de Usuario
            </label>
            <div className="search-wrapper" style={{ width: '100%' }}>
              <User size={16} className="search-icon" />
              <input 
                type="email" 
                className="search-input" 
                style={{ width: '100%' }}
                value={username}
                onChange={e => setUsername(e.target.value)}
                required
              />
            </div>
          </div>

          <div>
            <label style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--text-secondary)', display: 'block', marginBottom: '0.4rem' }}>
              Contraseña
            </label>
            <div className="search-wrapper" style={{ width: '100%' }}>
              <Lock size={16} className="search-icon" />
              <input 
                type="password" 
                className="search-input" 
                style={{ width: '100%' }}
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
              />
            </div>
          </div>

          <div>
            <label style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--text-secondary)', display: 'block', marginBottom: '0.4rem' }}>
              Rol de Acceso
            </label>
            <select 
              className="search-input" 
              style={{ width: '100%', paddingLeft: '1rem', cursor: 'pointer' }}
              value={role}
              onChange={e => setRole(e.target.value)}
            >
              <option value="Administrator">Administrador (Control Total)</option>
              <option value="Auditor">Auditor (Solo Lectura)</option>
            </select>
          </div>

          <button 
            type="submit" 
            className="btn btn-primary" 
            style={{ width: '100%', justifyContent: 'center', padding: '0.85rem', marginTop: '0.5rem', fontSize: '0.95rem' }}
          >
            <span>Ingresar al Panel</span>
            <ArrowRight size={18} />
          </button>
        </form>
      </div>
    </div>
  );
}
