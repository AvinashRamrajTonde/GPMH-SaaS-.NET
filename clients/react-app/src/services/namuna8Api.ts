import { apiRequest } from './apiClient';

export const SOCIAL_CATEGORIES = [
  'General',
  'OBC',
  'SC',
  'ST',
  'VJNT',
  'NT',
  'SBC',
  'Minority',
  'Other',
];

export const EXEMPTION_CATEGORIES = [
  'NotExempted',
  'Disabled',
  'Widow',
  'ExServiceman',
  'FreedomFighter',
  'SeniorCitizen',
];

export interface CitizenDto {
  id: string;
  tenantId: string;
  citizenCode: string;
  wardNo?: string | null;
  fullNameMr: string;
  fullNameEn: string;
  gender: 'Male' | 'Female' | 'Other' | string;
  mobileNo?: string | null;
  aadhaarMasked?: string | null;
  socialCategory: string;
  exemptionCategory: string;
  isActive: boolean;
  linkedPropertiesCount: number;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface PagedCitizensDto {
  items: CitizenDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateCitizenPayload {
  citizenCode?: string | null;
  wardNo?: string | null;
  fullNameMr: string;
  fullNameEn: string;
  gender: string;
  mobileNo?: string | null;
  aadhaarNumber?: string | null;
  socialCategory: string;
  exemptionCategory: string;
  dpdpConsentAccepted: boolean;
}

export interface UpdateCitizenPayload {
  wardNo?: string | null;
  fullNameMr: string;
  fullNameEn: string;
  gender: string;
  mobileNo?: string | null;
  aadhaarNumber?: string | null;
  socialCategory: string;
  exemptionCategory: string;
  isActive: boolean;
}

export interface PropertyDto {
  id: string;
  tenantId: string;
  parentPropertyId?: string | null;
  wardNo: string;
  propertyNo: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  boundaryEast?: string | null;
  boundaryWest?: string | null;
  boundaryNorth?: string | null;
  boundarySouth?: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  isActive: boolean;
  streetOrLaneName?: string | null;
  citySurveyOrGutNo?: string | null;
  occupierName?: string | null;
  propertyDescription?: string | null;
  otherTax: number;
  status: string;
  primaryOwnerNameMr?: string | null;
  primaryOwnerNameEn?: string | null;
  totalAnnualTax?: number | null;
  pendingBalance?: number | null;
  floorCount: number;
}

export interface PropertyOwnerDetailDto {
  id: string;
  citizenId: string;
  citizenCode: string;
  fullNameMr: string;
  fullNameEn: string;
  mobileNo?: string | null;
  aadhaarMasked?: string | null;
  exemptionCategory: string;
  ownershipSharePct: number;
  isPrimaryOwner: boolean;
  isActive: boolean;
}

export interface PropertyConstructionDto {
  id: string;
  floorNo: number;
  floorLabel: string;
  constructionTypeCode: string;
  usageCode: string;
  lengthFt: number;
  widthFt: number;
  areaSqFt: number;
  areaSqM: number;
  constructionStartYear: number;
  isActive: boolean;
}

export interface PropertyTaxCalcDto {
  id: string;
  assessmentYearId: string;
  assessmentYearLabel: string;
  buildingTax: number;
  openLandTax: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  waterTax: number;
  otherTax: number;
  totalTax: number;
  calculationSnapshotJson: string;
  updatedAtUtc?: string | null;
}

export interface PropertyDemandDto {
  id: string;
  assessmentYearId: string;
  assessmentYearLabel: string;
  arrearsBalance: number;
  currentDemand: number;
  rebateAmount: number;
  penaltyAmount: number;
  paidAmount: number;
  pendingBalance: number;
  status: string;
}

export interface ChildPropertySummaryDto {
  id: string;
  wardNo: string;
  propertyNo: string;
  totalAreaSqFt: number;
  status: string;
}

export interface PropertyDetailDto {
  id: string;
  tenantId: string;
  parentPropertyId?: string | null;
  wardNo: string;
  propertyNo: string;
  streetOrLaneName?: string | null;
  citySurveyOrGutNo?: string | null;
  occupierName?: string | null;
  propertyDescription?: string | null;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  totalAreaSqFt: number;
  boundaryEast?: string | null;
  boundaryWest?: string | null;
  boundaryNorth?: string | null;
  boundarySouth?: string | null;
  waterConnectionCode: string;
  otherTax: number;
  isTaxExempt: boolean;
  status: string;
  isActive: boolean;
  owners: PropertyOwnerDetailDto[];
  constructions: PropertyConstructionDto[];
  taxCalc?: PropertyTaxCalcDto | null;
  demand?: PropertyDemandDto | null;
  childProperties: ChildPropertySummaryDto[];
}

export interface PropertyHubKpisDto {
  totalProperties: number;
  totalAssessedDemand: number;
  exemptPropertiesCount: number;
  activeWardsCount: number;
  partitionedCount: number;
}

export interface PropertyOwnerRequestItem {
  citizenId: string;
  ownershipSharePct: number;
  isPrimaryOwner: boolean;
}

export interface PropertyFloorRequestItem {
  floorNo: number;
  floorLabel: string;
  constructionTypeCode: string;
  usageCode: string;
  lengthFt: number;
  widthFt: number;
  areaSqFt?: number | null;
  constructionStartYear: number;
}

export interface SaveFullPropertyPayload {
  wardNo?: string;
  propertyNo?: string;
  builtUpAreaSqFt: number;
  openPlotAreaSqFt: number;
  boundaryEast?: string | null;
  boundaryWest?: string | null;
  boundaryNorth?: string | null;
  boundarySouth?: string | null;
  waterConnectionCode: string;
  isTaxExempt: boolean;
  streetOrLaneName?: string | null;
  citySurveyOrGutNo?: string | null;
  occupierName?: string | null;
  propertyDescription?: string | null;
  otherTax: number;
  openPlotConstructionCode?: string | null;
  owners?: PropertyOwnerRequestItem[] | null;
  floors?: PropertyFloorRequestItem[] | null;
  calculateTax: boolean;
}

export const namuna8Api = {
  listCitizens: (params?: {
    search?: string;
    wardNo?: string;
    exemptionCategory?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const sp = new URLSearchParams();
    if (params?.search) sp.set('search', params.search);
    if (params?.wardNo) sp.set('wardNo', params.wardNo);
    if (params?.exemptionCategory) sp.set('exemptionCategory', params.exemptionCategory);
    if (params?.page) sp.set('page', String(params.page));
    if (params?.pageSize) sp.set('pageSize', String(params.pageSize));
    const qs = sp.toString();
    return apiRequest<PagedCitizensDto>(`citizens${qs ? `?${qs}` : ''}`);
  },

  createCitizen: (payload: CreateCitizenPayload) =>
    apiRequest<CitizenDto>('citizens', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateCitizen: (id: string, payload: UpdateCitizenPayload) =>
    apiRequest<CitizenDto>(`citizens/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  listProperties: () => apiRequest<PropertyDto[]>('properties'),

  getHubSummary: () => apiRequest<PropertyHubKpisDto>('properties/hub-summary'),

  getPropertyDetail: (id: string) =>
    apiRequest<PropertyDetailDto>(`properties/${encodeURIComponent(id)}`),

  createProperty: (payload: SaveFullPropertyPayload) =>
    apiRequest<PropertyDto>('properties', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateProperty: (id: string, payload: SaveFullPropertyPayload) =>
    apiRequest<PropertyDto>(`properties/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteProperty: (id: string) =>
    apiRequest<void>(`properties/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    }),
};
