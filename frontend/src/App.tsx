import React, { useEffect, useState, useCallback } from 'react';
import { User, Application, AccessRequest } from './types.js';
import { api } from './api.js';
import { Navbar } from './components/Navbar.js';
import { RequestList } from './components/RequestList.js';
import { ApprovalInbox } from './components/ApprovalInbox.js';
import { CreateRequestModal } from './components/CreateRequestModal.js';
import { RequestDetailModal } from './components/RequestDetailModal.js';
import { Plus, RefreshCw, Inbox, FileText, Database } from 'lucide-react';

export const App: React.FC = () => {
  const [users, setUsers] = useState<User[]>([]);
  const [currentUser, setCurrentUser] = useState<User | null>(null);
  const [applications, setApplications] = useState<Application[]>([]);
  const [activeTab, setActiveTab] = useState<'my-requests' | 'inbox' | 'all-requests'>('my-requests');

  const [myRequests, setMyRequests] = useState<AccessRequest[]>([]);
  const [inboxRequests, setInboxRequests] = useState<AccessRequest[]>([]);
  const [allRequests, setAllRequests] = useState<AccessRequest[]>([]);

  const [loading, setLoading] = useState<boolean>(true);
  const [isCreateOpen, setIsCreateOpen] = useState<boolean>(false);
  const [selectedDetailId, setSelectedDetailId] = useState<string | null>(null);

  // Load initial data
  useEffect(() => {
    const initApp = async () => {
      try {
        const [fetchedUsers, fetchedApps] = await Promise.all([
          api.getUsers(),
          api.getApplications(),
        ]);
        setUsers(fetchedUsers);
        setApplications(fetchedApps);

        // Default active user is Alice (Requester)
        const defaultUser = fetchedUsers.find((u) => u.id === 'usr_alice') || fetchedUsers[0] || null;
        if (defaultUser) {
          setCurrentUser(defaultUser);
          api.setCurrentUserId(defaultUser.id);
        }
      } catch (err) {
        console.error('Failed to initialize app', err);
      } finally {
        setLoading(false);
      }
    };

    initApp();
  }, []);

  // Fetch requests based on current user
  const refreshData = useCallback(async () => {
    if (!currentUser) return;

    try {
      const [mine, inbox, all] = await Promise.all([
        api.getRequests(),
        api.getInbox(),
        api.getRequests('all'),
      ]);
      setMyRequests(mine);
      setInboxRequests(inbox);
      setAllRequests(all);
    } catch (err) {
      console.error('Failed to fetch requests', err);
    }
  }, [currentUser]);

  useEffect(() => {
    if (currentUser) {
      refreshData();
    }
  }, [currentUser, refreshData]);

  const handleUserChange = (newUser: User) => {
    setCurrentUser(newUser);
    api.setCurrentUserId(newUser.id);
    // Switch to inbox tab automatically if user is a manager or system owner with pending approvals
    if (newUser.role === 'MANAGER' || newUser.role === 'SYSTEM_OWNER') {
      setActiveTab('inbox');
    } else if (newUser.role === 'ADMIN_AUDITOR') {
      setActiveTab('all-requests');
    } else {
      setActiveTab('my-requests');
    }
  };

  return (
    <div>
      <Navbar
        users={users}
        currentUser={currentUser}
        onUserChange={handleUserChange}
      />

      <main className="main-content">
        <div className="action-bar">
          <div className="nav-tabs">
            <button
              className={`tab-btn ${activeTab === 'my-requests' ? 'active' : ''}`}
              onClick={() => setActiveTab('my-requests')}
            >
              <FileText size={16} />
              My Requests
              {myRequests.length > 0 && <span className="tab-badge">{myRequests.length}</span>}
            </button>

            <button
              className={`tab-btn ${activeTab === 'inbox' ? 'active' : ''}`}
              onClick={() => setActiveTab('inbox')}
            >
              <Inbox size={16} />
              Approval Inbox
              {inboxRequests.length > 0 && (
                <span className="tab-badge" style={{ background: '#f59e0b', color: '#000' }}>
                  {inboxRequests.length}
                </span>
              )}
            </button>

            <button
              className={`tab-btn ${activeTab === 'all-requests' ? 'active' : ''}`}
              onClick={() => setActiveTab('all-requests')}
            >
              <Database size={16} />
              All Requests (Audit View)
              {allRequests.length > 0 && <span className="tab-badge">{allRequests.length}</span>}
            </button>
          </div>

          <div style={{ display: 'flex', gap: 10 }}>
            <button
              className="btn-outline btn-sm"
              onClick={refreshData}
              title="Refresh data from server"
            >
              <RefreshCw size={15} /> Refresh
            </button>
            <button
              className="btn-primary"
              onClick={() => setIsCreateOpen(true)}
            >
              <Plus size={18} /> New Request
            </button>
          </div>
        </div>

        {/* Tab Content */}
        {loading ? (
          <p style={{ color: 'var(--text-muted)', textAlign: 'center', padding: 40 }}>
            Connecting to ASP.NET Core API server...
          </p>
        ) : (
          <>
            {activeTab === 'my-requests' && (
              <RequestList
                requests={myRequests}
                onViewDetail={(id) => setSelectedDetailId(id)}
                emptyMessage={`No requests submitted yet by ${currentUser?.name}. Click 'New Request' above to create one.`}
              />
            )}

            {activeTab === 'inbox' && (
              <ApprovalInbox
                requests={inboxRequests}
                onRefresh={refreshData}
                onViewDetail={(id) => setSelectedDetailId(id)}
              />
            )}

            {activeTab === 'all-requests' && (
              <RequestList
                requests={allRequests}
                onViewDetail={(id) => setSelectedDetailId(id)}
                emptyMessage="No requests recorded across the organization yet."
              />
            )}
          </>
        )}
      </main>

      {/* Modals */}
      {isCreateOpen && (
        <CreateRequestModal
          applications={applications}
          onClose={() => setIsCreateOpen(false)}
          onSuccess={() => {
            setIsCreateOpen(false);
            refreshData();
          }}
        />
      )}

      {selectedDetailId && (
        <RequestDetailModal
          requestId={selectedDetailId}
          onClose={() => setSelectedDetailId(null)}
        />
      )}
    </div>
  );
};
export default App;
