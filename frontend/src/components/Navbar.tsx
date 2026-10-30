import React from 'react';
import { User } from '../types.js';
import { ShieldCheck, UserCheck } from 'lucide-react';

interface NavbarProps {
  users: User[];
  currentUser: User | null;
  onUserChange: (user: User) => void;
}

export const Navbar: React.FC<NavbarProps> = ({ users, currentUser, onUserChange }) => {
  return (
    <header className="navbar">
      <div className="brand-section">
        <div className="brand-logo">
          <ShieldCheck size={22} />
        </div>
        <div>
          <h1 className="brand-title">Access Request Hub</h1>
          <p className="brand-subtitle">Enterprise Approval & Audit Single Source of Truth</p>
        </div>
      </div>

      <div className="user-switcher-bar">
        <UserCheck size={18} color="#94a3b8" />
        <span className="switcher-label">Active User:</span>
        <select
          id="user-switcher-select"
          className="switcher-select"
          value={currentUser?.id || ''}
          onChange={(e) => {
            const selected = users.find((u) => u.id === e.target.value);
            if (selected) onUserChange(selected);
          }}
        >
          {users.map((u) => (
            <option key={u.id} value={u.id}>
              {u.name} ({u.email})
            </option>
          ))}
        </select>

        {currentUser && (
          <span className={`role-badge role-${currentUser.role.toLowerCase()}`}>
            {currentUser.role.replace('_', ' ')}
          </span>
        )}
      </div>
    </header>
  );
};
