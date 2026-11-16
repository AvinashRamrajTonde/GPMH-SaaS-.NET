import { normalizeBaseUrl } from './lgdApi';

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export class ApiProblemError extends Error {
  public readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails) {
    super(problem.detail || problem.title);
    this.name = 'ApiProblemError';
    this.problem = problem;
  }
}

const API_BASE_URL = normalizeBaseUrl(
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1/',
);

let activeTenantId: string | null = null;
let accessToken: string | null = null;

export function setClientSessionContext(tenantId: string | null, jwt: string | null): void {
  activeTenantId = tenantId;
  accessToken = jwt;
}

export async function apiRequest<T>(
  relativePath: string,
  init: RequestInit = {},
): Promise<T> {
  const cleanPath = relativePath.replace(/^\/+/, '');
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');

  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  if (activeTenantId) {
    headers.set('X-Tenant-ID', activeTenantId);
  }
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  const response = await fetch(`${API_BASE_URL}${cleanPath}`, {
    ...init,
    headers,
    credentials: 'include',
  });

  if (!response.ok) {
    let problem: ProblemDetails = {
      title: 'Request Failed',
      status: response.status,
      detail: response.statusText,
    };
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON upstream failure
    }
    throw new ApiProblemError(problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

