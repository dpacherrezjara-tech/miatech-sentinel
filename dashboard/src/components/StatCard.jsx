import React from 'react';

export default function StatCard({ title, value, subtitle, icon: Icon, colorTheme = 'blue' }) {
  return (
    <div className="stat-card">
      <div className="stat-card-header">
        <span className="stat-card-title">{title}</span>
        <div className={`stat-card-icon ${colorTheme}`}>
          <Icon size={20} />
        </div>
      </div>
      <div className="stat-card-value">{value}</div>
      {subtitle && <div className="stat-card-subtitle">{subtitle}</div>}
    </div>
  );
}
