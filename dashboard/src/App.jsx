import React, { useState, useEffect } from 'react';
import Sidebar from './components/Sidebar';
import ModalDetail from './components/ModalDetail';
import OverviewPage from './pages/OverviewPage';
import AgentsPage from './pages/AgentsPage';
import FindingsPage from './pages/FindingsPage';
import EventsPage from './pages/EventsPage';
import LoginPage from './pages/LoginPage';
import { subscribeToAgents, subscribeToFindings, subscribeToEvents } from './services/firebaseService';

export default function App() {
  const [currentUser, setCurrentUser] = useState(() => {
    const saved = localStorage.getItem('sentinel_user');
    return saved ? JSON.parse(saved) : { username: 'dpacherrez', email: 'dpacherrez@miatech.com', role: 'Administrator' };
  });

  const [activeTab, setActiveTab] = useState('overview');
  const [agents, setAgents] = useState([]);
  const [findings, setFindings] = useState([]);
  const [events, setEvents] = useState([]);
  const [selectedFinding, setSelectedFinding] = useState(null);

  useEffect(() => {
    const unsubAgents = subscribeToAgents(setAgents);
    const unsubFindings = subscribeToFindings(setFindings);
    const unsubEvents = subscribeToEvents(setEvents);

    return () => {
      unsubAgents();
      unsubFindings();
      unsubEvents();
    };
  }, []);

  const handleLogin = (user) => {
    setCurrentUser(user);
    localStorage.setItem('sentinel_user', JSON.stringify(user));
  };

  const handleLogout = () => {
    setCurrentUser(null);
    localStorage.removeItem('sentinel_user');
  };

  if (!currentUser) {
    return <LoginPage onLogin={handleLogin} />;
  }

  return (
    <div className="app-container">
      <Sidebar 
        activeTab={activeTab} 
        setActiveTab={setActiveTab} 
        currentUser={currentUser} 
        onLogout={handleLogout} 
      />

      <main className="main-content">
        {activeTab === 'overview' && (
          <OverviewPage 
            agents={agents} 
            findings={findings} 
            events={events} 
            onSelectFinding={setSelectedFinding} 
          />
        )}

        {activeTab === 'agents' && (
          <AgentsPage 
            agents={agents} 
            findings={findings} 
          />
        )}

        {activeTab === 'findings' && (
          <FindingsPage 
            findings={findings} 
            onSelectFinding={setSelectedFinding} 
          />
        )}

        {activeTab === 'events' && (
          <EventsPage 
            events={events} 
          />
        )}
      </main>

      <ModalDetail 
        finding={selectedFinding} 
        onClose={() => setSelectedFinding(null)} 
      />
    </div>
  );
}
