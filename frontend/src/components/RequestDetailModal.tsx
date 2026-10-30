import React, { useEffect, useState } from 'react';
import { RequestDetail } from '../types.js';
import { api } from '../api.js';
import { X, Clock, ShieldAlert, CheckCircle, XCircle, ArrowRight } from 'lucide-react';

interface RequestDetailModalProps {
  requestId: string;
  onClose: () => void;
}

export const RequestDetailModal: React.FC<RequestDetailModalProps> = ({ requestId, onClose }) => {
  const [detail, setDetail] = useState<RequestDetail | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchDetail = async () => {
      try {
        const res = await api.getRequestDetail(requestId);
        setDetail(res);
      } catch (err: any) {
        setError(err.message || 'Failed to fetch details');
      } finally {
        setLoading(false);
      }
    };
    fetchDetail();
  }, [requestId]);

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

  return (
    <div className="modal-overlay">
      <div className="modal-card">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <h3>Request Details</h3>
            {detail && getStatusBadge(detail.status)}
          </div>
          <button className="modal-close" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <div className="modal-body">
          {loading && <p style={{ color: '#94a3b8' }}>Loading request details & audit trail...</p>}
          {error && <p style={{ color: '#f87171' }}>{error}</p>}

          {detail && (
            <>
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                  gap: 16,
                  background: 'rgba(26, 35, 51, 0.4)',
                  padding: 16,
                  borderRadius: 12,
                  border: '1px solid var(--border-color)',
                  marginBottom: 20,
                }}
              >
                <div>
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Application</span>
                  <p style={{ fontWeight: 600 }}>{detail.applicationName}</p>
                </div>
                <div>
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Environment & Level</span>
                  <p style={{ fontWeight: 600 }}>
                    <span className="badge-env">{detail.environment}</span> • {detail.accessLevel}
                  </p>
                </div>
                <div>
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Requester</span>
                  <p style={{ fontWeight: 600 }}>{detail.requesterName}</p>
                </div>
                <div>
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Current Assignee</span>
                  <p style={{ fontWeight: 600 }}>{detail.currentAssigneeName || 'None (Terminal)'}</p>
                </div>
                <div>
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Version Token</span>
                  <p style={{ fontWeight: 600, color: '#818cf8' }}>v{detail.version} (Policy: {detail.policyVersion})</p>
                </div>
                <div>
                  <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Risk Assessment</span>
                  <p>
                    {detail.isHighRisk ? (
                      <span className="badge-high-risk">
                        <ShieldAlert size={12} /> HIGH RISK
                      </span>
                    ) : (
                      <span style={{ fontSize: '0.8rem', color: '#34d399', fontWeight: 600 }}>Standard</span>
                    )}
                  </p>
                </div>
              </div>

              <div style={{ marginBottom: 20 }}>
                <span style={{ fontSize: '0.8rem', color: '#94a3b8', fontWeight: 600 }}>Business Justification</span>
                <p style={{ marginTop: 4, background: 'rgba(18, 24, 38, 0.6)', padding: 12, borderRadius: 8, fontSize: '0.9rem' }}>
                  {detail.justification}
                </p>
              </div>

              {detail.rejectReason && (
                <div style={{ marginBottom: 20, background: 'rgba(244, 63, 94, 0.1)', border: '1px solid rgba(244, 63, 94, 0.3)', padding: 12, borderRadius: 8 }}>
                  <span style={{ fontSize: '0.8rem', color: '#f87171', fontWeight: 600 }}>Rejection Reason:</span>
                  <p style={{ marginTop: 4, color: '#fca5a5', fontSize: '0.9rem' }}>{detail.rejectReason}</p>
                </div>
              )}

              <div>
                <h4 style={{ fontSize: '0.95rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Clock size={16} /> Audit Trail & Timeline (Append-Only)
                </h4>

                <div className="timeline">
                  {detail.auditTrail.map((event) => (
                    <div key={event.id} className="timeline-item">
                      <div className="timeline-dot" />
                      <div className="timeline-content">
                        <div className="timeline-time">
                          {new Date(event.createdAt).toLocaleString()} • Version Token: {event.requestVersion}
                        </div>
                        <div className="timeline-title">
                          <span>{event.actorName}</span>
                          <span style={{ color: '#818cf8' }}>({event.action})</span>
                        </div>
                        <div className="timeline-desc">
                          Status Transition: <strong>{event.fromStatus || 'INIT'}</strong>{' '}
                          <ArrowRight size={12} style={{ display: 'inline', margin: '0 4px' }} />{' '}
                          <strong>{event.toStatus}</strong>
                          {event.reason && (
                            <p style={{ marginTop: 4, color: '#fb7185', fontStyle: 'italic' }}>
                              Reason: "{event.reason}"
                            </p>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}
        </div>

        <div className="modal-footer">
          <button className="btn-outline btn-sm" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
