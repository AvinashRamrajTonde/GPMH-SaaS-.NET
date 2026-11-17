import { apiDownloadFile, apiRequest } from './apiClient';
import { triggerBlobDownload } from './printApi';
import {
  type CanonicalKycStatus,
  type CompleteOnboardingPayload,
  normalizeKycStatus,
  type OnboardingStatus,
  type UploadedAssetResponse,
} from './sprint2Api';

export interface ReferralValidationDto {
  valid: boolean;
  agentCode: string;
  fullName: string | null;
  district: string | null;
}

export interface AgentFullProfile {
  agentId: string;
  userId: string;
  agentCode: string;
  kycStatus: CanonicalKycStatus;
  statusReason: string | null;
  fullName: string;
  mobileNo: string;
  email: string;
  district: string;
  talukasCsv: string;
  aadhaarMasked: string;
  panMasked: string;
  requestedCommissionRatePct: number;
  approvedNewGpRatePct: number;
  approvedRenewalRatePct: number;
  aadhaarDocumentKey: string | null;
  panDocumentKey: string | null;
  address: string;
  villageOrCity: string;
  state: string;
  bankName: string | null;
  bankBranch: string | null;
  accountNumber: string | null;
  ifscCode: string | null;
  upiId: string | null;
  createdAtUtc: string;
  approvedAtUtc: string | null;
}

export interface AgentCommissionItem {
  commissionId: string;
  agentId: string;
  agentCode: string | null;
  agentName: string | null;
  tenantId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  subscriptionPaymentId: string | null;
  commissionType: string;
  collectedAmount: number;
  ratePctSnapshot: number;
  commissionAmount: number;
  settledAmount: number;
  remainingPayable: number;
  status: string;
  latestPayoutId: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export interface AgentPayoutItem {
  payoutId: string;
  agentId: string;
  agentCode: string | null;
  agentName: string | null;
  voucherNo: string;
  voucherSequence: number;
  financialYear: string;
  payoutAmount: number;
  paymentMode: string;
  utrReference: string;
  paymentDate: string;
  status: string;
  receiptAssetKey: string | null;
  remarks: string | null;
  requestedAtUtc: string | null;
  paidAtUtc: string | null;
  createdAtUtc: string;
}

export interface AgentDashboardData {
  agentCode: string;
  fullName: string;
  district: string;
  referralLink: string;
  approvedNewGpRatePct: number;
  approvedRenewalRatePct: number;
  villagesOnboarded: number;
  activeGps: number;
  pendingVerificationGps: number;
  totalSalesValue: number;
  totalCommissionEarned: number;
  pendingPayableCommission: number;
  paidCommission: number;
  upcomingRenewals90dCount: number;
  recentCommissions: AgentCommissionItem[];
}

export interface AgentVillageItem {
  tenantId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  districtName: string;
  tehsilName: string;
  onboardedAtUtc: string;
  subscriptionStatus: string;
  effectiveStatus: string;
  subscriptionExpiresAtUtc: string | null;
  daysUntilExpiry: number | null;
  totalSalesCollected: number;
  totalCommissionEarned: number;
  projectedRenewalCommission: number;
}

export interface AgentVillagesResponse {
  villages: AgentVillageItem[];
  upcomingRenewals: AgentVillageItem[];
}

export interface SuperAdminAgentSummary extends AgentFullProfile {
  onboardedGpCount: number;
  activeGpCount: number;
  totalAccruedCommission: number;
  totalSettledCommission: number;
  pendingPayableBalance: number;
  pendingWithdrawalRequestsCount: number;
}

export interface SuperAdminAgentSettlementsData {
  totalAgents: number;
  pendingKycCount: number;
  activeAgentsCount: number;
  totalDisbursed: number;
  pendingSettlementAmount: number;
  agentPayableSummaries: SuperAdminAgentSummary[];
  unsettledCommissions: AgentCommissionItem[];
  pendingWithdrawalRequests: AgentPayoutItem[];
  disbursedHistory: AgentPayoutItem[];
}

export interface RequestAgentPayoutPayload {
  payoutAmount: number;
  paymentMode: string;
  remarks?: string | null;
}

export interface UpdateAgentProfilePayload {
  email: string;
  address: string;
  villageOrCity: string;
  district: string;
  talukasCsv: string;
  bankName?: string | null;
  bankBranch?: string | null;
  accountNumber?: string | null;
  ifscCode?: string | null;
  upiId?: string | null;
}

export interface ChangeAgentPasswordPayload {
  currentPassword: string;
  newPassword: string;
}

export interface RegisterAgentGpPayload {
  adminUsername: string;
  adminPassword: string;
  adminEmail: string | null;
  wizard: CompleteOnboardingPayload;
}

export interface ListSuperAdminAgentsParams {
  district?: string;
  taluka?: string;
  status?: string;
  search?: string;
}

export interface CreateAgentManualPayload {
  fullName: string;
  mobileNo: string;
  email: string;
  password: string;
  address: string;
  villageOrCity: string;
  district: string;
  talukasCsv: string;
  idProofType: string;
  aadhaarNumber: string;
  panNumber: string;
  bankName?: string | null;
  bankBranch?: string | null;
  accountNumber?: string | null;
  ifscCode?: string | null;
  upiId?: string | null;
  approvedNewGpRatePct: number;
  approvedRenewalRatePct: number;
  autoApprove: boolean;
}

export interface ReviewAgentKycPayload {
  status: number;
  approvedNewGpRatePct?: number;
  approvedRenewalRatePct?: number;
  reason?: string;
}

export interface SettleAgentPayoutPayload {
  amount: number;
  paymentMode: string;
  utrReference: string;
  paymentDate: string;
  fulfillRequestedPayoutId?: string | null;
  receiptAssetKey?: string | null;
  remarks?: string | null;
}

interface RawAgentFullProfile extends Omit<AgentFullProfile, 'kycStatus'> {
  kycStatus: unknown;
}

interface RawSuperAdminAgentSummary extends Omit<SuperAdminAgentSummary, 'kycStatus'> {
  kycStatus: unknown;
}

interface RawSuperAdminAgentSettlementsData
  extends Omit<SuperAdminAgentSettlementsData, 'agentPayableSummaries'> {
  agentPayableSummaries: RawSuperAdminAgentSummary[];
}

function mapAgentFullProfile(raw: RawAgentFullProfile): AgentFullProfile {
  return {
    ...raw,
    kycStatus: normalizeKycStatus(raw.kycStatus),
  };
}

function mapSuperAdminAgentSummary(raw: RawSuperAdminAgentSummary): SuperAdminAgentSummary {
  return {
    ...raw,
    kycStatus: normalizeKycStatus(raw.kycStatus),
  };
}

function buildQuery(params: Record<string, string | undefined>): string {
  const sp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v && v.trim() && v !== 'ALL') {
      sp.set(k, v.trim());
    }
  }
  const str = sp.toString();
  return str ? `?${str}` : '';
}

export const agentPortalApi = {
  validateReferralCode: (agentCode: string) =>
    apiRequest<ReferralValidationDto>(
      `agents/validate-referral/${encodeURIComponent(agentCode.trim())}`,
    ),

  getProfile: async (): Promise<AgentFullProfile> => {
    const raw = await apiRequest<RawAgentFullProfile>('agents/me');
    return mapAgentFullProfile(raw);
  },

  getDashboard: () => apiRequest<AgentDashboardData>('agents/dashboard'),

  getVillages: () => apiRequest<AgentVillagesResponse>('agents/villages'),

  listCommissions: (params: { status?: string; search?: string } = {}) =>
    apiRequest<AgentCommissionItem[]>(
      `agents/commissions${buildQuery({
        status: params.status,
        search: params.search,
      })}`,
    ),

  listPayouts: () => apiRequest<AgentPayoutItem[]>('agents/payouts'),

  requestPayout: (payload: RequestAgentPayoutPayload) =>
    apiRequest<AgentPayoutItem>('agents/payouts/request', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  assistedRegisterGp: (payload: RegisterAgentGpPayload) =>
    apiRequest<OnboardingStatus>('agents/register-gp', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateProfile: async (payload: UpdateAgentProfilePayload): Promise<AgentFullProfile> => {
    const raw = await apiRequest<RawAgentFullProfile>('agents/profile', {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
    return mapAgentFullProfile(raw);
  },

  changePassword: (payload: ChangeAgentPasswordPayload) =>
    apiRequest<void>('agents/change-password', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  async downloadVoucherPdf(payoutId: string, voucherNo?: string): Promise<string> {
    const { blob, fileName } = await apiDownloadFile(
      `agents/payouts/${encodeURIComponent(payoutId)}/voucher.pdf`,
      `${voucherNo || 'GPMH-Voucher'}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },

  async downloadProtectedAsset(assetKey: string, fallbackFilename: string): Promise<string> {
    const { blob, fileName } = await apiDownloadFile(
      `assets/${encodeURIComponent(assetKey)}`,
      fallbackFilename,
      '*/*',
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },
};

export const superAdminAgentsApi = {
  listAgents: async (params: ListSuperAdminAgentsParams = {}): Promise<SuperAdminAgentSummary[]> => {
    const rows = await apiRequest<RawSuperAdminAgentSummary[]>(
      `superadmin/agents${buildQuery({
        district: params.district,
        taluka: params.taluka,
        status: params.status,
        search: params.search,
      })}`,
    );
    return rows.map(mapSuperAdminAgentSummary);
  },

  createAgentManual: async (payload: CreateAgentManualPayload): Promise<SuperAdminAgentSummary> => {
    const raw = await apiRequest<RawSuperAdminAgentSummary>('superadmin/agents', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return mapSuperAdminAgentSummary(raw);
  },

  reviewAgentKyc: async (
    agentId: string,
    payload: ReviewAgentKycPayload,
  ): Promise<AgentFullProfile> => {
    const raw = await apiRequest<RawAgentFullProfile>(
      `superadmin/agents/${encodeURIComponent(agentId)}/kyc`,
      {
        method: 'PUT',
        body: JSON.stringify(payload),
      },
    );
    return mapAgentFullProfile(raw);
  },

  getSettlements: async (): Promise<SuperAdminAgentSettlementsData> => {
    const raw = await apiRequest<RawSuperAdminAgentSettlementsData>('superadmin/agents/settlements');
    return {
      ...raw,
      agentPayableSummaries: raw.agentPayableSummaries.map(mapSuperAdminAgentSummary),
    };
  },

  settleAgentPayout: (agentId: string, payload: SettleAgentPayoutPayload) =>
    apiRequest<AgentPayoutItem>(`superadmin/agents/${encodeURIComponent(agentId)}/settle-payout`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  uploadPayoutReceipt: (file: File) => {
    const form = new FormData();
    form.append('category', 'PAYOUT_RECEIPT');
    form.append('file', file);
    return apiRequest<UploadedAssetResponse>('assets/upload', {
      method: 'POST',
      body: form,
    });
  },

  async downloadVoucherPdf(payoutId: string, voucherNo?: string): Promise<string> {
    const { blob, fileName } = await apiDownloadFile(
      `superadmin/agents/payouts/${encodeURIComponent(payoutId)}/voucher.pdf`,
      `${voucherNo || 'GPMH-Voucher'}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },
};

