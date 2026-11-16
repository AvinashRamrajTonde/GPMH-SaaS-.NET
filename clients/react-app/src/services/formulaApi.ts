import { apiRequest } from './apiClient';

export type ConstructionCategory =
  | 'HUT_MUD'
  | 'STONE_BRICK_RCC'
  | 'OPEN_LAND'
  | 'SPECIAL';

export interface FormulaSettingDto {
  id: string;
  tenantId: string;
  ratePeriodName: string;
  rateStartYear: number;
  rateEndYear: number;
  taxDivisor: number;
  noticeGraceDays: number;
  earlyRebatePct: number;
  latePenaltyPct: number;
  applyLightingHealthOnOpenPlot: boolean;
  roundArea: boolean;
  isActive: boolean;
  lastResolutionNo?: string | null;
  lastResolutionDate?: string | null;
  lastChangeReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface ConstructionTypeMasterDto {
  id: string;
  tenantId: string;
  code: string;
  typeName: string;
  category: ConstructionCategory;
  landAnnualValueRate: number;
  constructionReadyReckonerRate: number;
  usageWeightageFactor: number;
  taxRate: number;
  isActive: boolean;
  lastResolutionNo?: string | null;
  lastResolutionDate?: string | null;
  lastChangeReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface UsageWeightageDto {
  id: string;
  tenantId: string;
  code: string;
  usageName: string;
  weightage: number;
  isActive: boolean;
  lastResolutionNo?: string | null;
  lastResolutionDate?: string | null;
  lastChangeReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface DepreciationSlabDto {
  id: string;
  tenantId: string;
  ageFrom: number;
  ageTo: number;
  fromInclusive: boolean;
  toInclusive: boolean;
  hutMudPercent: number;
  stoneBrickRccPercent: number;
  lastResolutionNo?: string | null;
  lastResolutionDate?: string | null;
  lastChangeReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface AreaSlabTaxDto {
  id: string;
  tenantId: string;
  areaFrom: number;
  areaTo: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
  lastResolutionNo?: string | null;
  lastResolutionDate?: string | null;
  lastChangeReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface WaterTaxMasterDto {
  id: string;
  tenantId: string;
  code: string;
  waterTypeName: string;
  amount: number;
  isActive: boolean;
  lastResolutionNo?: string | null;
  lastResolutionDate?: string | null;
  lastChangeReason?: string | null;
  updatedAtUtc?: string | null;
}

export interface FormulaWorkspaceSummaryDto {
  constructionTypesCount: number;
  usageWeightagesCount: number;
  depreciationSlabsCount: number;
  areaSlabTaxesCount: number;
  waterTaxesCount: number;
  ratePeriodsCount: number;
}

export interface FormulaWorkspaceDto {
  activeSetting: FormulaSettingDto | null;
  ratePeriods: FormulaSettingDto[];
  constructionTypes: ConstructionTypeMasterDto[];
  summary: FormulaWorkspaceSummaryDto;
  usageWeightages?: UsageWeightageDto[] | null;
  depreciationSlabs?: DepreciationSlabDto[] | null;
  areaSlabTaxes?: AreaSlabTaxDto[] | null;
  waterTaxes?: WaterTaxMasterDto[] | null;
}

export interface SaveFormulaSettingPayload {
  id?: string | null;
  ratePeriodName: string;
  rateStartYear: number;
  rateEndYear: number;
  taxDivisor: number;
  noticeGraceDays: number;
  earlyRebatePct: number;
  latePenaltyPct: number;
  applyLightingHealthOnOpenPlot: boolean;
  roundArea: boolean;
  isActive: boolean;
  resolutionNo: string;
  resolutionDate?: string | null;
  resolutionReason: string;
  createNewPeriod?: boolean;
}

export interface SaveConstructionTypePayload {
  id?: string | null;
  code: string;
  typeName: string;
  category: ConstructionCategory;
  landAnnualValueRate: number;
  constructionReadyReckonerRate: number;
  usageWeightageFactor: number;
  taxRate: number;
  isActive: boolean;
  resolutionNo?: string | null;
  resolutionDate?: string | null;
  changeReason?: string | null;
}

export interface SaveDepreciationSlabsBatchPayload {
  slabs: {
    ageFrom: number;
    ageTo: number;
    hutMudPercent: number;
    stoneBrickRccPercent: number;
  }[];
  resolutionNo: string;
  resolutionDate?: string | null;
  changeReason: string;
}

export interface SaveUsageWeightagePayload {
  id?: string | null;
  code: string;
  usageName: string;
  weightage: number;
  isActive: boolean;
  resolutionNo: string;
  resolutionDate?: string | null;
  changeReason: string;
}

export interface SaveAreaSlabTaxesBatchPayload {
  slabs: {
    areaFrom: number;
    areaTo: number;
    lightingTax: number;
    healthTax: number;
    sanitationTax: number;
    toiletTax: number;
  }[];
  resolutionNo: string;
  resolutionDate?: string | null;
  changeReason: string;
}

export interface SaveWaterTaxMasterPayload {
  id?: string | null;
  code: string;
  waterTypeName: string;
  amount: number;
  isActive: boolean;
  resolutionNo: string;
  resolutionDate?: string | null;
  changeReason: string;
}

export interface ResetFormulaDefaultsPayload {
  resolutionNo?: string | null;
  resolutionDate?: string | null;
  resolutionReason?: string | null;
}

export interface MasterFloorTaxRequestPayload {
  constructionTypeCode: string;
  usageCode: string;
  constructionStartYear: number;
  lengthFt: number;
  widthFt: number;
  areaSqFt?: number | null;
  areaSqm?: number | null;
}

export interface CalculateFromMastersPayload {
  assessmentStartYear: number;
  floors: MasterFloorTaxRequestPayload[];
  openAreaSqFt: number;
  openAreaSqm?: number | null;
  openPlotConstructionCode: string;
  waterConnectionCode: string;
  otherTax: number;
}

export interface FloorTaxBreakdownDto {
  floorIndex: number;
  areaSqm: number;
  buildingAge: number;
  depreciationPercentage: number;
  landValue: number;
  depreciatedBuildingValue: number;
  floorCapitalValue: number;
  rawBuildingTax: number;
}

export interface PropertyTaxCalculationResultDto {
  floorBreakdowns: FloorTaxBreakdownDto[];
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

export interface CalculatedPropertyTaxDto {
  assessmentStartYear: number;
  ratePeriodName: string;
  taxDivisor: number;
  totalBuiltUpSqFt: number;
  totalBuiltUpSqM: number;
  openAreaSqFt: number;
  openAreaSqM: number;
  waterConnectionCode: string;
  appliedAreaSlabRange: string;
  calculation: PropertyTaxCalculationResultDto;
}

export const formulaApi = {
  getWorkspace: () => apiRequest<FormulaWorkspaceDto>('formula/workspace'),

  saveSettings: (payload: SaveFormulaSettingPayload) =>
    apiRequest<FormulaSettingDto>('formula/settings', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  listConstructionTypes: () =>
    apiRequest<ConstructionTypeMasterDto[]>('formula/construction-types'),

  saveConstructionType: (payload: SaveConstructionTypePayload) => {
    if (payload.id) {
      return apiRequest<ConstructionTypeMasterDto>(
        `formula/construction-types/${encodeURIComponent(payload.id)}`,
        {
          method: 'PUT',
          body: JSON.stringify(payload),
        },
      );
    }

    return apiRequest<ConstructionTypeMasterDto>('formula/construction-types', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  saveDepreciationSlabs: (payload: SaveDepreciationSlabsBatchPayload) =>
    apiRequest<DepreciationSlabDto[]>('formula/depreciation-slabs', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  saveUsageWeightage: (payload: SaveUsageWeightagePayload) => {
    if (payload.id) {
      return apiRequest<UsageWeightageDto>(
        `formula/usage-weightages/${encodeURIComponent(payload.id)}`,
        {
          method: 'PUT',
          body: JSON.stringify(payload),
        },
      );
    }
    return apiRequest<UsageWeightageDto>('formula/usage-weightages', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  saveAreaSlabs: (payload: SaveAreaSlabTaxesBatchPayload) =>
    apiRequest<AreaSlabTaxDto[]>('formula/area-slabs', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  saveWaterTax: (payload: SaveWaterTaxMasterPayload) => {
    if (payload.id) {
      return apiRequest<WaterTaxMasterDto>(
        `formula/water-taxes/${encodeURIComponent(payload.id)}`,
        {
          method: 'PUT',
          body: JSON.stringify(payload),
        },
      );
    }
    return apiRequest<WaterTaxMasterDto>('formula/water-taxes', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  resetDefaults: (payload?: ResetFormulaDefaultsPayload) =>
    apiRequest<FormulaWorkspaceDto>('formula/reset-defaults', {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    }),

  calculateFromMasters: (payload: CalculateFromMastersPayload) =>
    apiRequest<CalculatedPropertyTaxDto>('tax/calculate', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
};
