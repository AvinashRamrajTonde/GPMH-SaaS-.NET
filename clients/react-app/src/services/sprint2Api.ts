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

export type CanonicalKycStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Suspended'
  | 'Rejected';

const ROLE_BY_NUMBER: Record<number, CanonicalRole> = {
  1: 'SuperAdmin',
  2: 'Gramsevak',
  3: 'GP_Admin',
  4: 'GP_Operator',
  5: 'Surveyor',
  6: 'Citizen',
  7: 'Auditor',
  8: 'Support_Admin',
  9: 'Field_Agent',
};

const KYC_STATUS_BY_NUMBER: Record<number, CanonicalKycStatus> = {
  1: 'PendingApproval',
  2: 'Approved',
  3: 'Suspended',
  4: 'Rejected',
};

export function normalizeRole(raw: unknown): CanonicalRole {
  if (typeof raw === 'number' && raw in ROLE_BY_NUMBER) {
    return ROLE_BY_NUMBER[raw];
  }
  if (typeof raw === 'string') {
    const trimmed = raw.trim();
    const num = Number(trimmed);
    if (!Number.isNaN(num) && num in ROLE_BY_NUMBER) {
      return ROLE_BY_NUMBER[num];
    }
    const valid = Object.values(ROLE_BY_NUMBER) as string[];
    if (valid.includes(trimmed)) {
      return trimmed as CanonicalRole;
    }
  }
  return 'Citizen';
}

export function normalizeKycStatus(raw: unknown): CanonicalKycStatus {
  if (typeof raw === 'number' && raw in KYC_STATUS_BY_NUMBER) {
    return KYC_STATUS_BY_NUMBER[raw];
  }
  if (typeof raw === 'string') {
    const trimmed = raw.trim();
    const num = Number(trimmed);
    if (!Number.isNaN(num) && num in KYC_STATUS_BY_NUMBER) {
      return KYC_STATUS_BY_NUMBER[num];
    }
    const valid = Object.values(KYC_STATUS_BY_NUMBER) as string[];
    if (valid.includes(trimmed)) {
      return trimmed as CanonicalKycStatus;
    }
  }
  return 'PendingApproval';
}

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

interface RawAuthUser {
  userId: string;
  username: string;
  fullNameEn: string;
  fullNameMr: string;
  role: unknown;
  activeTenantId: string | null;
  memberships: { tenantId: string; role: unknown; isDefault: boolean }[];
}

interface RawAuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: RawAuthUser;
  refreshToken?: string | null;
}

function mapUser(raw: RawAuthUser): AuthenticatedUser {
  return {
    ...raw,
    role: normalizeRole(raw.role),
    memberships: (raw.memberships ?? []).map((m) => ({
      ...m,
      role: normalizeRole(m.role),
    })),
  };
}

function mapAuthResponse(raw: RawAuthResponse): AuthResponse {
  return {
    ...raw,
    user: mapUser(raw.user),
  };
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

export interface PlatformPaymentAccountSummary {
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
  subscriptionStatus?: string;
  subscriptionExpiresAtUtc?: string | null;
  daysUntilExpiry?: number | null;
  showExpiryWarning30Days?: boolean;
  annualRenewalFee?: number;
  platformPaymentInfo?: PlatformPaymentAccountSummary | null;
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
  effectiveStatus: string;
  daysUntilExpiry: number | null;
  isTrial: boolean;
  isDeleted: boolean;
  deletedAtUtc: string | null;
  adminUsername: string | null;
  adminPhone: string | null;
  totalWards: number;
  populationCensus: number | null;
  propertiesCount: number;
  citizensCount: number;
  taxDemanded: number;
  taxCollected: number;
  referredByAgentId: string | null;
  referralCodeUsed: string | null;
}

export interface AgentRegistrationResult {
  agentId: string;
  userId: string;
  agentCode: string;
  fullName: string;
  mobileNo: string;
  kycStatus: CanonicalKycStatus;
  aadhaarMasked: string;
  panMasked: string;
  requestedCommissionRatePct: number;
}

export interface AgentProfile {
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
  aadhaarDocumentKey?: string | null;
  panDocumentKey?: string | null;
}

interface RawAgentRegistrationResult extends Omit<AgentRegistrationResult, 'kycStatus'> {
  kycStatus: unknown;
}

interface RawAgentProfile extends Omit<AgentProfile, 'kycStatus'> {
  kycStatus: unknown;
}

function mapAgentProfile(raw: RawAgentProfile): AgentProfile {
  return {
    ...raw,
    kycStatus: normalizeKycStatus(raw.kycStatus),
  };
}

export interface PropertyItem {
  id: string;
  tenantId: string;
  parentPropertyId: string | null;
  wardNo: string;
  propertyNo: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  boundaryEast: string | null;
  boundaryWest: string | null;
  boundaryNorth: string | null;
  boundarySouth: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  isActive: boolean;
}

export interface CreatePropertyPayload {
  wardNo: string;
  propertyNo: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  boundaryEast?: string | null;
  boundaryWest?: string | null;
  boundaryNorth?: string | null;
  boundarySouth?: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
}

export interface AuditLogEntry {
  id: string;
  tenantId: string | null;
  entityName: string;
  entityId: string;
  action: string;
  actorId: string | null;
  actorRole: string | null;
  correlationId: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  oldValuesJson: string | null;
  newValuesJson: string | null;
  changedColumnsJson: string | null;
  timestampUtc: string;
}

export interface TaxPreviewRequest {
  assessmentStartYear: number;
  floors: {
    lengthFt: number;
    widthFt: number;
    areaSqm: number | null;
    landAnnualValueRate: number;
    constructionReadyReckonerRate: number;
    usageWeightage: number;
    taxRate: number;
    constructionStartYear: number;
  }[];
  depreciationSlabs: {
    ageFrom: number;
    ageTo: number;
    retentionPercentage: number;
  }[];
  areaSlabTaxes: {
    lightingTax: number;
    healthTax: number;
    sanitationTax: number;
    toiletTax: number;
  };
  waterTax: number;
  otherTax: number;
  openAreaSqm: number;
  openPlotLandRate: number;
  openPlotTaxRate: number;
  taxDivisor: number;
  roundAreaToTwoDecimals: boolean;
}

export interface TaxPreviewResponse {
  floorBreakdowns: {
    floorIndex: number;
    areaSqm: number;
    buildingAge: number;
    depreciationPercentage: number;
    landValue: number;
    depreciatedBuildingValue: number;
    floorCapitalValue: number;
    rawBuildingTax: number;
  }[];
  buildingTax: number;
  openLandTax: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  waterTax: number;
  otherTax: number;
  totalTax: number;
}

export const sprint2Api = {
  login: async (username: string, password: string, tenantId?: string | null): Promise<AuthResponse> => {
    const raw = await apiRequest<RawAuthResponse>('auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password, tenantId: tenantId || null }),
    });
    return mapAuthResponse(raw);
  },

  refresh: async (tenantId?: string | null): Promise<AuthResponse> => {
    const raw = await apiRequest<RawAuthResponse>('auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ tenantId: tenantId || null }),
    });
    return mapAuthResponse(raw);
  },

  logout: () => apiRequest<void>('auth/logout', { method: 'POST', body: JSON.stringify({}) }),

  me: async (): Promise<AuthenticatedUser> => {
    const raw = await apiRequest<RawAuthUser>('auth/me');
    return mapUser(raw);
  },

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
    referralCode?: string | null,
  ) =>
    apiRequest<OnboardingStatus>('onboarding/register-gp', {
      method: 'POST',
      body: JSON.stringify({
        adminUsername,
        adminPassword,
        adminEmail,
        wizard,
        ...(referralCode ? { referralCode } : {}),
      }),
    }),

  uploadProtectedAsset: (category: 'SIGNATURE' | 'STAMP' | 'QR' | 'PAYMENT_SLIP', file: File) => {
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

  previewCapitalTax: (payload: TaxPreviewRequest) =>
    apiRequest<TaxPreviewResponse>('tax/preview', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  listProperties: () => apiRequest<PropertyItem[]>('properties'),

  getPropertyById: (id: string) => apiRequest<PropertyItem>(`properties/${encodeURIComponent(id)}`),

  createProperty: (payload: CreatePropertyPayload) =>
    apiRequest<PropertyItem>('properties', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  getAuditTrail: (entityName: string, entityId: string) =>
    apiRequest<AuditLogEntry[]>(
      `audit/${encodeURIComponent(entityName)}/${encodeURIComponent(entityId)}`,
    ),

  listSuperAdminTenants: () => apiRequest<SuperAdminTenantSummary[]>('superadmin/tenants'),

  provisionTenantDirect: (payload: ProvisionTenantPayload) =>
    apiRequest<ProvisionedTenantResult>('superadmin/tenants/provision', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  listSuperAdminAgents: async (): Promise<AgentProfile[]> => {
    const rows = await apiRequest<RawAgentProfile[]>('superadmin/agents');
    return rows.map(mapAgentProfile);
  },

  updateAgentStatus: async (
    agentId: string,
    status: number,
    approvedNewGpRatePct?: number,
    approvedRenewalRatePct?: number,
    reason?: string,
  ): Promise<AgentProfile> => {
    const raw = await apiRequest<RawAgentProfile>(`superadmin/agents/${agentId}/status`, {
      method: 'POST',
      body: JSON.stringify({ status, approvedNewGpRatePct, approvedRenewalRatePct, reason }),
    });
    return mapAgentProfile(raw);
  },

  registerAgent: async (formData: FormData): Promise<AgentRegistrationResult> => {
    const raw = await apiRequest<RawAgentRegistrationResult>('agents/register', {
      method: 'POST',
      body: formData,
    });
    return {
      ...raw,
      kycStatus: normalizeKycStatus(raw.kycStatus),
    };
  },

  loginAgent: async (identifier: string, password: string): Promise<AuthResponse> => {
    const raw = await apiRequest<RawAuthResponse>('agents/login', {
      method: 'POST',
      body: JSON.stringify({ identifier, password }),
    });
    return mapAuthResponse(raw);
  },

  getAgentProfile: async (): Promise<AgentProfile> => {
    const raw = await apiRequest<RawAgentProfile>('agents/me');
    return mapAgentProfile(raw);
  },
};
