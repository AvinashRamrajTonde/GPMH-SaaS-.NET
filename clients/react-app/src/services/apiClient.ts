export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  kycStatus?: string;
  agentCode?: string;
  reason?: string;
  helplinePhone?: string;
  helplineEmail?: string;
}

export class ApiProblemError extends Error {
  public readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails) {
    super(problem.detail || problem.title);
    this.name = 'ApiProblemError';
    this.problem = problem;
  }
}

export function normalizeBaseUrl(rawUrl: string): string {
  return rawUrl.replace(/\/+$/, '') + '/';
}

const API_BASE_URL = normalizeBaseUrl(
  import.meta.env.VITE_API_BASE_URL || '/api/v1/',
);

let activeTenantId: string | null = null;
let accessToken: string | null = null;

export function setClientSessionContext(tenantId: string | null, jwt: string | null): void {
  activeTenantId = tenantId;
  accessToken = jwt;
}

function buildAuthHeaders(initHeaders?: HeadersInit, body?: BodyInit | null): Headers {
  const headers = new Headers(initHeaders);
  if (body && !(body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  if (activeTenantId) {
    headers.set('X-Tenant-ID', activeTenantId);
  }
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }
  return headers;
}

export async function apiRequest<T>(
  relativePath: string,
  init: RequestInit = {},
): Promise<T> {
  const cleanPath = relativePath.replace(/^\/+/, '');
  const headers = buildAuthHeaders(init.headers, init.body);
  headers.set('Accept', 'application/json');

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

export async function apiFetchBlobUrl(relativePath: string): Promise<string> {
  const cleanPath = relativePath.replace(/^\/+/, '');
  const headers = buildAuthHeaders();
  const response = await fetch(`${API_BASE_URL}${cleanPath}`, {
    headers,
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Asset fetch failed (${response.status})`);
  }

  const blob = await response.blob();
  return URL.createObjectURL(blob);
}

export async function apiDownloadFile(
  relativePath: string,
  fallbackFilename: string,
): Promise<{ blob: Blob; fileName: string }> {
  const cleanPath = relativePath.replace(/^\/+/, '');
  const headers = buildAuthHeaders();
  headers.set('Accept', 'application/pdf, application/json');

  const response = await fetch(`${API_BASE_URL}${cleanPath}`, {
    headers,
    credentials: 'include',
  });

  if (!response.ok) {
    let problem: ProblemDetails = {
      title: 'Download Failed',
      status: response.status,
      detail: response.statusText,
    };
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON failure
    }
    throw new ApiProblemError(problem);
  }

  const disp = response.headers.get('Content-Disposition') || '';
  const match = /filename="?([^";\n]+)"?/i.exec(disp);
  const fileName = match?.[1]?.trim() || fallbackFilename;
  const blob = await response.blob();
  return { blob, fileName };
}

export function getApiBaseUrl(): string {
  return API_BASE_URL;
}
