import React, { useState } from 'react';
import { Application, Environment, AccessLevel } from '../types.js';
import { api } from '../api.js';
import { AlertTriangle, Send, X, ShieldAlert, CheckCircle2 } from 'lucide-react';

interface CreateRequestModalProps {
  applications: Application[];
  onClose: () => void;
  onSuccess: () => void;
}

export const CreateRequestModal: React.FC<CreateRequestModalProps> = ({
  applications,
  onClose,
  onSuccess,
}) => {
  const [clientRequestId] = useState<string>(() => `req-client-${crypto.randomUUID()}`);
  const [applicationId, setApplicationId] = useState<string>(applications[0]?.id || '');
  const [environment, setEnvironment] = useState<Environment>('NonProduction');
  const [accessLevel, setAccessLevel] = useState<AccessLevel>('Read');
  const [justification, setJustification] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Live High Risk calculation
  const isHighRisk = environment === 'Production' || accessLevel === 'Admin';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!justification.trim()) {
      setError('Justification is mandatory');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await api.createRequest({
        clientRequestId,
        applicationId,
        environment,
        accessLevel,
        justification,
      });
      onSuccess();
    } catch (err: any) {
      setError(err.message || 'Failed to submit access request');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-card">
        <div className="modal-header">
          <h3>Create Access Request</h3>
          <button className="modal-close" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            {error && (
              <div className="alert-conflict" style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <AlertTriangle size={18} />
                  <span>{error}</span>
                </div>
              </div>
            )}

            <div className="form-group">
              <label>Target Application</label>
              <select
                value={applicationId}
                onChange={(e) => setApplicationId(e.target.value)}
                required
              >
                {applications.map((app) => (
                  <option key={app.id} value={app.id}>
                    {app.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Environment</label>
                <select
                  value={environment}
                  onChange={(e) => setEnvironment(e.target.value as Environment)}
                >
                  <option value="NonProduction">NonProduction</option>
                  <option value="Production">Production</option>
                </select>
              </div>

              <div className="form-group">
                <label>Access Level</label>
                <select
                  value={accessLevel}
                  onChange={(e) => setAccessLevel(e.target.value as AccessLevel)}
                >
                  <option value="Read">Read</option>
                  <option value="Admin">Admin</option>
                </select>
              </div>
            </div>

            {/* Live High-risk indicator banner */}
            <div
              style={{
                padding: '12px 16px',
                borderRadius: '10px',
                background: isHighRisk ? 'rgba(239, 68, 68, 0.1)' : 'rgba(16, 185, 129, 0.1)',
                border: `1px solid ${isHighRisk ? 'rgba(239, 68, 68, 0.3)' : 'rgba(16, 185, 129, 0.3)'}`,
                display: 'flex',
                alignItems: 'center',
                gap: '12px',
                marginBottom: '18px',
              }}
            >
              {isHighRisk ? (
                <>
                  <ShieldAlert size={20} color="#f87171" />
                  <div style={{ fontSize: '0.85rem', color: '#fca5a5' }}>
                    <strong>High-Risk Request Detected:</strong> Requires 2-stage approval (Manager, followed by System Owner).
                  </div>
                </>
              ) : (
                <>
                  <CheckCircle2 size={20} color="#34d399" />
                  <div style={{ fontSize: '0.85rem', color: '#6ee7b7' }}>
                    <strong>Standard Request:</strong> Requires single Manager approval to become Approved.
                  </div>
                </>
              )}
            </div>

            <div className="form-group">
              <label>Business Justification (Required)</label>
              <textarea
                rows={3}
                placeholder="State the explicit business purpose and requirements for this access..."
                value={justification}
                onChange={(e) => setJustification(e.target.value)}
                required
              />
            </div>

            <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
              Idempotency Key: <code>{clientRequestId}</code> (Policy Version: v1)
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn-outline btn-sm" onClick={onClose} disabled={loading}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={loading}>
              <Send size={16} />
              {loading ? 'Submitting...' : 'Submit Request'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
