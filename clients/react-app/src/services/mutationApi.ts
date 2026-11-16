import {
  apiDownloadFile,
  apiFetchBlobUrl,
  apiRequest,
} from './apiClient';
import { triggerBlobDownload } from './printApi';

export const MUTATION_TYPES = [
  'Heirship',
  'SaleDeed',
  'GiftDeed',
  'ReleaseDeed',
  'CourtDecree',
] as const;

export interface MutationOwnerSnapshotItem {
  citizenId: string;
  citizenCode: string;
  fullNameMr: string;
  fullNameEn: string;
  aadhaarMasked?: string | null;
  ownershipSharePct: number;
  isPrimaryOwner: boolean;
}

export interface MutationSupportingAssetDto {
  assetKey: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface MutationListItemDto {
  id: string;
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  applicationNo: string;
  mutationType: string;
  previousOwnersSummaryMr: string;
  previousOwnersSummaryEn: string;
  proposedOwnersSummaryMr: string;
  proposedOwnersSummaryEn: string;
  documentReferenceNo?: string | null;
  documentDate?: string | null;
  status: 'NoticePublished' | 'ObjectionRecorded' | 'Approved' | 'Rejected' | string;
  noticeIssuedAtUtc: string;
  noticeEndsAtUtc: string;
  daysRemaining: number;
  isNoticeWindowElapsed: boolean;
  hasObjection: boolean;
  resolutionNo?: string | null;
  resolutionDate?: string | null;
  createdAtUtc: string;
}

export interface PagedMutationsDto {
  items: MutationListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface MutationDetailDto {
  id: string;
  tenantId: string;
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  streetOrLaneName?: string | null;
  propertyDescription?: string | null;
  totalAreaSqFt: number;
  applicationNo: string;
  mutationType: string;
  previousOwners: MutationOwnerSnapshotItem[];
  proposedOwners: MutationOwnerSnapshotItem[];
  documentReferenceNo?: string | null;
  documentDate?: string | null;
  remarks?: string | null;
  supportingAssets: MutationSupportingAssetDto[];
  status: string;
  noticeIssuedAtUtc: string;
  noticeEndsAtUtc: string;
  daysRemaining: number;
  isNoticeWindowElapsed: boolean;
  hasObjection: boolean;
  objectionSummary?: string | null;
  objectionRecordedAtUtc?: string | null;
  resolutionNo?: string | null;
  resolutionDate?: string | null;
  rejectionReason?: string | null;
  approvedAtUtc?: string | null;
  createdAtUtc: string;
}

export interface UploadedMutationAssetDto {
  assetKey: string;
  category: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface CreateMutationPayload {
  propertyId: string;
  mutationType: string;
  proposedOwners: Array<{
    citizenId: string;
    ownershipSharePct: number;
    isPrimaryOwner: boolean;
  }>;
  documentReferenceNo?: string | null;
  documentDate?: string | null;
  remarks?: string | null;
  supportingAssetKeys?: string[];
}

export const mutationApi = {
  list(params: {
    status?: string;
    search?: string;
    wardNo?: string;
    page?: number;
    pageSize?: number;
  }) {
    const qs = new URLSearchParams();
    if (params.status) qs.set('status', params.status);
    if (params.search) qs.set('search', params.search);
    if (params.wardNo) qs.set('wardNo', params.wardNo);
    qs.set('page', String(params.page ?? 1));
    qs.set('pageSize', String(params.pageSize ?? 25));
    return apiRequest<PagedMutationsDto>(`/mutations?${qs.toString()}`);
  },

  getById(id: string) {
    return apiRequest<MutationDetailDto>(`/mutations/${encodeURIComponent(id)}`);
  },

  initiate(payload: CreateMutationPayload) {
    return apiRequest<MutationDetailDto>('/mutations', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  recordObjection(id: string, objectionSummary: string) {
    return apiRequest<MutationDetailDto>(`/mutations/${encodeURIComponent(id)}/objection`, {
      method: 'POST',
      body: JSON.stringify({ objectionSummary }),
    });
  },

  approve(
    id: string,
    payload: { resolutionNo: string; resolutionDate: string; objectionResolvedNotes?: string | null },
  ) {
    return apiRequest<MutationDetailDto>(`/mutations/${encodeURIComponent(id)}/approve`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  reject(
    id: string,
    payload: { rejectionReason: string; resolutionNo: string; resolutionDate: string },
  ) {
    return apiRequest<MutationDetailDto>(`/mutations/${encodeURIComponent(id)}/reject`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async uploadDocument(file: File): Promise<UploadedMutationAssetDto> {
    const formData = new FormData();
    formData.append('category', 'MUTATION_DOC');
    formData.append('file', file);
    return apiRequest<UploadedMutationAssetDto>('/assets/upload', {
      method: 'POST',
      body: formData,
    });
  },

  openProtectedAsset(assetKey: string) {
    return apiFetchBlobUrl(`/assets/${encodeURIComponent(assetKey)}`);
  },

  async downloadRegisterPdf(params?: { wardNo?: string; fromDate?: string; toDate?: string }) {
    const qs = new URLSearchParams();
    if (params?.wardNo) qs.set('wardNo', params.wardNo);
    if (params?.fromDate) qs.set('fromDate', params.fromDate);
    if (params?.toDate) qs.set('toDate', params.toDate);
    const suffix = qs.toString() ? `?${qs.toString()}` : '';
    const { blob, fileName } = await apiDownloadFile(
      `/mutations/register.pdf${suffix}`,
      'mutation-register.pdf',
    );
    triggerBlobDownload(blob, fileName);
  },
};

