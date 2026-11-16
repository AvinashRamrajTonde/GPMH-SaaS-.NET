import { apiDownloadFile, apiRequest } from './apiClient';
import { triggerBlobDownload } from './printApi';

export type Namuna9Format = 'A' | 'B' | 'C';
export type Namuna9RegisterPart = 'Both' | 'Demand' | 'Recovery';
export type Namuna9PrintStyle = 'color' | 'bw';

export interface Namuna9HeadAmountsDto {
  arrears: number;
  current: number;
  total: number;
}

export interface DemandCollectedBreakdownDto {
  houseTax: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  generalWaterTax: number;
  specialWaterTax: number;
  otherTax: number;
  noticeFeePaid: number;
  warrantFeePaid: number;
  penaltyPaid: number;
  rebateGranted: number;
}

export interface DemandHeadBreakdownDto {
  houseTax: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  generalWaterTax: number;
  specialWaterTax: number;
  otherTax: number;
  feesAndPenalty: number;
}

export interface Namuna9DemandRowDto {
  demandId: string;
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  primaryOwnerNameMr?: string | null;
  primaryOwnerNameEn?: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  houseTax: Namuna9HeadAmountsDto;
  lightingTax: Namuna9HeadAmountsDto;
  healthTax: Namuna9HeadAmountsDto;
  sanitationTax: Namuna9HeadAmountsDto;
  toiletTax: Namuna9HeadAmountsDto;
  generalWaterTax: Namuna9HeadAmountsDto;
  specialWaterTax: Namuna9HeadAmountsDto;
  otherTax: Namuna9HeadAmountsDto;
  carriedFeesAndPenalty: number;
  noticeFee: number;
  warrantFee: number;
  penaltyAmount: number;
  rebateAmount: number;
  totalArrears: number;
  totalCurrent: number;
  netDemand: number;
  collectedByHead: DemandCollectedBreakdownDto;
  balanceByHead: DemandHeadBreakdownDto;
  paidAmount: number;
  pendingBalance: number;
  status: string;
  earlyRebateApplied: boolean;
  rebateCutoffDate?: string | null;
  remarks?: string | null;
  publishedAtUtc?: string | null;
}

export interface Namuna9KpisDto {
  totalDemand: number;
  totalCollected: number;
  totalRebate: number;
  totalBalanceDues: number;
  generalTaxDemand: number;
  waterTaxDemand: number;
  propertyCount: number;
}

export interface Namuna9YearDto {
  id: string;
  yearLabel: string;
  startYear: number;
  endYear: number;
  isActive: boolean;
  isLocked: boolean;
}

export interface Namuna9FormulaRatesDto {
  ratePeriodName: string;
  taxDivisor: number;
  noticeGraceDays: number;
  earlyRebatePct: number;
  latePenaltyPct: number;
  defaultRebateCutoffDate: string;
}

export interface Namuna9WorkspaceDto {
  selectedYear: Namuna9YearDto;
  assessmentYears: Namuna9YearDto[];
  formulaRates: Namuna9FormulaRatesDto;
  format: Namuna9Format;
  kpis: Namuna9KpisDto;
  items: Namuna9DemandRowDto[];
}

export interface Namuna9FeeAdjustmentItem {
  propertyId: string;
  noticeFee: number;
  warrantFee: number;
  penaltyAmount: number;
  rebateAmount: number;
  remarks?: string | null;
}

export interface Namuna9PublishResultDto {
  yearLabel: string;
  publishedCount: number;
  skippedCount: number;
}

export interface Namuna9AdjustedDemandDto {
  demandId: string;
  propertyId: string;
  netDemand: number;
  pendingBalance: number;
  status: string;
}

export interface Namuna9AdjustResultDto {
  yearLabel: string;
  updatedCount: number;
  items: Namuna9AdjustedDemandDto[];
}

export interface Namuna9RebateResultDto {
  yearLabel: string;
  applicationDate: string;
  cutoffDate: string;
  earlyRebatePct: number;
  appliedCount: number;
  skippedCount: number;
  totalRebateGranted: number;
}

export interface Namuna9RolloverResultDto {
  sourceYearId: string;
  sourceYearLabel: string;
  targetYearId: string;
  targetYearLabel: string;
  propertiesRolledOver: number;
  totalCarriedArrears: number;
  totalCurrentDemand: number;
}

function buildFilterQuery(params: {
  yearId?: string;
  wardNo?: string;
  search?: string;
  format?: Namuna9Format;
  pendingOnly?: boolean;
  part?: Namuna9RegisterPart;
  style?: Namuna9PrintStyle;
  leftMarginMm?: number;
}): string {
  const qs = new URLSearchParams();
  if (params.yearId) qs.set('yearId', params.yearId);
  if (params.wardNo) qs.set('wardNo', params.wardNo);
  if (params.search) qs.set('search', params.search);
  if (params.format) qs.set('format', params.format);
  if (params.pendingOnly) qs.set('pendingOnly', 'true');
  if (params.part) qs.set('part', params.part);
  if (params.style) qs.set('style', params.style);
  if (params.leftMarginMm !== undefined) qs.set('leftMarginMm', String(params.leftMarginMm));
  const encoded = qs.toString();
  return encoded ? `?${encoded}` : '';
}

export const namuna9Api = {
  getRegister(params: {
    yearId?: string;
    wardNo?: string;
    search?: string;
    format?: Namuna9Format;
    pendingOnly?: boolean;
  }) {
    return apiRequest<Namuna9WorkspaceDto>(`/namuna-9/register${buildFilterQuery(params)}`);
  },

  publishDrafts(payload: { assessmentYearId?: string; propertyIds?: string[] }) {
    return apiRequest<Namuna9PublishResultDto>('/namuna-9/publish', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  adjustFees(payload: {
    assessmentYearId: string;
    adjustments: Namuna9FeeAdjustmentItem[];
    remarks?: string;
  }) {
    return apiRequest<Namuna9AdjustResultDto>('/namuna-9/adjustments', {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  applyEarlyRebate(payload: {
    assessmentYearId: string;
    applicationDate: string;
    cutoffDate?: string;
    propertyIds?: string[];
  }) {
    return apiRequest<Namuna9RebateResultDto>('/namuna-9/apply-rebate', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  executeRollover(payload: {
    sourceAssessmentYearId: string;
    targetYearLabel: string;
    carryForwardCurrentTaxCalc?: boolean;
  }) {
    return apiRequest<Namuna9RolloverResultDto>('/namuna-9/rollover', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async downloadRegisterPdf(params: {
    yearId?: string;
    wardNo?: string;
    search?: string;
    format: Namuna9Format;
    part: Namuna9RegisterPart;
    style: Namuna9PrintStyle;
    leftMarginMm: number;
    pendingOnly?: boolean;
  }): Promise<void> {
    const { blob, fileName } = await apiDownloadFile(
      `/namuna-9/register.pdf${buildFilterQuery(params)}`,
      `namuna9-register-format-${params.format.toLowerCase()}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
  },

  async downloadCsv(params: {
    yearId?: string;
    wardNo?: string;
    search?: string;
    format: Namuna9Format;
    pendingOnly?: boolean;
  }): Promise<void> {
    const { blob, fileName } = await apiDownloadFile(
      `/namuna-9/export.csv${buildFilterQuery(params)}`,
      `namuna9-register-format-${params.format.toLowerCase()}.csv`,
    );
    triggerBlobDownload(blob, fileName);
  },
};

