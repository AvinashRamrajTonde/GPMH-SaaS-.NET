import { apiRequest } from './apiClient';
import type { PropertyFloorRequestItem, PropertyOwnerRequestItem } from './namuna8Api';

export interface PartitionOwnerItemDto {
  citizenId: string;
  fullNameMr: string;
  fullNameEn: string;
  ownershipSharePct: number;
  isPrimaryOwner: boolean;
}

export interface PartitionFloorItemDto {
  floorNo: number;
  floorLabel: string;
  constructionTypeCode: string;
  usageCode: string;
  lengthFt: number;
  widthFt: number;
  areaSqFt: number;
  areaSqM: number;
  constructionStartYear: number;
}

export interface PartitionEligibilityDto {
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  status: string;
  isActive: boolean;
  streetOrLaneName?: string | null;
  citySurveyOrGutNo?: string | null;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  totalAreaSqM: number;
  pendingDues: number;
  arrearsBalance: number;
  currentDemand: number;
  paidAmount: number;
  zeroDuesGatePassed: boolean;
  blockReason?: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  otherTax: number;
  currentOwners: PartitionOwnerItemDto[];
  currentFloors: PartitionFloorItemDto[];
  suggestedChildPropertyNos: string[];
}

export interface PartitionChildRequestItem {
  childPropertyNo: string;
  owners: PropertyOwnerRequestItem[];
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  floors?: PropertyFloorRequestItem[] | null;
  streetOrLaneName?: string | null;
  citySurveyOrGutNo?: string | null;
  occupierName?: string | null;
  propertyDescription?: string | null;
  boundaryEast?: string | null;
  boundaryWest?: string | null;
  boundaryNorth?: string | null;
  boundarySouth?: string | null;
  waterConnectionCode?: string | null;
  otherTax: number;
  isTaxExempt: boolean;
}

export interface ExecutePropertyPartitionRequest {
  resolutionNo: string;
  resolutionDate: string;
  childPartitions: PartitionChildRequestItem[];
  partitionReason?: string | null;
  documentReferenceNo?: string | null;
  documentDate?: string | null;
  remarks?: string | null;
  supportingAssetKeys?: string[] | null;
}

export interface PartitionChildResultDto {
  propertyId: string;
  wardNo: string;
  propertyNo: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  areaSqM: number;
  totalAnnualTax: number;
  draftDemandBalance: number;
  primaryOwnerNameMr: string;
  primaryOwnerNameEn: string;
}

export interface ExecutePartitionResultDto {
  parentPropertyId: string;
  originalParentPropertyNo: string;
  archivedParentPropertyNo: string;
  partitionMutationId: string;
  applicationNo: string;
  parentAreaSqM: number;
  totalChildAreaSqM: number;
  areaDeltaSqM: number;
  allowedToleranceSqM: number;
  childProperties: PartitionChildResultDto[];
}

export const partitionApi = {
  getEligibility: (propertyId: string) =>
    apiRequest<PartitionEligibilityDto>(
      `/properties/${encodeURIComponent(propertyId)}/partition-eligibility`,
    ),

  executePartition: (propertyId: string, payload: ExecutePropertyPartitionRequest) =>
    apiRequest<ExecutePartitionResultDto>(
      `/properties/${encodeURIComponent(propertyId)}/partition`,
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
    ),
};

