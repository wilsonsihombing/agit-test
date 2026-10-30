import React, { useState } from 'react';
import { AccessRequest } from '../types.js';
import { api } from '../api.js';
import { Check, X, ShieldAlert, AlertTriangle, RefreshCw, Eye } from 'lucide-react';

interface ApprovalInboxProps {
  requests: AccessRequest[];
  onRefresh: () => void;
  onViewDetail: (id: string) => void;
}

export const ApprovalInbox: React.FC<ApprovalInboxProps> = ({
  requests,
  onRefresh,
  onViewDetail,
}) => {
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState<string>('');
  const [actionLoading, setActionLoading] = useState<boolean>(false);
  const [conflictError, setConflictError] = useState<string | null>(null);

  const handleApprove = async (req: AccessRequest) => {
    setActionLoading(true);
    setConflictError(null);
    try {
      await api.approveRequest(req.id, req.version);
      onRefresh();
    } catch (err: any) {
      if (err.status === 409 || err.code === 'CONFLICT') {
        setConflictError(
          `Conflict detected! Another approver modified Request (${req.applicationName}) concurrently. Please refresh.`
        );
      } else {
        alert(err.message || 'Approval failed');
      }
    } finally {
      setActionLoading(false);
    }
  };

  const handleRejectSubmit = async (req: AccessRequest) => {
    if (!rejectReason.trim()) {
      alert('Reject reason is required!');
      return;
    }

    setActionLoading(true);
    setConflictError(null);
    try {
      await api.rejectRequest(req.id, req.version, rejectReason);
      setRejectingId(null);
      setRejectReason('');
      onRefresh();
    } catch (err: any) {
      if (err.status === 409 || err.code === 'CONFLICT') {
        setConflictError(
          `Conflict detected! Another approver modified Request (${req.applicationName}) concurrently. Please refresh.`
        );
      } else {
        alert(err.message || 'Rejection failed');
      }
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div>
      {conflictError && (
        <div className="alert-conflict">
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <AlertTriangle size={20} />
            <span>{conflictError}</span>
          </div>
          <button className="btn-outline btn-sm" onClick={onRefresh} style={{ borderColor: 'rgba(239, 68, 68, 0.4)' }}>
            <RefreshCw size={14} /> Refresh Data
          </button>
        </div>
      )}

      {requests.length === 0 ? (
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
          <h3>Your Approval Inbox is Clean!</h3>
          <p style={{ marginTop: 8, fontSize: '0.9rem' }}>
            There are currently no access requests pending your review.
          </p>
        </div>
      ) : (
        <div className="card-table-wrapper">
          <table className="table-custom">
            <thead>
              <tr>
                <th>Application</th>
                <th>Requester</th>
                <th>Target & Level</th>
                <th>Risk Level</th>
                <th>Version</th>
                <th>Justification</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
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
                  <td>
                    <code style={{ color: '#818cf8' }}>v{req.version}</code>
                  </td>
                  <td style={{ maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {req.justification}
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: 8 }}>
                      <button
                        className="btn-outline btn-sm"
                        onClick={() => onViewDetail(req.id)}
                        title="View Details"
                      >
                        <Eye size={14} />
                      </button>
                      <button
                        className="btn-approve btn-sm"
                        disabled={actionLoading}
                        onClick={() => handleApprove(req)}
                        title="Approve Request"
                      >
                        <Check size={14} /> Approve
                      </button>
                      <button
                        className="btn-reject btn-sm"
                        disabled={actionLoading}
                        onClick={() => {
                          setRejectingId(req.id);
                          setRejectReason('');
                        }}
                        title="Reject Request"
                      >
                        <X size={14} /> Reject
                      </button>
                    </div>

                    {rejectingId === req.id && (
                      <div
                        style={{
                          marginTop: 12,
                          background: 'rgba(26, 35, 51, 0.95)',
                          padding: 12,
                          borderRadius: 10,
                          border: '1px solid var(--border-color)',
                          textAlign: 'left',
                        }}
                      >
                        <label style={{ fontSize: '0.8rem', fontWeight: 600, color: '#f87171' }}>
                          Reason for Rejection (Required):
                        </label>
                        <textarea
                          rows={2}
                          placeholder="Provide the reason for rejecting..."
                          value={rejectReason}
                          onChange={(e) => setRejectReason(e.target.value)}
                          style={{ marginTop: 6, marginBottom: 8 }}
                        />
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 6 }}>
                          <button
                            type="button"
                            className="btn-outline btn-sm"
                            onClick={() => setRejectingId(null)}
                          >
                            Cancel
                          </button>
                          <button
                            type="button"
                            className="btn-reject btn-sm"
                            onClick={() => handleRejectSubmit(req)}
                          >
                            Confirm Reject
                          </button>
                        </div>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
