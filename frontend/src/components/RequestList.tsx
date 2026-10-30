import React from 'react';
import { AccessRequest } from '../types.js';
import { ShieldAlert, Eye } from 'lucide-react';

interface RequestListProps {
  requests: AccessRequest[];
  onViewDetail: (id: string) => void;
  emptyMessage?: string;
}

export const RequestList: React.FC<RequestListProps> = ({
  requests,
  onViewDetail,
  emptyMessage = 'No access requests found.',
}) => {
  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'PENDING_MANAGER':
        return <span className="badge-status badge-pending_manager">Pending Manager</span>;
      case 'PENDING_SYSTEM_OWNER':
        return <span className="badge-status badge-pending_system_owner">Pending System Owner</span>;
      case 'APPROVED':
        return <span className="badge-status badge-approved">Approved</span>;
      case 'REJECTED':
        return <span className="badge-status badge-rejected">Rejected</span>;
      default:
        return <span>{status}</span>;
    }
  };

  if (requests.length === 0) {
    return (
      <div
        style={{
          background: 'var(--bg-card)',
          border: '1px solid var(--border-color)',
          borderRadius: '16px',
          padding: '48px 24px',
          textAlign: 'center',
          color: 'var(--text-muted)',
        }}
      >
        <h3>No Requests Found</h3>
        <p style={{ marginTop: 8, fontSize: '0.9rem' }}>{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="card-table-wrapper">
      <table className="table-custom">
        <thead>
          <tr>
            <th>Application</th>
            <th>Requester</th>
            <th>Environment & Access</th>
            <th>Risk Level</th>
            <th>Status</th>
            <th>Current Assignee</th>
            <th style={{ textAlign: 'right' }}>Details</th>
          </tr>
        </thead>
        <tbody>
          {requests.map((req) => (
            <tr key={req.id}>
              <td>
                <div style={{ fontWeight: 600 }}>{req.applicationName}</div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                  {new Date(req.createdAt).toLocaleDateString()}
                </div>
              </td>
              <td>{req.requesterName}</td>
              <td>
                <span className="badge-env">{req.environment}</span> • {req.accessLevel}
              </td>
              <td>
                {req.isHighRisk ? (
                  <span className="badge-high-risk">
                    <ShieldAlert size={12} /> HIGH RISK
                  </span>
                ) : (
                  <span style={{ fontSize: '0.8rem', color: '#34d399', fontWeight: 600 }}>Standard</span>
                )}
              </td>
              <td>{getStatusBadge(req.status)}</td>
              <td>{req.currentAssigneeName || <span style={{ color: '#64748b' }}>None (Terminal)</span>}</td>
              <td style={{ textAlign: 'right' }}>
                <button
                  className="btn-outline btn-sm"
                  onClick={() => onViewDetail(req.id)}
                >
                  <Eye size={14} /> View
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
