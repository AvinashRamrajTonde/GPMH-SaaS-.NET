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

export interface ResetFormulaDefaultsPayload {
  resolutionNo?: string | null;
  resolutionDate?: string | null;
  resolutionReason?: string | null;
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

  resetDefaults: (payload?: ResetFormulaDefaultsPayload) =>
    apiRequest<FormulaWorkspaceDto>('formula/reset-defaults', {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    }),
};

