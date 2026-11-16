import { apiRequest } from './apiClient';

export type CanonicalRole =
  | 'SuperAdmin'
  | 'Gramsevak'
  | 'GP_Admin'
  | 'GP_Operator'
  | 'Surveyor'
  | 'Citizen'
  | 'Auditor'
  | 'Support_Admin'
  | 'Field_Agent';

export interface TenantMembershipSummary {
  tenantId: string;
  role: CanonicalRole;
  isDefault: boolean;
}

export interface AuthenticatedUser {
  userId: string;
  username: string;
  fullNameEn: string;
  fullNameMr: string;
  role: CanonicalRole;
  activeTenantId: string | null;
  memberships: TenantMembershipSummary[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
  refreshToken?: string | null;
}

export interface LgdProvenance {
  isAutofilledFromLgd: boolean;
  isEditedOnOnboarding: boolean;
  editedFields: string[];
  originalLgdSnapshotJson: string | null;
}

export interface BankAccountPayload {
  fundType: string;
  bankName: string;
  branchName: string;
  accountNumber: string;
  ifscCode: string;
  upiId: string | null;
  upiPayeeName: string | null;
  qrImageKey?: string | null;
  openingBalance: number;
}

export interface OnboardingStatus {
  tenantId: string;
  tenantStatus: string;
  setupProgressPct: number;
  lgdCode: string;
  districtCode: string;
  districtName: string;
  tehsilCode: string;
  tehsilName: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  pincode: string | null;
  censusCode: string | null;
  officeAddress: string | null;
  totalWards: number;
  populationCensus: number | null;
  committee: {
    sarpanchName: string | null;
    sarpanchMobile: string | null;
    upSarpanchName: string | null;
    gramsevakName: string | null;
    gramsevakMobile: string | null;
    operatorName: string | null;
    operatorMobile: string | null;
    gramsevakSignatureKey: string | null;
    sarpanchSignatureKey: string | null;
    operatorSignatureKey: string | null;
    officialStampKey: string | null;
  };
  assessmentYear: {
    yearLabel: string;
    startYear: number;
    endYear: number;
    isLocked: boolean;
    isArrearsRolloverInitialized: boolean;
  };
  bankAccounts: BankAccountPayload[];
  formulaSeeded: boolean;
  provenance: LgdProvenance | null;
}

export interface CompleteOnboardingPayload {
  lgdCode: string;
  districtCode: string;
  districtName: string;
  tehsilCode: string;
  tehsilName: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  pincode: string | null;
  censusCode: string | null;
  officeAddress: string | null;
  totalWards: number;
  populationCensus: number | null;
  sarpanchName: string | null;
  sarpanchMobile: string | null;
  upSarpanchName: string | null;
  gramsevakName: string | null;
  gramsevakMobile: string | null;
  operatorName: string | null;
  operatorMobile: string | null;
  gramsevakSignatureKey: string | null;
  sarpanchSignatureKey: string | null;
  operatorSignatureKey: string | null;
  officialStampKey: string | null;
  assessmentYearLabel: string;
  assessmentStartYear: number;
  assessmentEndYear: number;
  lockAssessmentYear: boolean;
  initializeArrearsRollover: boolean;
  primaryUpiId: string | null;
  upiPayeeName: string | null;
  bankAccounts: BankAccountPayload[];
  seedDefaultFormulas: boolean;
  activateTenant: boolean;
  provenance: LgdProvenance | null;
}

export interface UploadedAssetResponse {
  assetKey: string;
  category: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface DashboardSummary {
  tenantId: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  lgdCode: string;
  activeAssessmentYear: string;
  setupProgressPct: number;
  totalProperties: number;
  currentFyDemand: number;
  todayCollection: number;
  monthCollection: number;
  outstandingArrears: number;
  pendingMutations: number;
  waterAndSpecialCollection: number;
  todayReceipts: number;
  checklistSteps: {
    stepNumber: number;
    key: string;
    isCompleted: boolean;
    detail: string | null;
  }[];
  recentActivity: {
    id: string;
    entityName: string;
    entityId: string;
    action: string;
    actorId: string | null;
    actorRole: string | null;
    timestampUtc: string;
  }[];
}

export interface DashboardSearchResult {
  properties: {
    id: string;
    wardNo: string;
    propertyNo: string;
    totalAreaSqFt: number;
    waterConnectionCode: string;
  }[];
  citizens: {
    id: string;
    citizenCode: string;
    wardNo: string | null;
    fullNameEn: string;
    fullNameMr: string;
    mobileNo: string | null;
    aadhaarMasked: string | null;
  }[];
}

export interface ProvisionTenantPayload {
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  districtName: string;
  tehsilName: string;
  districtCode: string | null;
  tehsilCode: string | null;
  pincode: string | null;
  censusCode: string | null;
  officeAddress: string | null;
  adminFullName: string;
  adminUsername: string;
  adminEmail: string;
  adminPassword: string;
  planName: 'Standard' | 'Enterprise';
  paymentMode: 'UPI' | 'NEFT_RTGS' | 'CASH' | 'CHEQUE' | 'TREASURY';
  paymentAmount: number;
  durationDays: number;
  paymentReference: string | null;
  totalWards: number;
  populationCensus: number | null;
}

export interface ProvisionedTenantResult {
  tenantId: string;
  tenantCode: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  status: string;
  subscriptionExpiresAtUtc: string;
  adminUsername: string;
  adminRole: string;
  paymentReference: string;
  amountPaid: number;
  formulaSeeded: boolean;
}

export interface SuperAdminTenantSummary {
  tenantId: string;
  tenantCode: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  districtName: string;
  tehsilName: string;
  status: string;
  subscriptionExpiresAtUtc: string | null;
  setupProgressPct: number;
}

export interface AgentRegistrationResult {
  agentId: string;
  userId: string;
  agentCode: string;
  fullName: string;
  mobileNo: string;
  kycStatus: 'PendingApproval' | 'Approved' | 'Suspended' | 'Rejected' | number;
  aadhaarMasked: string;
  panMasked: string;
  requestedCommissionRatePct: number;
}

export interface AgentProfile {
  agentId: string;
  userId: string;
  agentCode: string;
  kycStatus: 'PendingApproval' | 'Approved' | 'Suspended' | 'Rejected' | number;
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
}

export const sprint2Api = {
  login: (username: string, password: string, tenantId?: string | null) =>
    apiRequest<AuthResponse>('auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password, tenantId: tenantId || null }),
    }),

  refresh: (tenantId?: string | null) =>
    apiRequest<AuthResponse>('auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ tenantId: tenantId || null }),
    }),

  logout: () => apiRequest<void>('auth/logout', { method: 'POST', body: JSON.stringify({}) }),

  me: () => apiRequest<AuthenticatedUser>('auth/me'),

  getOnboardingStatus: () => apiRequest<OnboardingStatus>('onboarding/status'),

  completeOnboarding: (payload: CompleteOnboardingPayload) =>
    apiRequest<OnboardingStatus>('onboarding/complete', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  registerSelfServiceGp: (
    adminUsername: string,
    adminPassword: string,
    adminEmail: string | null,
    wizard: CompleteOnboardingPayload,
  ) =>
    apiRequest<OnboardingStatus>('onboarding/register-gp', {
      method: 'POST',
      body: JSON.stringify({ adminUsername, adminPassword, adminEmail, wizard }),
    }),

  uploadProtectedAsset: (category: 'SIGNATURE' | 'STAMP' | 'QR', file: File) => {
    const form = new FormData();
    form.append('category', category);
    form.append('file', file);
    return apiRequest<UploadedAssetResponse>('onboarding/upload-asset', {
      method: 'POST',
      body: form,
    });
  },

  getDashboardSummary: () => apiRequest<DashboardSummary>('dashboard/summary'),

  searchDashboard: (q: string) =>
    apiRequest<DashboardSearchResult>(`dashboard/search?q=${encodeURIComponent(q)}&limit=10`),

  listSuperAdminTenants: () => apiRequest<SuperAdminTenantSummary[]>('superadmin/tenants'),

  provisionTenantDirect: (payload: ProvisionTenantPayload) =>
    apiRequest<ProvisionedTenantResult>('superadmin/tenants/provision', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  listSuperAdminAgents: () => apiRequest<AgentProfile[]>('superadmin/agents'),

  updateAgentStatus: (
    agentId: string,
    status: number,
    approvedNewGpRatePct?: number,
    approvedRenewalRatePct?: number,
    reason?: string,
  ) =>
    apiRequest<AgentProfile>(`superadmin/agents/${agentId}/status`, {
      method: 'POST',
      body: JSON.stringify({ status, approvedNewGpRatePct, approvedRenewalRatePct, reason }),
    }),

  registerAgent: (formData: FormData) =>
    apiRequest<AgentRegistrationResult>('agents/register', {
      method: 'POST',
      body: formData,
    }),

  loginAgent: (identifier: string, password: string) =>
    apiRequest<AuthResponse>('agents/login', {
      method: 'POST',
      body: JSON.stringify({ identifier, password }),
    }),

  getAgentProfile: () => apiRequest<AgentProfile>('agents/me'),
};

