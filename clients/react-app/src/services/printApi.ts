import { ApiProblemError, apiDownloadFile, apiRequest, getApiBaseUrl, type ProblemDetails } from './apiClient';

export interface Namuna8ExtractPreviewDto {
  tenantId: string;
  propertyId: string;
  assessmentYearId: string;
  srNo: number;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  villageNameMr: string;
  tehsilName: string;
  districtName: string;
  pincode?: string | null;
  assessmentYearLabel: string;
  ratePeriodName: string;
  documentRefNo: string;
  issuedDateDisplay: string;
  wardNo: string;
  propertyNo: string;
  streetOrLaneName?: string | null;
  citySurveyOrGutNo?: string | null;
  ownersSummaryMr: string;
  ownersSummaryEn: string;
  primaryOwnerAadhaarMasked?: string | null;
  primaryOwnerMobileMasked?: string | null;
  occupierName: string;
  propertyDescriptionMr: string;
  constructionYearsDisplay: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  totalAreaSqM: number;
  openLandRrRate: number;
  buildingLandRate: number;
  constructionRrRate: number;
  depreciationPercentDisplay: string;
  usageWeightageDisplay: string;
  totalCapitalValue: number;
  taxRateDisplay: string;
  buildingTaxComponent: number;
  lightingTax: number;
  healthTax: number;
  waterTax: number;
  sanitationTax: number;
  toiletTax: number;
  otherTax: number;
  totalAnnualTax: number;
  revisionHouseTax?: number | null;
  revisionLightingTax?: number | null;
  revisionHealthTax?: number | null;
  revisionWaterTax?: number | null;
  revisionTotalTax?: number | null;
  remarks: string;
  waterConnectionLabelMr: string;
  isTaxExempt: boolean;
  status: string;
  boundaryEast?: string | null;
  boundaryWest?: string | null;
  boundaryNorth?: string | null;
  boundarySouth?: string | null;
  gramsevakName?: string | null;
  sarpanchName?: string | null;
  operatorName?: string | null;
  verifyToken: string;
  verifyUrl: string;
  verifyQrSvg: string;
  upiId?: string | null;
  upiPayUri?: string | null;
}

export interface DigitalNameplateItemDto {
  propertyId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  tehsilName: string;
  districtName: string;
  pincode?: string | null;
  wardNo: string;
  propertyNo: string;
  ownerNameMr: string;
  ownerNameEn: string;
  coOwnerNamesMr?: string | null;
  occupierLabelMr: string;
  constructionAndUsageMr: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  totalAreaSqM: number;
  totalAnnualTax: number;
  isTaxExempt: boolean;
  uniqueCode: string;
  verifyToken: string;
  verifyUrl: string;
  verifyQrSvg: string;
  upiId?: string | null;
  upiPayUri?: string | null;
  upiQrSvg?: string | null;
}

export interface VerifiedExtractPublicDto {
  isValid: boolean;
  token: string;
  tenantId: string;
  propertyId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  tehsilName: string;
  districtName: string;
  assessmentYearLabel: string;
  wardNo: string;
  propertyNo: string;
  primaryOwnerNameMr: string;
  primaryOwnerNameEn: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  totalAreaSqM: number;
  totalAnnualTax: number;
  isTaxExempt: boolean;
  status: string;
  issuedAtUtc: string;
}

export interface VerifiedReceiptPublicDto {
  isValid: boolean;
  token: string;
  tenantId: string;
  receiptId: string;
  lgdCode: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  tehsilName: string;
  districtName: string;
  assessmentYearLabel: string;
  receiptNo: string;
  bookNo: string;
  receiptDate: string;
  wardNo: string;
  propertyNo: string;
  primaryOwnerNameMr: string;
  primaryOwnerNameEn: string;
  paymentMode: string;
  totalPaid: number;
  discountGiven: number;
  isCancelled: boolean;
  cancelReason?: string | null;
  cancelledAtUtc?: string | null;
  issuedAtUtc: string;
}

async function verifyPublicResource<T>(relativePath: string): Promise<T> {
  const response = await fetch(`${getApiBaseUrl()}${relativePath}`, {
    headers: { Accept: 'application/json' },
  });

  if (!response.ok) {
    let problem: ProblemDetails = {
      title: 'Verification Failed',
      status: response.status,
      detail: response.statusText,
    };
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON
    }
    throw new ApiProblemError(problem);
  }

  return (await response.json()) as T;
}

function buildFilterQuery(params: {
  propertyId?: string;
  wardNo?: string;
  propertyIds?: string[];
  layout?: string;
}): string {
  const qs = new URLSearchParams();
  if (params.propertyId) qs.set('propertyId', params.propertyId);
  if (params.wardNo) qs.set('wardNo', params.wardNo);
  if (params.propertyIds && params.propertyIds.length > 0) {
    qs.set('propertyIds', params.propertyIds.join(','));
  }
  if (params.layout) qs.set('layout', params.layout);
  const str = qs.toString();
  return str ? `?${str}` : '';
}

export function triggerBlobDownload(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  setTimeout(() => URL.revokeObjectURL(objectUrl), 1500);
}

export const printApi = {
  previewExtracts(params: { propertyId?: string; wardNo?: string; propertyIds?: string[] }) {
    return apiRequest<Namuna8ExtractPreviewDto[]>(
      `/properties/namuna8-extract/preview${buildFilterQuery(params)}`,
    );
  },

  previewNameplates(params: { propertyId?: string; wardNo?: string; propertyIds?: string[] }) {
    return apiRequest<DigitalNameplateItemDto[]>(
      `/properties/nameplates/preview${buildFilterQuery(params)}`,
    );
  },

  async downloadSingleExtractPdf(propertyId: string): Promise<void> {
    const { blob, fileName } = await apiDownloadFile(
      `/properties/${encodeURIComponent(propertyId)}/namuna8-extract.pdf`,
      `namuna8-${propertyId}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
  },

  async downloadBatchExtractPdf(params: { wardNo?: string; propertyIds?: string[] }): Promise<void> {
    const { blob, fileName } = await apiDownloadFile(
      `/properties/namuna8-extract/batch.pdf${buildFilterQuery(params)}`,
      `namuna8-ward-${params.wardNo || 'batch'}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
  },

  async downloadNameplatesPdf(params: {
    propertyId?: string;
    wardNo?: string;
    propertyIds?: string[];
    layout?: 'bulk' | 'single';
  }): Promise<void> {
    const { blob, fileName } = await apiDownloadFile(
      `/properties/nameplates.pdf${buildFilterQuery(params)}`,
      `nameplates-${params.wardNo || 'sheet'}.pdf`,
    );
    triggerBlobDownload(blob, fileName);
  },

  verifyPublicExtract(token: string): Promise<VerifiedExtractPublicDto> {
    return verifyPublicResource<VerifiedExtractPublicDto>(
      `public/verify/extract/${encodeURIComponent(token)}`,
    );
  },

  verifyPublicReceipt(token: string): Promise<VerifiedReceiptPublicDto> {
    return verifyPublicResource<VerifiedReceiptPublicDto>(
      `public/verify/receipt/${encodeURIComponent(token)}`,
    );
  },
};

