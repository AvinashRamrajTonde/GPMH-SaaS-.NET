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

interface LgdEnvelope<T> {
  success: boolean;
  execution_time_ms: number;
  data?: T;
  error?: {
    code: string;
    message: string;
  };
}

export function normalizeBaseUrl(rawUrl: string): string {
  return rawUrl.replace(/\/+$/, '') + '/';
}

const LGD_BASE_URL = normalizeBaseUrl(
  import.meta.env.VITE_LGD_API_BASE_URL || 'https://api.grampanchayatmh.in/api/v1/',
);

async function fetchLgd<T>(relativePath: string): Promise<T> {
  const cleanPath = relativePath.replace(/^\/+/, '');
  const response = await fetch(`${LGD_BASE_URL}${cleanPath}`, {
    headers: { Accept: 'application/json' },
  });

  const payload = (await response.json()) as LgdEnvelope<T>;
  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error?.message || `LGD API HTTP ${response.status}`);
  }
  return payload.data;
}

export async function getDistricts(): Promise<LgdDistrict[]> {
  const data = await fetchLgd<{ districts: LgdDistrict[] }>('districts');
  return data.districts;
}

export async function getTehsils(district: string): Promise<LgdTehsil[]> {
  const data = await fetchLgd<{ tehsils: LgdTehsil[] }>(
    `tehsils?district=${encodeURIComponent(district)}`,
  );
  return data.tehsils;
}

export async function getPanchayats(
  district: string,
  tehsil: string,
): Promise<LgdPanchayatSummary[]> {
  const data = await fetchLgd<{ panchayats: LgdPanchayatSummary[] }>(
    `panchayats?district=${encodeURIComponent(district)}&tehsil=${encodeURIComponent(tehsil)}`,
  );
  return data.panchayats;
}

export async function getPanchayatByLgd(lgdCode: string): Promise<LgdPanchayatDetail> {
  return fetchLgd<LgdPanchayatDetail>(`panchayat/${encodeURIComponent(lgdCode.trim())}`);
}

