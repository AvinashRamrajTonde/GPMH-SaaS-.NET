import { apiDownloadFile, apiRequest } from './apiClient';
import { triggerBlobDownload } from './printApi';
import type { Namuna9YearDto } from './namuna9Api';

export type PaymentModeCode = 1 | 2 | 3 | 4 | 5;

export const PaymentMode = {
  Cash: 1 as PaymentModeCode,
  Upi: 2 as PaymentModeCode,
  Cheque: 3 as PaymentModeCode,
  NeftRtgs: 4 as PaymentModeCode,
  OnlineGateway: 5 as PaymentModeCode,
} as const;

export interface TaxHeadDuesDto {
  houseTax: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  generalWaterTax: number;
  specialWaterTax: number;
  otherTax: number;
  generalTaxTotal: number;
  waterTotal: number;
  total: number;
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
  generalTaxTotal: number;
  waterTotal: number;
  total: number;
}

export interface TaxReceiptHeadAllocationDto {
  noticeFeePaid: number;
  warrantFeePaid: number;
  penaltyPaid: number;
  rebateApplied: number;
  arrearsAllocated?: DemandHeadBreakdownDto | null;
  currentAllocated?: DemandHeadBreakdownDto | null;
  excessCredit: number;
  totalFeesPaid?: number;
  totalHeadsAllocated?: number;
  totalWaterAllocated?: number;
  totalAllocatedTowardDemand?: number;
}

export interface Namuna10PropertySummaryReceiptDto {
  receiptId: string;
  receiptNo: string;
  bookNo: string;
  receiptDate: string;
  paymentMode: PaymentModeCode;
  totalPaid: number;
  isCancelled: boolean;
}

export interface Namuna10RegisterRowDto {
  demandId: string;
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  primaryOwnerNameMr?: string | null;
  primaryOwnerNameEn?: string | null;
  primaryOwnerAadhaarMasked?: string | null;
  primaryOwnerMobileMasked?: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  demandStatus: string;
  activeBillNo?: string | null;
  activeBillId?: string | null;
  arrearsDue: TaxHeadDuesDto;
  currentDue: TaxHeadDuesDto;
  noticeFeeDue: number;
  warrantFeeDue: number;
  penaltyDue: number;
  carriedFeesDue: number;
  rebateAmount: number;
  earlyRebateEligible: boolean;
  earlyRebateApplied: boolean;
  rebateCutoffDate?: string | null;
  netDemand: number;
  paidAmount: number;
  remainingBalance: number;
  lastReceipt?: Namuna10PropertySummaryReceiptDto | null;
}

export interface Namuna10KpisDto {
  totalProperties: number;
  totalDemand: number;
  totalCollected: number;
  totalBalance: number;
  partiallyPaidCount: number;
  fullyPaidCount: number;
}

export interface Namuna10RegisterWorkspaceDto {
  selectedYear: Namuna9YearDto;
  assessmentYears: Namuna9YearDto[];
  noticeGraceDays: number;
  earlyRebatePct: number;
  defaultRebateCutoffDate: string;
  kpis: Namuna10KpisDto;
  items: Namuna10RegisterRowDto[];
}

export interface TaxReceiptDto {
  receiptId: string;
  tenantId: string;
  propertyId: string;
  propertyDemandId: string;
  assessmentYearId: string;
  yearLabel: string;
  operationId: string;
  demandBillId?: string | null;
  billNo?: string | null;
  wardNo: string;
  propertyNo: string;
  primaryOwnerNameMr?: string | null;
  primaryOwnerNameEn?: string | null;
  primaryOwnerAadhaarMasked?: string | null;
  primaryOwnerMobileMasked?: string | null;
  bookNo: string;
  receiptNo: string;
  receiptSequence: number;
  receiptDate: string;
  collectedAtUtc: string;
  paymentMode: PaymentModeCode;
  payerName: string;
  referenceNo?: string | null;
  bankName?: string | null;
  chequeDate?: string | null;
  remarks?: string | null;
  totalPaid: number;
  discountGiven: number;
  excessCredit: number;
  remainingBalanceAfterPayment: number;
  headAllocation: TaxReceiptHeadAllocationDto;
  amountInWordsMr: string;
  isCancelled: boolean;
  cancelReason?: string | null;
  cancelledAtUtc?: string | null;
  idempotentReplay: boolean;
}

export interface Namuna10ReceiptsPageDto {
  selectedYear: Namuna9YearDto;
  assessmentYears: Namuna9YearDto[];
  totalCount: number;
  totalCollectedAmount: number;
  cancelledCount: number;
  items: TaxReceiptDto[];
}

export interface CollectTaxPayload {
  propertyId: string;
  assessmentYearId: string;
  operationId: string;
  amountPaid: number;
  paymentMode: PaymentModeCode;
  receiptDate?: string;
  bookNo?: string;
  payerName?: string;
  referenceNo?: string;
  bankName?: string;
  chequeDate?: string;
  remarks?: string;
  applyEligibleEarlyRebate?: boolean;
}

export const namuna10Api = {
  getRegister(params: {
    yearId?: string;
    wardNo?: string;
    search?: string;
    status?: string;
  }) {
    const qs = new URLSearchParams();
    if (params.yearId) qs.set('yearId', params.yearId);
    if (params.wardNo) qs.set('wardNo', params.wardNo);
    if (params.search) qs.set('search', params.search);
    if (params.status) qs.set('status', params.status);
    const suffix = qs.toString() ? `?${qs.toString()}` : '';
    return apiRequest<Namuna10RegisterWorkspaceDto>(`/namuna-10/register${suffix}`);
  },

  listReceipts(params: {
    yearId?: string;
    propertyId?: string;
    fromDate?: string;
    toDate?: string;
    mode?: PaymentModeCode;
    wardNo?: string;
    search?: string;
    includeCancelled?: boolean;
  }) {
    const qs = new URLSearchParams();
    if (params.yearId) qs.set('yearId', params.yearId);
    if (params.propertyId) qs.set('propertyId', params.propertyId);
    if (params.fromDate) qs.set('fromDate', params.fromDate);
    if (params.toDate) qs.set('toDate', params.toDate);
    if (params.mode) qs.set('mode', String(params.mode));
    if (params.wardNo) qs.set('wardNo', params.wardNo);
    if (params.search) qs.set('search', params.search);
    if (params.includeCancelled !== undefined) {
      qs.set('includeCancelled', params.includeCancelled ? 'true' : 'false');
    }
    const suffix = qs.toString() ? `?${qs.toString()}` : '';
    return apiRequest<Namuna10ReceiptsPageDto>(`/namuna-10/receipts${suffix}`);
  },

  getReceiptById(receiptId: string) {
    return apiRequest<TaxReceiptDto>(`/namuna-10/receipts/${encodeURIComponent(receiptId)}`);
  },

  collectTax(payload: CollectTaxPayload) {
    return apiRequest<TaxReceiptDto>('/namuna-10/collections', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async downloadDualReceiptPdf(receiptId: string, receiptNo?: string): Promise<void> {
    const safeName = (receiptNo || receiptId).replace(/[^a-zA-Z0-9_-]/g, '_');
    const { blob, fileName } = await apiDownloadFile(
      `/namuna-10/receipts/${encodeURIComponent(receiptId)}/dual.pdf`,
      `namuna10-receipt-${safeName}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
  },
};

