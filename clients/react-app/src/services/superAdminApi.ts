import { apiDownloadFile, apiRequest } from './apiClient';
import { triggerBlobDownload } from './printApi';
import type { PlatformPaymentAccountSummary, SuperAdminTenantSummary } from './sprint2Api';

export interface SuperAdminDashboardStats {
  totalVillages: number;
  activeVillages: number;
  pendingActivation: number;
  expiredVillages: number;
  suspendedVillages: number;
  trashCount: number;
  pendingPaymentSubmissions: number;
  expiringIn30Days: number;
  totalCitizens: number;
  totalProperties: number;
  totalTaxDemandedStatewide: number;
  totalTaxCollectedStatewide: number;
  collectionPercentage: number;
  totalPlatformRevenue: number;
}

export interface SuperAdminTenantUser {
  userId: string;
  username: string;
  fullNameEn: string;
  fullNameMr: string;
  phone: string | null;
  email: string | null;
  role: string;
  isActive: boolean;
}

export interface TenantSubscriptionHistoryItem {
  paymentId: string;
  planName: string;
  paymentMode: string;
  amount: number;
  gstAmount: number;
  durationDays: number;
  paymentReference: string | null;
  invoiceNo: string | null;
  paymentDateUtc: string;
  notes: string | null;
}

export interface TenantPaymentSubmission {
  submissionId: string;
  tenantId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  districtName: string;
  submittedByUserId: string;
  submittedByUsername: string;
  planName: string;
  paymentMode: string;
  amount: number;
  utrReference: string;
  paymentDate: string;
  slipAssetKey: string | null;
  status: string;
  rejectionReason: string | null;
  reviewedByUserId: string | null;
  reviewedAtUtc: string | null;
  approvedSubscriptionPaymentId: string | null;
  invoiceNo: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export interface SuperAdminAuditLogItem {
  id: string;
  tenantId: string | null;
  tenantNameEn: string | null;
  lgdCode: string | null;
  entityName: string;
  entityId: string;
  action: string;
  actorId: string | null;
  actorRole: string | null;
  oldValuesJson: string | null;
  newValuesJson: string | null;
  ipAddress: string | null;
  timestampUtc: string;
}

export interface SuperAdminTenantDetail {
  summary: SuperAdminTenantSummary;
  districtCode: string;
  tehsilCode: string;
  pincode: string | null;
  censusCode: string | null;
  officeAddress: string | null;
  sarpanchName: string | null;
  sarpanchMobile: string | null;
  gramsevakName: string | null;
  gramsevakMobile: string | null;
  primaryUpiId: string | null;
  activeAssessmentYear: string | null;
  protectedAssetsCount: number;
  bankAccountsCount: number;
  users: SuperAdminTenantUser[];
  subscriptionPayments: TenantSubscriptionHistoryItem[];
  paymentSubmissions: TenantPaymentSubmission[];
  recentAuditLogs: SuperAdminAuditLogItem[];
}

export interface SupportImpersonationResult {
  accessToken: string;
  expiresAtUtc: string;
  expiresInSeconds: number;
  targetTenantId: string;
  targetTenantCode: string;
  targetLgdCode: string;
  targetTenantNameEn: string;
  targetTenantNameMr: string;
  readOnly: boolean;
  reason: string;
}

export interface ResetTenantAdminPasswordResult {
  tenantId: string;
  username: string;
  resetAtUtc: string;
}

export interface PlatformPaymentAccount {
  id: string;
  accountName: string;
  bankName: string;
  branchName: string;
  accountNumber: string;
  ifscCode: string;
  upiId: string;
  annualFee: number;
  gstPercent: number;
  gstin: string | null;
  supportPhone: string | null;
  supportEmail: string | null;
  isActive: boolean;
  updatedAtUtc: string;
}

export interface GpSubscriptionStatus {
  tenantId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  status: string;
  effectiveStatus: string;
  subscriptionExpiresAtUtc: string | null;
  daysUntilExpiry: number | null;
  showExpiryWarning30Days: boolean;
  platformPaymentAccount: PlatformPaymentAccountSummary;
  recentSubmissions: TenantPaymentSubmission[];
  paymentHistory: TenantSubscriptionHistoryItem[];
}

export type TenantStatusAction = 'Activate' | 'Suspend' | 'GrantTrial' | 'DirectRenew';

export interface UpdateTenantStatusPayload {
  action: TenantStatusAction;
  trialDays?: number;
  durationDays?: number;
  reason?: string;
}

export interface UpdatePlatformSettingsPayload {
  accountName: string;
  bankName: string;
  branchName: string;
  accountNumber: string;
  ifscCode: string;
  upiId: string;
  annualFee: number;
  gstPercent: number;
  gstin: string | null;
  supportPhone: string | null;
  supportEmail: string | null;
}

export interface SubmitSubscriptionPaymentPayload {
  planName: string;
  paymentMode: string;
  amount: number;
  utrReference: string;
  paymentDate: string;
  slipAssetKey?: string | null;
  notes?: string | null;
}

export interface ListTenantsParams {
  search?: string;
  district?: string;
  taluka?: string;
  status?: string;
  includeDeleted?: boolean;
}

export interface ListPaymentsParams {
  status?: string;
  tenantId?: string;
  search?: string;
}

export interface ListAuditLogsParams {
  tenantId?: string;
  action?: string;
  entityName?: string;
  fromDate?: string;
  toDate?: string;
  search?: string;
  limit?: number;
}

function buildQuery(entries: Record<string, string | number | boolean | undefined | null>): string {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(entries)) {
    if (v === undefined || v === null || v === '') continue;
    qs.set(k, String(v));
  }
  const str = qs.toString();
  return str ? `?${str}` : '';
}

export const superAdminApi = {
  getStats: () => apiRequest<SuperAdminDashboardStats>('superadmin/stats'),

  listTenants: (params: ListTenantsParams = {}) =>
    apiRequest<SuperAdminTenantSummary[]>(
      `superadmin/tenants${buildQuery({
        search: params.search,
        district: params.district,
        taluka: params.taluka,
        status: params.status,
        includeDeleted: params.includeDeleted ? true : undefined,
      })}`,
    ),

  getTenantDetail: (id: string) =>
    apiRequest<SuperAdminTenantDetail>(`superadmin/tenants/${encodeURIComponent(id)}`),

  impersonateTenant: (id: string, reason: string) =>
    apiRequest<SupportImpersonationResult>(`superadmin/tenants/${encodeURIComponent(id)}/impersonate`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  exitImpersonation: (targetTenantId?: string | null) =>
    apiRequest<void>('superadmin/impersonate/exit', {
      method: 'POST',
      body: JSON.stringify(targetTenantId ? { targetTenantId } : {}),
    }),

  updateTenantStatus: (id: string, payload: UpdateTenantStatusPayload) =>
    apiRequest<SuperAdminTenantSummary>(`superadmin/tenants/${encodeURIComponent(id)}/status`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  resetAdminPassword: (id: string, newPassword: string, targetUsername?: string) =>
    apiRequest<ResetTenantAdminPasswordResult>(
      `superadmin/tenants/${encodeURIComponent(id)}/reset-admin-password`,
      {
        method: 'POST',
        body: JSON.stringify({
          newPassword,
          ...(targetUsername?.trim() ? { targetUsername: targetUsername.trim() } : {}),
        }),
      },
    ),

  softDeleteTenant: (id: string) =>
    apiRequest<void>(`superadmin/tenants/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    }),

  restoreTenant: (id: string) =>
    apiRequest<SuperAdminTenantSummary>(`superadmin/tenants/${encodeURIComponent(id)}/restore`, {
      method: 'POST',
    }),

  permanentDeleteTenant: (id: string, confirmLgdCode: string) =>
    apiRequest<void>(
      `superadmin/tenants/${encodeURIComponent(id)}/permanent${buildQuery({ confirmLgdCode })}`,
      {
        method: 'DELETE',
        body: JSON.stringify({ confirmLgdCode }),
      },
    ),

  listPayments: (params: ListPaymentsParams = {}) =>
    apiRequest<TenantPaymentSubmission[]>(
      `superadmin/payments${buildQuery({
        status: params.status,
        tenantId: params.tenantId,
        search: params.search,
      })}`,
    ),

  approvePayment: (id: string, durationDays = 365, notes?: string) =>
    apiRequest<TenantPaymentSubmission>(`superadmin/payments/${encodeURIComponent(id)}/approve`, {
      method: 'POST',
      body: JSON.stringify({ durationDays, ...(notes?.trim() ? { notes: notes.trim() } : {}) }),
    }),

  rejectPayment: (id: string, reason: string) =>
    apiRequest<TenantPaymentSubmission>(`superadmin/payments/${encodeURIComponent(id)}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  async downloadInvoicePdf(id: string): Promise<string> {
    const { blob, fileName } = await apiDownloadFile(
      `superadmin/payments/${encodeURIComponent(id)}/invoice.pdf`,
      `GPMH-Invoice-${id.slice(0, 8)}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },

  listAuditLogs: (params: ListAuditLogsParams = {}) =>
    apiRequest<SuperAdminAuditLogItem[]>(
      `superadmin/audit-logs${buildQuery({
        tenantId: params.tenantId,
        action: params.action,
        entityName: params.entityName,
        fromDate: params.fromDate,
        toDate: params.toDate,
        search: params.search,
        limit: params.limit ?? 100,
      })}`,
    ),

  async exportAuditLogsCsv(params: ListAuditLogsParams = {}): Promise<string> {
    const qs = buildQuery({
      tenantId: params.tenantId,
      action: params.action,
      entityName: params.entityName,
      fromDate: params.fromDate,
      toDate: params.toDate,
      search: params.search,
    });
    const { blob, fileName } = await apiDownloadFile(
      `superadmin/audit-logs/export.csv${qs}`,
      `GPMH-AuditLogs-${new Date().toISOString().slice(0, 10)}.csv`,
      'text/csv, application/json',
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },

  getPlatformSettings: () => apiRequest<PlatformPaymentAccount>('superadmin/platform-settings'),

  updatePlatformSettings: (payload: UpdatePlatformSettingsPayload) =>
    apiRequest<PlatformPaymentAccount>('superadmin/platform-settings', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  async downloadProtectedAsset(assetKey: string, fallbackFilename: string): Promise<string> {
    const { blob, fileName } = await apiDownloadFile(
      `onboarding/assets/${encodeURIComponent(assetKey)}`,
      fallbackFilename,
      '*/*',
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },
};

export const gpSubscriptionApi = {
  getSubscriptionStatus: () => apiRequest<GpSubscriptionStatus>('onboarding/subscription-status'),

  submitSubscriptionPayment: (payload: SubmitSubscriptionPaymentPayload) =>
    apiRequest<TenantPaymentSubmission>('onboarding/subscription-payments', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  async downloadGpInvoicePdf(id: string): Promise<string> {
    const { blob, fileName } = await apiDownloadFile(
      `onboarding/subscription-payments/${encodeURIComponent(id)}/invoice.pdf`,
      `GPMH-Invoice-${id.slice(0, 8)}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
    return fileName;
  },
};

