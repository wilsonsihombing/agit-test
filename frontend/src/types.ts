export interface User {
  id: string;
  email: string;
  name: string;
  role: 'EMPLOYEE' | 'MANAGER' | 'SYSTEM_OWNER' | 'ADMIN_AUDITOR';
  managerId: string | null;
  createdAt: string;
}

export interface Application {
  id: string;
  name: string;
  systemOwnerId: string;
  createdAt: string;
}

export type Environment = 'NonProduction' | 'Production';
export type AccessLevel = 'Read' | 'Admin';
export type RequestStatus = 'PENDING_MANAGER' | 'PENDING_SYSTEM_OWNER' | 'APPROVED' | 'REJECTED';

export interface AccessRequest {
  id: string;
  clientRequestId: string;
  requesterId: string;
  requesterName: string;
  applicationId: string;
  applicationName: string;
  environment: Environment;
  accessLevel: AccessLevel;
  justification: string;
  isHighRisk: boolean;
  status: RequestStatus;
  policyVersion: string;
  currentAssigneeId: string | null;
  currentAssigneeName: string | null;
  version: number;
  rejectReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AuditEvent {
  id: number;
  requestId: string;
  actorId: string;
  actorName: string;
  action: string;
  fromStatus: string | null;
  toStatus: string;
  reason: string | null;
  requestVersion: number;
  createdAt: string;
}

export interface RequestDetail extends AccessRequest {
  auditTrail: AuditEvent[];
}

export interface ApiError {
  code: string;
  message: string;
}
