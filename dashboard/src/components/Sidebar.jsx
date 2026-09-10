import React from 'react';
import { 
  ShieldAlert, 
  LayoutDashboard, 
  Monitor, 
  KeyRound, 
  History, 
  LogOut, 
  Sparkles 
} from 'lucide-react';

export default function Sidebar({ activeTab, setActiveTab, currentUser, onLogout }) {
  const menuItems = [
    { id: 'overview', label: 'Panel Principal', icon: LayoutDashboard },
    { id: 'agents', label: 'Equipos Monitoreados', icon: Monitor },
    { id: 'findings', label: 'Credenciales Halladas', icon: KeyRound },
    { id: 'events', label: 'Historial de Eventos', icon: History }
  ];

  return (
    <aside className="sidebar">
      <div className="brand-header">
        <div className="brand-logo">
          <ShieldAlert size={24} color="#ffffff" />
        </div>
        <div>
          <div className="brand-title">MIATECH</div>
          <div className="brand-subtitle">SENTINEL DLP</div>
        </div>
      </div>

      <ul className="nav-list">
        {menuItems.map(item => {
          const Icon = item.icon;
          const isActive = activeTab === item.id;
          return (
            <li key={item.id} className={`nav-item ${isActive ? 'active' : ''}`}>
              <button onClick={() => setActiveTab(item.id)}>
                <Icon size={18} />
                <span>{item.label}</span>
              </button>
            </li>
          );
        })}
      </ul>

      <div className="sidebar-footer">
        <div className="user-badge mb-2">
          <div className="user-avatar">
            {currentUser?.username ? currentUser.username[0].toUpperCase() : 'A'}
          </div>
          <div className="user-info">
            <span className="user-name">{currentUser?.username || 'Admin Usuario'}</span>
            <span className="user-role">{currentUser?.role || 'Administrator'}</span>
          </div>
        </div>
        <button 
          className="btn btn-secondary w-full justify-center text-xs mt-2" 
          onClick={onLogout}
          style={{ width: '100%', justifyContent: 'center' }}
        >
          <LogOut size={14} />
          <span>Cerrar Sesión</span>
        </button>
      </div>
    </aside>
  );
}
