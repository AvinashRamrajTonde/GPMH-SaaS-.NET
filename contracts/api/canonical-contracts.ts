// Canonical TypeScript API Contracts for GPMH React 19.3 Frontend
// Reconciles conflicting enums, fixes legacy typos (openPlotAreaFeett/assementYear),
// masks Aadhaar PII, and matches RFC 9457 ProblemDetails error payloads.

export type ApplicationRole =
  | 'SuperAdmin'
  | 'Gramsevak'
  | 'GP_Admin'
  | 'GP_Operator'
  | 'Surveyor'
  | 'Citizen'
  | 'Auditor'
  | 'Support_Admin'
  | 'Field_Agent';

export type Gender = 'Male' | 'Female' | 'Other';

export type SocialCategory =
  | 'General'
  | 'OBC'
  | 'SC'
  | 'ST'
  | 'VJ_A'
  | 'NT_B'
  | 'NT_C'
  | 'NT_D'
  | 'SBC'
  | 'SEBC'
  | 'EWS';

export type ExemptionCategory =
  | 'NotExempted'
  | 'FamilyOfFreedomFighter'
  | 'ExServicemen'
  | 'WarWidows'
  | 'Martyrs'
  | 'NaturalCalamityVictim'
  | 'PoorWidow'
  | 'PersonWithDisability';

export type PaymentMode = 'Cash' | 'Cheque' | 'Upi' | 'Online';

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface LgdPanchayatDto {
  lgd_code: string;
  gp_name_en: string;
  gp_name_mr: string;
  district: string;
  district_code?: string;
  tehsil: string;
  tehsil_code?: string;
  pincode?: string;
}

export interface CitizenDto {
  id: string;
  tenantId: string;
  citizenCode: string;
  wardNo?: string;
  fullNameMr: string;
  fullNameEn: string;
  gender: Gender;
  mobileNo?: string;
  aadhaarMasked?: string; // Strictly "XXXX-XXXX-1234" (never raw 12 digits)
  socialCategory: SocialCategory;
  exemptionCategory: ExemptionCategory;
  isActive: boolean;
}

export interface PropertyAssessmentDto {
  id: string;
  propertyId: string;
  assessmentYear: string;
  buildingTax: number;
  openLandTax: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  waterTax: number;
  otherTax: number;
  totalTax: number;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
}

export interface TaxCollectionRequest {
  operationId: string; // Client UUID idempotency key (required for online/offline sync)
  propertyId: string;
  assessmentYearId: string;
  bookNo: string;
  amountPaid: number;
  paymentMode: PaymentMode;
  referenceNo?: string;
  payerName: string;
}

