import { apiDownloadFile, apiRequest } from './apiClient';
import { triggerBlobDownload } from './printApi';

export type Namuna9KaLayoutMode = '2in1_landscape' | 'a5';
export type Namuna9KaSplitMode = 'counterfoil' | 'batch2in1';

export interface Namuna9KaHeadRowDto {
  buildingArrears: number;
  buildingCurrent: number;
  lightingArrears: number;
  lightingCurrent: number;
  healthArrears: number;
  healthCurrent: number;
  sanitationArrears: number;
  sanitationCurrent: number;
  toiletArrears: number;
  toiletCurrent: number;
  generalWaterArrears: number;
  generalWaterCurrent: number;
  specialWaterArrears: number;
  specialWaterCurrent: number;
  otherArrears: number;
  otherCurrent: number;
  carriedFeesAndPenalty: number;
  noticeFee: number;
  warrantFee: number;
  penaltyAmount: number;
  rebateAmount: number;
  paidAmount: number;
  totalArrears: number;
  totalCurrent: number;
  totalPayable: number;
}

export interface Namuna9KaBillRowDto {
  demandId: string;
  billId?: string | null;
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  primaryOwnerNameMr?: string | null;
  primaryOwnerNameEn?: string | null;
  primaryOwnerAadhaarMasked?: string | null;
  primaryOwnerMobileMasked?: string | null;
  occupierName: string;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  billNo?: string | null;
  billSequence?: number | null;
  issueDate?: string | null;
  dueDate?: string | null;
  rebateCutoffDate?: string | null;
  billStatus: 'PendingGeneration' | 'Issued' | 'PartiallyPaid' | 'Paid' | string;
  demandStatus: string;
  arrearsAmount: number;
  currentAmount: number;
  generalDemandTotal: number;
  waterDemandTotal: number;
  noticeAndPenaltyAmount: number;
  rebateAmount: number;
  paidAmount: number;
  netPayableAmount: number;
  amountInWordsMr: string;
  upiIdUsed?: string | null;
  upiPayUri?: string | null;
  isStaleBill: boolean;
  heads: Namuna9KaHeadRowDto;
}

export interface Namuna9KaYearSummaryDto {
  id: string;
  yearLabel: string;
  startYear: number;
  endYear: number;
  isActive: boolean;
  isLocked: boolean;
}

export interface Namuna9KaWorkspaceDto {
  selectedYear: Namuna9KaYearSummaryDto;
  assessmentYears: Namuna9KaYearSummaryDto[];
  noticeGraceDays: number;
  earlyRebatePct: number;
  defaultRebateCutoffDate: string;
  hasUpiConfigured: boolean;
  configuredUpiId?: string | null;
  configuredPayeeName?: string | null;
  totalProperties: number;
  billsIssuedCount: number;
  pendingGenerationCount: number;
  totalNetPayable: number;
  items: Namuna9KaBillRowDto[];
}

export interface Namuna9KaGeneratedBillRefDto {
  billId: string;
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  billNo: string;
  billSequence: number;
  netPayableAmount: number;
  issueDate: string;
  dueDate: string;
  upiPayUri?: string | null;
  newlyIssued: boolean;
  replacedSuperseded: boolean;
}

export interface GenerateBillsResultDto {
  yearLabel: string;
  generatedCount: number;
  reusedCount: number;
  supersededCount: number;
  hasUpiConfigured: boolean;
  configuredUpiId?: string | null;
  warningMessage?: string | null;
  bills: Namuna9KaGeneratedBillRefDto[];
}

function buildBillsQuery(params: {
  yearId?: string;
  wardNo?: string;
  search?: string;
  pendingOnly?: boolean;
  propertyIds?: string[];
  layout?: Namuna9KaLayoutMode;
  splitMode?: Namuna9KaSplitMode;
}): string {
  const qs = new URLSearchParams();
  if (params.yearId) qs.set('yearId', params.yearId);
  if (params.wardNo) qs.set('wardNo', params.wardNo);
  if (params.search) qs.set('search', params.search);
  if (params.pendingOnly) qs.set('pendingOnly', 'true');
  if (params.propertyIds && params.propertyIds.length > 0) {
    qs.set('propertyIds', params.propertyIds.join(','));
  }
  if (params.layout) qs.set('layout', params.layout);
  if (params.splitMode) qs.set('splitMode', params.splitMode);
  const encoded = qs.toString();
  return encoded ? `?${encoded}` : '';
}

export const namuna9KaApi = {
  getBills(params: {
    yearId?: string;
    wardNo?: string;
    search?: string;
    pendingOnly?: boolean;
    propertyIds?: string[];
  }) {
    return apiRequest<Namuna9KaWorkspaceDto>(`/namuna-9ka/bills${buildBillsQuery(params)}`);
  },

  generateBills(payload: {
    assessmentYearId?: string;
    wardNo?: string;
    propertyIds?: string[];
    issueDate?: string;
    regenerateIfChanged?: boolean;
  }) {
    return apiRequest<GenerateBillsResultDto>('/namuna-9ka/bills/generate', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async downloadBillsPdf(params: {
    yearId?: string;
    wardNo?: string;
    search?: string;
    propertyIds?: string[];
    layout: Namuna9KaLayoutMode;
    splitMode: Namuna9KaSplitMode;
  }): Promise<void> {
    const { blob, fileName } = await apiDownloadFile(
      `/namuna-9ka/bills.pdf${buildBillsQuery(params)}`,
      `namuna9ka-bills-${params.layout}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
  },
};

