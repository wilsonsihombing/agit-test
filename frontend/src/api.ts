import { User, Application, AccessRequest, RequestDetail, ApiError } from './types.js';

class ApiClient {
  private currentUserId: string = 'usr_alice';

  public setCurrentUserId(userId: string) {
    this.currentUserId = userId;
  }

  public getCurrentUserId(): string {
    return this.currentUserId;
  }

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const headers = new Headers(options.headers || {});
    headers.set('Content-Type', 'application/json');
    if (this.currentUserId) {
      headers.set('x-user-id', this.currentUserId);
    }

    const response = await fetch(endpoint, {
      ...options,
      headers,
    });

    const data = await response.json();

    if (!response.ok) {
      const error: ApiError = data?.error || {
        code: 'UNKNOWN_ERROR',
        message: response.statusText || 'Request failed',
      };
      const customError: any = new Error(error.message);
      customError.code = error.code;
      customError.status = response.status;
      throw customError;
    }

    return data;
  }

  public async getUsers(): Promise<User[]> {
    const res = await this.request<{ data: User[] }>('/api/users');
    return res.data;
  }

  public async getApplications(): Promise<Application[]> {
    const res = await this.request<{ data: Application[] }>('/api/applications');
    return res.data;
  }

  public async getRequests(scope?: string): Promise<AccessRequest[]> {
    const url = scope ? `/api/requests?scope=${scope}` : '/api/requests';
    const res = await this.request<{ data: AccessRequest[] }>(url);
    return res.data;
  }

  public async getInbox(): Promise<AccessRequest[]> {
    const res = await this.request<{ data: AccessRequest[] }>('/api/inbox');
    return res.data;
  }

  public async getRequestDetail(id: string): Promise<RequestDetail> {
    const res = await this.request<{ data: RequestDetail }>(`/api/requests/${id}`);
    return res.data;
  }

  public async createRequest(payload: {
    clientRequestId: string;
    applicationId: string;
    environment: string;
    accessLevel: string;
    justification: string;
  }): Promise<{ data: AccessRequest; isDuplicate: boolean }> {
    return await this.request<{ data: AccessRequest; isDuplicate: boolean }>('/api/requests', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  }

  public async approveRequest(id: string, expectedVersion: number): Promise<AccessRequest> {
    const res = await this.request<{ data: AccessRequest }>(`/api/requests/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ expectedVersion }),
    });
    return res.data;
  }

  public async rejectRequest(id: string, expectedVersion: number, reason: string): Promise<AccessRequest> {
    const res = await this.request<{ data: AccessRequest }>(`/api/requests/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ expectedVersion, reason }),
    });
    return res.data;
  }
}

export const api = new ApiClient();
