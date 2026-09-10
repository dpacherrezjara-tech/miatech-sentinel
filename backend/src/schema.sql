-- Miatech Sentinel - PostgreSQL Database Schema
-- Version: 1.0.0

-- 1. Tabla de Equipos Monitoreados (agents)
CREATE TABLE IF NOT EXISTS agents (
    id VARCHAR(100) PRIMARY KEY,
    hostname VARCHAR(100) NOT NULL UNIQUE,
    username VARCHAR(100) NOT NULL,
    ip_address VARCHAR(45) NOT NULL,
    version VARCHAR(20) DEFAULT '1.0.0',
    status VARCHAR(20) DEFAULT 'active',
    last_ping TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. Tabla de Credenciales Detectadas (findings)
CREATE TABLE IF NOT EXISTS findings (
    id VARCHAR(100) PRIMARY KEY,
    agent_id VARCHAR(100) REFERENCES agents(id) ON DELETE CASCADE,
    hostname VARCHAR(100) NOT NULL,
    username VARCHAR(100) NOT NULL,
    file_path TEXT NOT NULL,
    rule_id VARCHAR(50) NOT NULL,
    description TEXT,
    severity VARCHAR(20) NOT NULL DEFAULT 'High',
    secret TEXT NOT NULL,
    line_number INT DEFAULT 1,
    full_match TEXT,
    detected_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. Tabla de Historial de Eventos (events)
CREATE TABLE IF NOT EXISTS events (
    id VARCHAR(100) PRIMARY KEY,
    agent_id VARCHAR(100),
    hostname VARCHAR(100) NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL DEFAULT 'INFO',
    description TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. Tabla de Usuarios del Dashboard (users)
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    role VARCHAR(20) DEFAULT 'Administrator',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Indices para consultas rápidas
CREATE INDEX IF NOT EXISTS idx_findings_severity ON findings(severity);
CREATE INDEX IF NOT EXISTS idx_findings_detected_at ON findings(detected_at DESC);
CREATE INDEX IF NOT EXISTS idx_events_created_at ON events(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_agents_status ON agents(status);
