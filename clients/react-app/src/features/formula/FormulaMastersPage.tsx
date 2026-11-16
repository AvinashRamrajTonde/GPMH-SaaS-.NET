import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Droplets,
  Edit3,
  History,
  Layers,
  Percent,
  Plus,
  RotateCcw,
  Save,
  Scale,
  Search,
  ShieldCheck,
  SlidersHorizontal,
  TrendingDown,
  X,
} from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import {
  type ConstructionCategory,
  type ConstructionTypeMasterDto,
  formulaApi,
  type FormulaSettingDto,
  type SaveConstructionTypePayload,
  type SaveFormulaSettingPayload,
} from '../../services/formulaApi';
import { formatDateTime, formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { FormulaSimulatorCard } from './FormulaSimulatorCard';
import {
  AreaSlabTaxesTab,
  DepreciationSlabsTab,
  UsageWeightagesTab,
  WaterTaxMastersTab,
} from './SecondaryFormulaTabs';

type FormulaTab = 'construction' | 'depreciation' | 'usage' | 'areaSlabs' | 'waterTax';

const CATEGORIES: ConstructionCategory[] = [
  'HUT_MUD',
  'STONE_BRICK_RCC',
  'OPEN_LAND',
  'SPECIAL',
];

const CATEGORY_BADGE_STYLES: Record<ConstructionCategory, string> = {
  HUT_MUD: 'bg-amber-50 text-amber-800 border-amber-200',
  STONE_BRICK_RCC: 'bg-brand-50 text-brand-800 border-brand-200',
  OPEN_LAND: 'bg-sky-50 text-sky-800 border-sky-200',
  SPECIAL: 'bg-violet-50 text-violet-800 border-violet-200',
};

function buildInitialSettingsForm(active: FormulaSettingDto | null): SaveFormulaSettingPayload {
  return {
    id: active?.id ?? null,
    ratePeriodName: active?.ratePeriodName ?? '2026-27 ते 2029-30',
    rateStartYear: active?.rateStartYear ?? 2026,
    rateEndYear: active?.rateEndYear ?? 2029,
    taxDivisor: active?.taxDivisor ?? 1000,
    noticeGraceDays: active?.noticeGraceDays ?? 15,
    earlyRebatePct: active?.earlyRebatePct ?? 10,
    latePenaltyPct: active?.latePenaltyPct ?? 10,
    applyLightingHealthOnOpenPlot: active?.applyLightingHealthOnOpenPlot ?? true,
    roundArea: active?.roundArea ?? true,
    isActive: active?.isActive ?? true,
    resolutionNo: '',
    resolutionDate: new Date().toISOString().slice(0, 10),
    resolutionReason: '',
    createNewPeriod: false,
  };
}

function buildInitialConstructionForm(item?: ConstructionTypeMasterDto | null): SaveConstructionTypePayload {
  if (!item) {
    return {
      id: null,
      code: '',
      typeName: '',
      category: 'STONE_BRICK_RCC',
      landAnnualValueRate: 760,
      constructionReadyReckonerRate: 0,
      usageWeightageFactor: 1,
      taxRate: 1.0,
      isActive: true,
      resolutionNo: '',
      resolutionDate: new Date().toISOString().slice(0, 10),
      changeReason: '',
    };
  }

  return {
    id: item.id,
    code: item.code,
    typeName: item.typeName,
    category: item.category,
    landAnnualValueRate: item.landAnnualValueRate,
    constructionReadyReckonerRate: item.constructionReadyReckonerRate,
    usageWeightageFactor: item.usageWeightageFactor,
    taxRate: item.taxRate,
    isActive: item.isActive,
    resolutionNo: item.lastResolutionNo ?? '',
    resolutionDate: item.lastResolutionDate ?? new Date().toISOString().slice(0, 10),
    changeReason: '',
  };
}

export function FormulaMastersPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState<FormulaTab>('construction');
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<'ALL' | ConstructionCategory>('ALL');

  const [settingsDrawerOpen, setSettingsDrawerOpen] = useState(false);
  const [settingsForm, setSettingsForm] = useState<SaveFormulaSettingPayload>(() =>
    buildInitialSettingsForm(null),
  );
  const [savingSettings, setSavingSettings] = useState(false);
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [settingsErrorItems, setSettingsErrorItems] = useState<ParsedValidationItem[]>([]);

  const [constructionDrawerOpen, setConstructionDrawerOpen] = useState(false);
  const [constructionForm, setConstructionForm] = useState<SaveConstructionTypePayload>(() =>
    buildInitialConstructionForm(null),
  );
  const [savingConstruction, setSavingConstruction] = useState(false);
  const [constructionError, setConstructionError] = useState<string | null>(null);
  const [constructionErrorItems, setConstructionErrorItems] = useState<ParsedValidationItem[]>([]);

  const [resetOpen, setResetOpen] = useState(false);
  const [resetResolutionNo, setResetResolutionNo] = useState('');
  const [resetReason, setResetReason] = useState('');
  const [resetting, setResetting] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor', 'Auditor'].includes(user.role),
  );
  const canWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin'].includes(user.role),
  );

  const workspaceQuery = useQuery({
    queryKey: ['formula-workspace', user?.activeTenantId],
    queryFn: formulaApi.getWorkspace,
    enabled: canRead,
  });

  const activeSetting = workspaceQuery.data?.activeSetting ?? null;
  const constructionTypes = useMemo(
    () => workspaceQuery.data?.constructionTypes ?? [],
    [workspaceQuery.data?.constructionTypes],
  );
  const summary = workspaceQuery.data?.summary;

  const filteredConstructionTypes = useMemo(() => {
    const q = searchTerm.trim().toLowerCase();
    return constructionTypes.filter((c) => {
      if (categoryFilter !== 'ALL' && c.category !== categoryFilter) {
        return false;
      }
      if (!q) return true;
      return (
        c.code.toLowerCase().includes(q) ||
        c.typeName.toLowerCase().includes(q) ||
        c.category.toLowerCase().includes(q)
      );
    });
  }, [constructionTypes, categoryFilter, searchTerm]);

  const categoryCounts = useMemo(() => {
    const counts: Record<ConstructionCategory, number> = {
      HUT_MUD: 0,
      STONE_BRICK_RCC: 0,
      OPEN_LAND: 0,
      SPECIAL: 0,
    };
    for (const item of constructionTypes) {
      if (item.category in counts) {
        counts[item.category] += 1;
      }
    }
    return counts;
  }, [constructionTypes]);

  function openSettingsEditor() {
    setSettingsForm(buildInitialSettingsForm(activeSetting));
    setSettingsError(null);
    setSettingsErrorItems([]);
    setSettingsDrawerOpen(true);
  }

  function openConstructionEditor(item?: ConstructionTypeMasterDto) {
    setConstructionForm(buildInitialConstructionForm(item ?? null));
    setConstructionError(null);
    setConstructionErrorItems([]);
    setConstructionDrawerOpen(true);
  }

  async function refreshWorkspace() {
    await queryClient.invalidateQueries({ queryKey: ['formula-workspace', user?.activeTenantId] });
  }

  async function handleSaveSettings(e: FormEvent) {
    e.preventDefault();
    setSettingsError(null);
    setSettingsErrorItems([]);

    const clientItems: ParsedValidationItem[] = [];
    if (!settingsForm.ratePeriodName.trim()) {
      clientItems.push({ key: 'ratePeriodName', rawField: 'RatePeriodName', message: t('validation.required') });
    }
    if (settingsForm.rateEndYear < settingsForm.rateStartYear) {
      clientItems.push({ key: 'rateEndYear', rawField: 'RateEndYear', message: t('formula.errEndBeforeStart') });
    }
    if (!(Number(settingsForm.taxDivisor) > 0)) {
      clientItems.push({ key: 'taxDivisor', rawField: 'TaxDivisor', message: t('formula.errDivisorPositive') });
    }
    if (!settingsForm.resolutionNo.trim()) {
      clientItems.push({ key: 'resolutionNo', rawField: 'ResolutionNo', message: t('formula.errResolutionNoRequired') });
    }
    if (!settingsForm.resolutionReason.trim()) {
      clientItems.push({ key: 'resolutionReason', rawField: 'ResolutionReason', message: t('formula.errReasonRequired') });
    }

    if (clientItems.length > 0) {
      setSettingsError(t('validation.summaryTitle'));
      setSettingsErrorItems(clientItems);
      return;
    }

    setSavingSettings(true);
    try {
      await formulaApi.saveSettings({
        ...settingsForm,
        resolutionDate: settingsForm.resolutionDate || null,
      });
      setSettingsDrawerOpen(false);
      setToastMessage(t('formula.settingsSavedToast'));
      await refreshWorkspace();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setSettingsError(parsed.summary);
      setSettingsErrorItems(parsed.items);
    } finally {
      setSavingSettings(false);
    }
  }

  async function handleSaveConstructionType(e: FormEvent) {
    e.preventDefault();
    setConstructionError(null);
    setConstructionErrorItems([]);

    const clientItems: ParsedValidationItem[] = [];
    if (!constructionForm.code.trim() || !/^[A-Za-z0-9_-]+$/.test(constructionForm.code.trim())) {
      clientItems.push({ key: 'code', rawField: 'Code', message: t('formula.errCodeFormat') });
    }
    if (!constructionForm.typeName.trim()) {
      clientItems.push({ key: 'typeName', rawField: 'TypeName', message: t('validation.required') });
    }
    if (
      Number(constructionForm.landAnnualValueRate) < 0 ||
      Number(constructionForm.constructionReadyReckonerRate) < 0 ||
      Number(constructionForm.usageWeightageFactor) < 0 ||
      Number(constructionForm.taxRate) < 0
    ) {
      clientItems.push({ key: 'rates', rawField: 'Rates', message: t('formula.errNegativeRate') });
    }

    if (clientItems.length > 0) {
      setConstructionError(t('validation.summaryTitle'));
      setConstructionErrorItems(clientItems);
      return;
    }

    setSavingConstruction(true);
    try {
      await formulaApi.saveConstructionType({
        ...constructionForm,
        code: constructionForm.code.trim().toUpperCase(),
        typeName: constructionForm.typeName.trim(),
        resolutionNo: constructionForm.resolutionNo?.trim() || null,
        resolutionDate: constructionForm.resolutionDate || null,
        changeReason: constructionForm.changeReason?.trim() || null,
      });
      setConstructionDrawerOpen(false);
      setToastMessage(t('formula.constructionSavedToast'));
      await refreshWorkspace();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setConstructionError(parsed.summary);
      setConstructionErrorItems(parsed.items);
    } finally {
      setSavingConstruction(false);
    }
  }

  async function handleResetDefaults(e: FormEvent) {
    e.preventDefault();
    setResetting(true);
    setToastMessage(null);
    try {
      await formulaApi.resetDefaults({
        resolutionNo: resetResolutionNo.trim() || null,
        resolutionDate: new Date().toISOString().slice(0, 10),
        resolutionReason: resetReason.trim() || null,
      });
      setResetOpen(false);
      setResetResolutionNo('');
      setResetReason('');
      setToastMessage(t('formula.resetDoneToast'));
      await refreshWorkspace();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setToastMessage(parsed.summary);
    } finally {
      setResetting(false);
    }
  }

  if (!canRead) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <div className="flex items-center gap-2 text-sm font-bold">
          <SlidersHorizontal className="h-5 w-5 text-brand-700" />
          <h1>{t('formula.title')}</h1>
        </div>
        <p>{t('formula.signInRequired')}</p>
        <Link
          to="/login"
          className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800"
        >
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {toastMessage && (
        <div
          role="status"
          className="flex items-center justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs font-semibold text-emerald-900"
        >
          <span className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-700" />
            {toastMessage}
          </span>
          <button
            type="button"
            onClick={() => setToastMessage(null)}
            className="rounded p-1 hover:bg-emerald-100"
            aria-label={t('dashboard.clearSearch')}
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      {/* Statutory Policy Header Card */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <SlidersHorizontal className="h-5 w-5 text-brand-700" />
              <h1 className="text-xl font-extrabold text-ink">{t('formula.title')}</h1>
              {activeSetting && (
                <span className="rounded-full border border-brand-200 bg-brand-50 px-2.5 py-0.5 text-[11px] font-bold text-brand-800">
                  {activeSetting.ratePeriodName} ({activeSetting.rateStartYear}–{activeSetting.rateEndYear})
                </span>
              )}
            </div>
            <p className="text-xs text-muted">{t('formula.subtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {activeSetting && (
              <Link
                to={`/audit/FormulaSetting/${activeSetting.id}`}
                className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3 py-2 text-xs font-bold text-slate-700 hover:border-brand-300 hover:bg-brand-50 hover:text-brand-800 transition"
              >
                <History className="h-3.5 w-3.5" />
                {t('formula.viewSettingsAudit')}
              </Link>
            )}
            {canWrite && (
              <>
                <button
                  type="button"
                  onClick={() => setResetOpen((prev) => !prev)}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-bold text-amber-900 hover:bg-amber-100 transition"
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                  {t('formula.resetDefaultsBtn')}
                </button>
                <button
                  type="button"
                  onClick={openSettingsEditor}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-brand-800 transition"
                >
                  <Edit3 className="h-3.5 w-3.5" />
                  {t('formula.editSettingsBtn')}
                </button>
              </>
            )}
          </div>
        </div>

        {!canWrite && (
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2.5 text-xs font-medium text-slate-700 flex items-center gap-2">
            <ShieldCheck className="h-4 w-4 text-brand-700 shrink-0" />
            <span>{t('formula.readOnlyRoleNotice')}</span>
          </div>
        )}

        {resetOpen && canWrite && (
          <form
            onSubmit={(e) => void handleResetDefaults(e)}
            className="rounded-xl border border-amber-200 bg-amber-50/70 p-4 space-y-3 text-xs"
          >
            <p className="font-bold text-amber-950">{t('formula.resetConfirmTitle')}</p>
            <p className="text-amber-900">{t('formula.resetConfirmDesc')}</p>
            <div className="grid gap-3 sm:grid-cols-2">
              <input
                type="text"
                placeholder={t('formula.resolutionNoPlaceholder')}
                value={resetResolutionNo}
                onChange={(e) => setResetResolutionNo(e.target.value)}
                className="rounded-lg border border-amber-300 bg-white px-3 py-2"
              />
              <input
                type="text"
                placeholder={t('formula.changeReasonPlaceholder')}
                value={resetReason}
                onChange={(e) => setResetReason(e.target.value)}
                className="rounded-lg border border-amber-300 bg-white px-3 py-2"
              />
            </div>
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setResetOpen(false)}
                className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 font-bold text-slate-700"
              >
                {t('properties.cancel')}
              </button>
              <button
                type="submit"
                disabled={resetting}
                className="rounded-lg bg-amber-700 px-3.5 py-1.5 font-bold text-white hover:bg-amber-800 disabled:opacity-60"
              >
                {resetting ? t('onboarding.submitting') : t('formula.confirmResetBtn')}
              </button>
            </div>
          </form>
        )}

        {workspaceQuery.isLoading ? (
          <div className="grid gap-3 sm:grid-cols-5">
            {Array.from({ length: 5 }).map((_, idx) => (
              <div key={idx} className="h-16 animate-pulse rounded-xl bg-slate-200/70" />
            ))}
          </div>
        ) : activeSetting ? (
          <div className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5 text-xs">
              <div className="rounded-xl border border-line bg-surface p-3">
                <p className="text-[11px] font-semibold text-muted">{t('formula.taxDivisor')}</p>
                <p className="mt-0.5 text-base font-extrabold text-ink mono">{activeSetting.taxDivisor}</p>
              </div>
              <div className="rounded-xl border border-line bg-surface p-3">
                <p className="text-[11px] font-semibold text-muted">{t('formula.noticeGraceDays')}</p>
                <p className="mt-0.5 text-base font-extrabold text-ink mono">
                  {activeSetting.noticeGraceDays} {t('formula.daysUnit')}
                </p>
              </div>
              <div className="rounded-xl border border-line bg-surface p-3">
                <p className="text-[11px] font-semibold text-muted">{t('formula.earlyRebatePct')}</p>
                <p className="mt-0.5 text-base font-extrabold text-emerald-700 mono">
                  {activeSetting.earlyRebatePct}%
                </p>
              </div>
              <div className="rounded-xl border border-line bg-surface p-3">
                <p className="text-[11px] font-semibold text-muted">{t('formula.latePenaltyPct')}</p>
                <p className="mt-0.5 text-base font-extrabold text-rose-700 mono">
                  {activeSetting.latePenaltyPct}%
                </p>
              </div>
              <div className="rounded-xl border border-line bg-surface p-3 space-y-1">
                <p className="text-[11px] font-semibold text-muted">{t('formula.policyFlags')}</p>
                <p className="text-[11px] font-bold text-ink">
                  {activeSetting.roundArea ? t('formula.roundAreaOn') : t('formula.roundAreaOff')} ·{' '}
                  {activeSetting.applyLightingHealthOnOpenPlot
                    ? t('formula.openPlotCessOn')
                    : t('formula.openPlotCessOff')}
                </p>
              </div>
            </div>

            {activeSetting.lastResolutionNo && (
              <div className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2 text-[11px] text-slate-700">
                <span className="font-bold text-slate-900">
                  {t('formula.lastResolutionLabel')}: {activeSetting.lastResolutionNo}
                </span>
                {activeSetting.lastResolutionDate && (
                  <span className="mono text-slate-600">({activeSetting.lastResolutionDate})</span>
                )}
                {activeSetting.lastChangeReason && <span>— {activeSetting.lastChangeReason}</span>}
                {activeSetting.updatedAtUtc && (
                  <span className="ml-auto text-slate-500">
                    {formatDateTime(activeSetting.updatedAtUtc, i18n.language)}
                  </span>
                )}
              </div>
            )}
          </div>
        ) : null}
      </section>

      {/* Formula Tabs Bar */}
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => setActiveTab('construction')}
          className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-xs font-bold transition ${
            activeTab === 'construction'
              ? 'border-brand-700 bg-brand-700 text-white shadow-xs'
              : 'border-line bg-card text-slate-700 hover:border-brand-300'
          }`}
        >
          <Layers className="h-3.5 w-3.5" />
          {t('formula.tabConstruction')}
          <span className="rounded-full bg-black/10 px-1.5 py-0.5 mono text-[10px]">
            {summary?.constructionTypesCount ?? constructionTypes.length}
          </span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('depreciation')}
          className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-xs font-bold transition ${
            activeTab === 'depreciation'
              ? 'border-brand-700 bg-brand-700 text-white shadow-xs'
              : 'border-line bg-card text-slate-700 hover:border-brand-300'
          }`}
        >
          <TrendingDown className="h-3.5 w-3.5" />
          {t('formula.tabDepreciation')}
          <span className="rounded-full bg-black/10 px-1.5 py-0.5 mono text-[10px]">
            {summary?.depreciationSlabsCount ?? 9}
          </span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('usage')}
          className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-xs font-bold transition ${
            activeTab === 'usage'
              ? 'border-brand-700 bg-brand-700 text-white shadow-xs'
              : 'border-line bg-card text-slate-700 hover:border-brand-300'
          }`}
        >
          <Scale className="h-3.5 w-3.5" />
          {t('formula.tabUsage')}
          <span className="rounded-full bg-black/10 px-1.5 py-0.5 mono text-[10px]">
            {summary?.usageWeightagesCount ?? 3}
          </span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('areaSlabs')}
          className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-xs font-bold transition ${
            activeTab === 'areaSlabs'
              ? 'border-brand-700 bg-brand-700 text-white shadow-xs'
              : 'border-line bg-card text-slate-700 hover:border-brand-300'
          }`}
        >
          <Percent className="h-3.5 w-3.5" />
          {t('formula.tabAreaSlabs')}
          <span className="rounded-full bg-black/10 px-1.5 py-0.5 mono text-[10px]">
            {summary?.areaSlabTaxesCount ?? 3}
          </span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('waterTax')}
          className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-xs font-bold transition ${
            activeTab === 'waterTax'
              ? 'border-brand-700 bg-brand-700 text-white shadow-xs'
              : 'border-line bg-card text-slate-700 hover:border-brand-300'
          }`}
        >
          <Droplets className="h-3.5 w-3.5" />
          {t('formula.tabWaterTax')}
          <span className="rounded-full bg-black/10 px-1.5 py-0.5 mono text-[10px]">
            {summary?.waterTaxesCount ?? 3}
          </span>
        </button>
      </div>

      {/* Tab Content */}
      {activeTab === 'construction' ? (
        <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="text-base font-extrabold text-ink">{t('formula.constructionTitle')}</h2>
              <p className="text-xs text-muted">{t('formula.constructionSubtitle')}</p>
            </div>
            {canWrite && (
              <button
                type="button"
                onClick={() => openConstructionEditor()}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-brand-800 transition"
              >
                <Plus className="h-4 w-4" />
                {t('formula.addConstructionBtn')}
              </button>
            )}
          </div>

          {/* Search & Category Filters */}
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative flex-1 min-w-[220px]">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <input
                type="search"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder={t('formula.searchPlaceholder')}
                className="w-full rounded-xl border border-slate-300 bg-surface py-2 pl-9 pr-3 text-xs focus:border-brand-600 focus:bg-white focus:outline-none"
              />
            </div>

            <button
              type="button"
              onClick={() => setCategoryFilter('ALL')}
              className={`rounded-xl border px-3 py-1.5 text-xs font-bold ${
                categoryFilter === 'ALL'
                  ? 'border-brand-700 bg-brand-50 text-brand-800'
                  : 'border-line bg-surface text-slate-600 hover:bg-slate-100'
              }`}
            >
              {t('formula.filterAll')} ({constructionTypes.length})
            </button>

            {CATEGORIES.map((cat) => (
              <button
                key={cat}
                type="button"
                onClick={() => setCategoryFilter(cat)}
                className={`rounded-xl border px-3 py-1.5 text-xs font-bold ${
                  categoryFilter === cat
                    ? 'border-brand-700 bg-brand-50 text-brand-800'
                    : 'border-line bg-surface text-slate-600 hover:bg-slate-100'
                }`}
              >
                {t(`formula.category.${cat}`)} ({categoryCounts[cat]})
              </button>
            ))}
          </div>

          {workspaceQuery.isError ? (
            <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs font-semibold text-rose-800">
              {extractProblemValidation(workspaceQuery.error, t('formula.loadFailed')).summary}
            </div>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-line">
              <table className="w-full text-left text-xs">
                <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
                  <tr>
                    <th className="px-3 py-2.5">#</th>
                    <th className="px-3 py-2.5">{t('formula.colCode')}</th>
                    <th className="px-3 py-2.5">{t('formula.colTypeName')}</th>
                    <th className="px-3 py-2.5">{t('formula.colCategory')}</th>
                    <th className="px-3 py-2.5 text-right">{t('formula.colLandRate')}</th>
                    <th className="px-3 py-2.5 text-right">{t('formula.colRrRate')}</th>
                    <th className="px-3 py-2.5 text-right">{t('formula.colWeight')}</th>
                    <th className="px-3 py-2.5 text-right">{t('formula.colTaxRate')}</th>
                    <th className="px-3 py-2.5">{t('formula.colStatus')}</th>
                    <th className="px-3 py-2.5 text-right">{t('formula.colActions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line bg-card">
                  {filteredConstructionTypes.map((item, idx) => (
                    <tr key={item.id} className="hover:bg-slate-50/70">
                      <td className="px-3 py-2.5 mono text-muted">{idx + 1}</td>
                      <td className="px-3 py-2.5 font-bold mono text-ink">{item.code}</td>
                      <td className="px-3 py-2.5">
                        <div className="font-semibold text-ink">{item.typeName}</div>
                        {item.lastResolutionNo && (
                          <div className="text-[10.5px] text-muted">
                            {t('formula.resTag')}: {item.lastResolutionNo}
                            {item.lastResolutionDate ? ` (${item.lastResolutionDate})` : ''}
                          </div>
                        )}
                      </td>
                      <td className="px-3 py-2.5">
                        <span
                          className={`inline-block rounded-full border px-2 py-0.5 text-[10.5px] font-bold ${
                            CATEGORY_BADGE_STYLES[item.category] ?? 'bg-slate-100 text-slate-700 border-slate-200'
                          }`}
                        >
                          {t(`formula.category.${item.category}`)}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-right mono font-semibold text-ink">
                        {formatInr(item.landAnnualValueRate)}
                      </td>
                      <td className="px-3 py-2.5 text-right mono font-bold text-brand-800">
                        {formatInr(item.constructionReadyReckonerRate)}
                      </td>
                      <td className="px-3 py-2.5 text-right mono text-slate-700">
                        {item.usageWeightageFactor.toFixed(2)}
                      </td>
                      <td className="px-3 py-2.5 text-right mono font-semibold text-ink">
                        {item.taxRate.toFixed(2)}
                      </td>
                      <td className="px-3 py-2.5">
                        <span
                          className={`inline-block rounded-full px-2 py-0.5 text-[10.5px] font-bold ${
                            item.isActive
                              ? 'bg-emerald-50 text-emerald-800 border border-emerald-200'
                              : 'bg-slate-100 text-slate-600 border border-slate-200'
                          }`}
                        >
                          {item.isActive ? t('formula.active') : t('formula.inactive')}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-right">
                        <div className="inline-flex items-center gap-1.5">
                          <Link
                            to={`/audit/ConstructionTypeMaster/${item.id}`}
                            className="inline-flex items-center gap-1 rounded-lg border border-line bg-surface px-2 py-1 text-[11px] font-bold text-slate-700 hover:bg-brand-50 hover:text-brand-800"
                            title={t('properties.auditHistory')}
                          >
                            <History className="h-3 w-3" />
                          </Link>
                          {canWrite && (
                            <button
                              type="button"
                              onClick={() => openConstructionEditor(item)}
                              className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-800 hover:bg-brand-100"
                            >
                              <Edit3 className="h-3 w-3" />
                              {t('formula.editRowBtn')}
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                  {filteredConstructionTypes.length === 0 && (
                    <tr>
                      <td colSpan={10} className="px-4 py-8 text-center text-xs text-muted">
                        {t('formula.emptyFilter')}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : activeTab === 'depreciation' ? (
        <DepreciationSlabsTab
          key={`dep-${(workspaceQuery.data?.depreciationSlabs ?? [])
            .map((s) => `${s.id}:${s.ageFrom}-${s.ageTo}:${s.hutMudPercent}:${s.stoneBrickRccPercent}`)
            .join('|')}`}
          slabs={workspaceQuery.data?.depreciationSlabs ?? []}
          canWrite={canWrite}
          onSaved={async (msg) => {
            setToastMessage(msg);
            await refreshWorkspace();
          }}
        />
      ) : activeTab === 'usage' ? (
        <UsageWeightagesTab
          items={workspaceQuery.data?.usageWeightages ?? []}
          canWrite={canWrite}
          onSaved={async (msg) => {
            setToastMessage(msg);
            await refreshWorkspace();
          }}
        />
      ) : activeTab === 'areaSlabs' ? (
        <AreaSlabTaxesTab
          key={`area-${(workspaceQuery.data?.areaSlabTaxes ?? [])
            .map((s) => `${s.id}:${s.areaFrom}-${s.areaTo}:${s.lightingTax}:${s.healthTax}:${s.sanitationTax}:${s.toiletTax}`)
            .join('|')}`}
          slabs={workspaceQuery.data?.areaSlabTaxes ?? []}
          canWrite={canWrite}
          onSaved={async (msg) => {
            setToastMessage(msg);
            await refreshWorkspace();
          }}
        />
      ) : (
        <WaterTaxMastersTab
          items={workspaceQuery.data?.waterTaxes ?? []}
          canWrite={canWrite}
          onSaved={async (msg) => {
            setToastMessage(msg);
            await refreshWorkspace();
          }}
        />
      )}

      {/* Interactive Live Tenant-Master Tax Simulator */}
      {workspaceQuery.data && (
        <FormulaSimulatorCard
          constructionTypes={constructionTypes}
          usageWeightages={workspaceQuery.data.usageWeightages ?? []}
          waterTaxes={workspaceQuery.data.waterTaxes ?? []}
        />
      )}

      {/* Formula Setting Drawer / Modal */}
      {settingsDrawerOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={t('formula.editSettingsBtn')}
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 overflow-y-auto"
        >
          <div className="w-full max-w-2xl rounded-2xl border border-line bg-card p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between border-b border-line pb-3">
              <div>
                <h2 className="text-base font-extrabold text-ink">{t('formula.settingsModalTitle')}</h2>
                <p className="text-[11px] text-muted">{t('formula.settingsModalSubtitle')}</p>
              </div>
              <button
                type="button"
                onClick={() => setSettingsDrawerOpen(false)}
                className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100"
                aria-label={t('properties.cancel')}
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <ValidationSummary summary={settingsError} items={settingsErrorItems} />

            <form onSubmit={(e) => void handleSaveSettings(e)} noValidate className="space-y-4 text-xs">
              <div className="grid gap-3 sm:grid-cols-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>
                    {t('formula.ratePeriodName')} <span className="text-rose-600">*</span>
                  </span>
                  <input
                    type="text"
                    value={settingsForm.ratePeriodName}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, ratePeriodName: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.rateStartYear')}</span>
                  <input
                    type="number"
                    value={settingsForm.rateStartYear}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, rateStartYear: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.rateEndYear')}</span>
                  <input
                    type="number"
                    value={settingsForm.rateEndYear}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, rateEndYear: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>

              <div className="grid gap-3 sm:grid-cols-4">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.taxDivisor')}</span>
                  <input
                    type="number"
                    step="0.01"
                    min="1"
                    value={settingsForm.taxDivisor}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, taxDivisor: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.noticeGraceDays')}</span>
                  <input
                    type="number"
                    min="1"
                    max="365"
                    value={settingsForm.noticeGraceDays}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, noticeGraceDays: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.earlyRebatePct')}</span>
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="100"
                    value={settingsForm.earlyRebatePct}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, earlyRebatePct: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.latePenaltyPct')}</span>
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="100"
                    value={settingsForm.latePenaltyPct}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, latePenaltyPct: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>

              <div className="flex flex-wrap gap-5 rounded-xl border border-line bg-surface p-3">
                <label className="inline-flex items-center gap-2 font-semibold text-slate-700 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={settingsForm.applyLightingHealthOnOpenPlot}
                    onChange={(e) =>
                      setSettingsForm((p) => ({ ...p, applyLightingHealthOnOpenPlot: e.target.checked }))
                    }
                    className="h-4 w-4 rounded border-slate-300 text-brand-700"
                  />
                  <span>{t('formula.applyLightingOnOpenPlot')}</span>
                </label>
                <label className="inline-flex items-center gap-2 font-semibold text-slate-700 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={settingsForm.roundArea}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, roundArea: e.target.checked }))}
                    className="h-4 w-4 rounded border-slate-300 text-brand-700"
                  />
                  <span>{t('formula.roundAreaLabel')}</span>
                </label>
              </div>

              <div className="grid gap-3 sm:grid-cols-2 border-t border-line pt-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>
                    {t('formula.resolutionNoLabel')} <span className="text-rose-600">*</span>
                  </span>
                  <input
                    type="text"
                    placeholder={t('formula.resolutionNoPlaceholder')}
                    value={settingsForm.resolutionNo}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, resolutionNo: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionDateLabel')}</span>
                  <input
                    type="date"
                    value={settingsForm.resolutionDate ?? ''}
                    onChange={(e) => setSettingsForm((p) => ({ ...p, resolutionDate: e.target.value || null }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>

              <label className="block space-y-1 font-bold text-slate-700">
                <span>
                  {t('formula.resolutionReasonLabel')} <span className="text-rose-600">*</span>
                </span>
                <textarea
                  rows={2}
                  placeholder={t('formula.changeReasonPlaceholder')}
                  value={settingsForm.resolutionReason}
                  onChange={(e) => setSettingsForm((p) => ({ ...p, resolutionReason: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setSettingsDrawerOpen(false)}
                  className="rounded-xl border border-slate-300 px-4 py-2 font-bold text-slate-700 hover:bg-slate-50"
                >
                  {t('properties.cancel')}
                </button>
                <button
                  type="submit"
                  disabled={savingSettings}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
                >
                  <Save className="h-3.5 w-3.5" />
                  {savingSettings ? t('onboarding.submitting') : t('formula.saveSettingsSubmit')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Construction Type Add/Edit Modal */}
      {constructionDrawerOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={constructionForm.id ? t('formula.editRowBtn') : t('formula.addConstructionBtn')}
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 overflow-y-auto"
        >
          <div className="w-full max-w-2xl rounded-2xl border border-line bg-card p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between border-b border-line pb-3">
              <div>
                <h2 className="text-base font-extrabold text-ink">
                  {constructionForm.id ? t('formula.editConstructionTitle') : t('formula.addConstructionTitle')}
                </h2>
                <p className="text-[11px] text-muted">{t('formula.constructionSubtitle')}</p>
              </div>
              <button
                type="button"
                onClick={() => setConstructionDrawerOpen(false)}
                className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100"
                aria-label={t('properties.cancel')}
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <ValidationSummary summary={constructionError} items={constructionErrorItems} />

            <form onSubmit={(e) => void handleSaveConstructionType(e)} noValidate className="space-y-4 text-xs">
              <div className="grid gap-3 sm:grid-cols-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>
                    {t('formula.colCode')} <span className="text-rose-600">*</span>
                  </span>
                  <input
                    type="text"
                    value={constructionForm.code}
                    onChange={(e) =>
                      setConstructionForm((p) => ({ ...p, code: e.target.value.toUpperCase() }))
                    }
                    placeholder="HUT_MUD"
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>

                <label className="space-y-1 font-bold text-slate-700 sm:col-span-2">
                  <span>
                    {t('formula.colTypeName')} <span className="text-rose-600">*</span>
                  </span>
                  <input
                    type="text"
                    value={constructionForm.typeName}
                    onChange={(e) => setConstructionForm((p) => ({ ...p, typeName: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2"
                  />
                </label>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colCategory')}</span>
                  <select
                    value={constructionForm.category}
                    onChange={(e) =>
                      setConstructionForm((p) => ({
                        ...p,
                        category: e.target.value as ConstructionCategory,
                      }))
                    }
                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                  >
                    {CATEGORIES.map((cat) => (
                      <option key={cat} value={cat}>
                        {t(`formula.category.${cat}`)}
                      </option>
                    ))}
                  </select>
                </label>

                <label className="flex items-center gap-2 pt-5 font-bold text-slate-700 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={constructionForm.isActive}
                    onChange={(e) => setConstructionForm((p) => ({ ...p, isActive: e.target.checked }))}
                    className="h-4 w-4 rounded border-slate-300 text-brand-700"
                  />
                  <span>{t('formula.active')}</span>
                </label>
              </div>

              <div className="grid gap-3 sm:grid-cols-4">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colLandRate')}</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={constructionForm.landAnnualValueRate}
                    onChange={(e) =>
                      setConstructionForm((p) => ({ ...p, landAnnualValueRate: Number(e.target.value) }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colRrRate')}</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={constructionForm.constructionReadyReckonerRate}
                    onChange={(e) =>
                      setConstructionForm((p) => ({
                        ...p,
                        constructionReadyReckonerRate: Number(e.target.value),
                      }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colWeight')}</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={constructionForm.usageWeightageFactor}
                    onChange={(e) =>
                      setConstructionForm((p) => ({ ...p, usageWeightageFactor: Number(e.target.value) }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colTaxRate')}</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={constructionForm.taxRate}
                    onChange={(e) =>
                      setConstructionForm((p) => ({ ...p, taxRate: Number(e.target.value) }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>

              <div className="grid gap-3 sm:grid-cols-2 border-t border-line pt-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionNoLabel')}</span>
                  <input
                    type="text"
                    placeholder={t('formula.resolutionNoPlaceholder')}
                    value={constructionForm.resolutionNo ?? ''}
                    onChange={(e) => setConstructionForm((p) => ({ ...p, resolutionNo: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionDateLabel')}</span>
                  <input
                    type="date"
                    value={constructionForm.resolutionDate ?? ''}
                    onChange={(e) =>
                      setConstructionForm((p) => ({ ...p, resolutionDate: e.target.value || null }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>

              <label className="block space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionReasonLabel')}</span>
                <input
                  type="text"
                  placeholder={t('formula.changeReasonPlaceholder')}
                  value={constructionForm.changeReason ?? ''}
                  onChange={(e) => setConstructionForm((p) => ({ ...p, changeReason: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setConstructionDrawerOpen(false)}
                  className="rounded-xl border border-slate-300 px-4 py-2 font-bold text-slate-700 hover:bg-slate-50"
                >
                  {t('properties.cancel')}
                </button>
                <button
                  type="submit"
                  disabled={savingConstruction}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
                >
                  <Save className="h-3.5 w-3.5" />
                  {savingConstruction ? t('onboarding.submitting') : t('formula.saveRowBtn')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

