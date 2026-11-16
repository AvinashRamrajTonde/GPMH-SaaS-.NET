import { normalizeBaseUrl } from './apiClient';

export interface LgdDistrict {
  district: string;
  district_code: string | null;
  gp_count: number;
}

export interface LgdTehsil {
  tehsil: string;
  tehsil_code: string | null;
  gp_count: number;
}

export interface LgdPanchayatSummary {
  lgd_code: string;
  gp_name_en: string;
  gp_name_mr: string;
  district: string;
  tehsil: string;
  pincode: string | null;
}

export interface LgdPanchayatDetail extends LgdPanchayatSummary {
  district_code: string | null;
  tehsil_code: string | null;
}

export interface LgdStats {
  state: string;
  state_code: number;
  total_districts: number;
  total_tehsils: number;
  total_panchayats: number;
  api_version: string;
}

interface LgdEnvelope<T> {
  success: boolean;
  execution_time_ms?: number;
  data?: T;
  error?: {
    code: string;
    message: string;
  };
}

export { normalizeBaseUrl };

const PRIMARY_LGD_URL = normalizeBaseUrl(
  import.meta.env.VITE_LGD_API_BASE_URL || '/api/v1/lgd/',
);
const BACKEND_PROXY_LGD_URL = normalizeBaseUrl(
  `${import.meta.env.VITE_API_BASE_URL || '/api/v1/'}lgd/`,
);

function unwrapEnvelope<T>(payload: unknown, nestedKey?: string): T {
  if (payload && typeof payload === 'object' && 'success' in payload) {
    const env = payload as LgdEnvelope<Record<string, unknown> | T>;
    if (!env.success || !env.data) {
      throw new Error(env.error?.message || 'LGD lookup failed');
    }
    if (
      nestedKey &&
      typeof env.data === 'object' &&
      env.data !== null &&
      nestedKey in env.data
    ) {
      return (env.data as Record<string, unknown>)[nestedKey] as T;
    }
    return env.data as T;
  }
  return payload as T;
}

async function requestLgdJson<T>(
  baseUrl: string,
  relativePath: string,
  nestedKey?: string,
): Promise<T> {
  const cleanPath = relativePath.replace(/^\/+/, '');
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  try {
    const response = await fetch(`${baseUrl}${cleanPath}`, {
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });
    const payload: unknown = await response.json();
    if (!response.ok) {
      const errObj = payload as { detail?: string; error?: { message?: string } };
      throw new Error(errObj.detail || errObj.error?.message || `LGD API HTTP ${response.status}`);
    }
    return unwrapEnvelope<T>(payload, nestedKey);
  } finally {
    clearTimeout(timeout);
  }
}

async function fetchLgd<T>(relativePath: string, nestedKey?: string): Promise<T> {
  try {
    return await requestLgdJson<T>(PRIMARY_LGD_URL, relativePath, nestedKey);
  } catch (primaryErr) {
    if (PRIMARY_LGD_URL !== BACKEND_PROXY_LGD_URL) {
      try {
        return await requestLgdJson<T>(BACKEND_PROXY_LGD_URL, relativePath, nestedKey);
      } catch {
        throw primaryErr instanceof Error ? primaryErr : new Error('LGD directory unavailable');
      }
    }
    throw primaryErr instanceof Error ? primaryErr : new Error('LGD directory unavailable');
  }
}

export async function getDistricts(): Promise<LgdDistrict[]> {
  return fetchLgd<LgdDistrict[]>('districts', 'districts');
}

export async function getTehsils(district: string): Promise<LgdTehsil[]> {
  return fetchLgd<LgdTehsil[]>(
    `tehsils?district=${encodeURIComponent(district)}`,
    'tehsils',
  );
}

export async function getPanchayats(
  district: string,
  tehsil: string,
): Promise<LgdPanchayatSummary[]> {
  return fetchLgd<LgdPanchayatSummary[]>(
    `panchayats?district=${encodeURIComponent(district)}&tehsil=${encodeURIComponent(tehsil)}`,
    'panchayats',
  );
}

export async function getPanchayatByLgd(lgdCode: string): Promise<LgdPanchayatDetail> {
  return fetchLgd<LgdPanchayatDetail>(`panchayat/${encodeURIComponent(lgdCode.trim())}`);
}

export async function searchPanchayats(
  query: string,
  limit = 15,
): Promise<LgdPanchayatSummary[]> {
  return fetchLgd<LgdPanchayatSummary[]>(
    `search?q=${encodeURIComponent(query.trim())}&limit=${limit}`,
    'results',
  );
}

export async function getLgdStats(): Promise<LgdStats> {
  return fetchLgd<LgdStats>('stats');
}
