import { useQuery } from '@tanstack/react-query';
import {
  Activity,
  AlertCircle,
  ArrowRight,
  Building2,
  Calculator,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Droplets,
  FilePlus2,
  FileSpreadsheet,
  GitBranch,
  History,
  IndianRupee,
  Landmark,
  ReceiptText,
  Search,
  Sparkles,
  X,
} from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { sprint2Api, type TaxPreviewRequest } from '../../services/sprint2Api';
import { formatDateTime, formatInr } from '../../utils/formatters';

interface ConstructionPreset {
  code: string;
  category: 'HUT_MUD' | 'STONE_BRICK_RCC' | 'OPEN_LAND';
  landRate: number;
  rrRate: number;
  taxRate: number;
}

const CONSTRUCTION_PRESETS: ConstructionPreset[] = [
  { code: 'HUT_MUD', category: 'HUT_MUD', landRate: 760, rrRate: 5852, taxRate: 0.5 },
  { code: 'STONE_BRICK_MUD', category: 'STONE_BRICK_RCC', landRate: 760, rrRate: 10032, taxRate: 0.8 },
  { code: 'STONE_LIME_SEMI_PUCCA', category: 'STONE_BRICK_RCC', landRate: 758, rrRate: 14012, taxRate: 1.0 },
  { code: 'RCC_PUCCA', category: 'STONE_BRICK_RCC', landRate: 760, rrRate: 18392, taxRate: 1.5 },
  { code: 'CATTLE_SHED', category: 'HUT_MUD', landRate: 760, rrRate: 5852, taxRate: 0.5 },
  { code: 'TIN_SHED', category: 'HUT_MUD', landRate: 760, rrRate: 5852, taxRate: 0.5 },
  { code: 'OPEN_PLOT', category: 'OPEN_LAND', landRate: 760, rrRate: 0, taxRate: 2.0 },
  { code: 'OTHER_COMMERCIAL', category: 'STONE_BRICK_RCC', landRate: 760, rrRate: 18392, taxRate: 1.5 },
];

const USAGE_WEIGHTS = [
  { code: 'RESIDENTIAL', weight: 1.0 },
  { code: 'INDUSTRIAL', weight: 1.2 },
  { code: 'COMMERCIAL', weight: 1.25 },
];

function buildSlabs(category: ConstructionPreset['category']) {
  const isRcc = category === 'STONE_BRICK_RCC';
  return [
    { ageFrom: 0, ageTo: 2, retentionPercentage: 100 },
    { ageFrom: 2, ageTo: 5, retentionPercentage: 95 },
    { ageFrom: 5, ageTo: 10, retentionPercentage: isRcc ? 90 : 85 },
    { ageFrom: 10, ageTo: 20, retentionPercentage: isRcc ? 80 : 75 },
    { ageFrom: 20, ageTo: 30, retentionPercentage: isRcc ? 70 : 60 },
    { ageFrom: 30, ageTo: 40, retentionPercentage: isRcc ? 60 : 45 },
    { ageFrom: 40, ageTo: 50, retentionPercentage: isRcc ? 50 : 30 },
    { ageFrom: 50, ageTo: 60, retentionPercentage: isRcc ? 40 : 20 },
    { ageFrom: 60, ageTo: 120, retentionPercentage: isRcc ? 30 : 15 },
  ];
}

function buildAreaSlabs(areaSqFt: number) {
  if (areaSqFt <= 0) {
    return { lightingTax: 0, healthTax: 0, sanitationTax: 0, toiletTax: 0 };
  }
  if (areaSqFt <= 300) {
    return { lightingTax: 40, healthTax: 40, sanitationTax: 100, toiletTax: 0 };
  }
  if (areaSqFt <= 700) {
    return { lightingTax: 50, healthTax: 50, sanitationTax: 100, toiletTax: 20 };
  }
  return { lightingTax: 100, healthTax: 100, sanitationTax: 100, toiletTax: 0 };
}

export function VillageDashboard() {
  const { t, i18n } = useTranslation();
  const { user, accessToken, isBootstrapping } = useAuth();
  const searchInputRef = useRef<HTMLInputElement>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [checklistExpanded, setChecklistExpanded] = useState(true);

  // Authoritative Server Capital Value Simulator state
  const [constructionCode, setConstructionCode] = useState('HUT_MUD');
  const [usageCode, setUsageCode] = useState('RESIDENTIAL');
  const [lengthFt, setLengthFt] = useState(15);
  const [widthFt, setWidthFt] = useState(15);
  const [landRate, setLandRate] = useState(760);
  const [rrRate, setRrRate] = useState(5852);
  const [ageYears, setAgeYears] = useState(5);
  const [taxRate, setTaxRate] = useState(0.5);
  const [waterTax, setWaterTax] = useState(0);

  const canQueryTenantDashboard = Boolean(accessToken && user?.activeTenantId);

  const summaryQuery = useQuery({
    queryKey: ['dashboard', 'summary', user?.activeTenantId],
    queryFn: sprint2Api.getDashboardSummary,
    enabled: canQueryTenantDashboard,
  });

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchTerm.trim());
    }, 320);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  const searchQuery = useQuery({
    queryKey: ['dashboard', 'search', user?.activeTenantId, debouncedSearch],
    queryFn: () => sprint2Api.searchDashboard(debouncedSearch),
    enabled: canQueryTenantDashboard && debouncedSearch.length >= 2,
  });

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      const target = e.target as HTMLElement | null;
      const isInput =
        target?.tagName === 'INPUT' ||
        target?.tagName === 'TEXTAREA' ||
        target?.tagName === 'SELECT';
      if (e.key === '/' && !isInput) {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const taxRequest: TaxPreviewRequest = useMemo(() => {
    const preset =
      CONSTRUCTION_PRESETS.find((c) => c.code === constructionCode) ?? CONSTRUCTION_PRESETS[0];
    const usage = USAGE_WEIGHTS.find((u) => u.code === usageCode) ?? USAGE_WEIGHTS[0];
    const safeLen = Math.max(0, Number(lengthFt) || 0);
    const safeWidth = Math.max(0, Number(widthFt) || 0);
    const areaSqFt = safeLen * safeWidth;
    const isOpenLand = preset.category === 'OPEN_LAND';

    return {
      assessmentStartYear: 2026,
      floors: isOpenLand
        ? []
        : [
            {
              lengthFt: safeLen,
              widthFt: safeWidth,
              areaSqm: null,
              landAnnualValueRate: Math.max(0, Number(landRate) || 0),
              constructionReadyReckonerRate: Math.max(0, Number(rrRate) || 0),
              usageWeightage: usage.weight,
              taxRate: Math.max(0, Number(taxRate) || 0),
              constructionStartYear: Math.max(1900, 2026 - Math.max(0, Math.round(ageYears) || 0)),
            },
          ],
      depreciationSlabs: buildSlabs(preset.category),
      areaSlabTaxes: buildAreaSlabs(areaSqFt),
      waterTax: Math.max(0, Number(waterTax) || 0),
      otherTax: 0,
      openAreaSqm: isOpenLand ? Math.round(areaSqFt * 0.09290304 * 100) / 100 : 0,
      openPlotLandRate: isOpenLand ? Math.max(0, Number(landRate) || 0) : 0,
      openPlotTaxRate: isOpenLand ? Math.max(0, Number(taxRate) || 0) : 0,
      taxDivisor: 1000,
      roundAreaToTwoDecimals: true,
    };
  }, [constructionCode, usageCode, lengthFt, widthFt, landRate, rrRate, ageYears, taxRate, waterTax]);

  const taxPreviewQuery = useQuery({
    queryKey: ['tax', 'preview', taxRequest],
    queryFn: () => sprint2Api.previewCapitalTax(taxRequest),
    staleTime: 1000 * 30,
  });

  function applyConstructionPreset(code: string) {
    setConstructionCode(code);
    const found = CONSTRUCTION_PRESETS.find((c) => c.code === code);
    if (found) {
      setLandRate(found.landRate);
      setRrRate(found.rrRate);
      setTaxRate(found.taxRate);
    }
  }

  function loadGoldenVector() {
    setConstructionCode('HUT_MUD');
    setUsageCode('RESIDENTIAL');
    setLengthFt(15);
    setWidthFt(15);
    setLandRate(760);
    setRrRate(5852);
    setAgeYears(5);
    setTaxRate(0.5);
    setWaterTax(0);
  }

  const summary = summaryQuery.data;

  const kpiCards = [
    {
      label: t('dashboard.kpiProperties'),
      value: summary ? String(summary.totalProperties) : '—',
      icon: Building2,
      accent: 'text-brand-700 bg-brand-50 border-brand-200',
    },
    {
      label: t('dashboard.kpiDemand'),
      value: summary ? formatInr(summary.currentFyDemand) : '₹ 0.00',
      icon: Landmark,
      accent: 'text-indigo-700 bg-indigo-50 border-indigo-200',
    },
    {
      label: t('dashboard.kpiCollection'),
      value: summary ? formatInr(summary.todayCollection) : '₹ 0.00',
      sub: summary ? `${t('dashboard.kpiMtdPrefix')}: ${formatInr(summary.monthCollection)}` : undefined,
      icon: IndianRupee,
      accent: 'text-emerald-700 bg-emerald-50 border-emerald-200',
    },
    {
      label: t('dashboard.kpiTodayReceipts'),
      value: summary ? String(summary.todayReceipts) : '0',
      icon: ReceiptText,
      accent: 'text-teal-700 bg-teal-50 border-teal-200',
    },
    {
      label: t('dashboard.kpiArrears'),
      value: summary ? formatInr(summary.outstandingArrears) : '₹ 0.00',
      icon: AlertCircle,
      accent: 'text-amber-700 bg-amber-50 border-amber-200',
    },
    {
      label: t('dashboard.kpiWater'),
      value: summary ? formatInr(summary.waterAndSpecialCollection) : '₹ 0.00',
      sub: summary ? `${t('dashboard.kpiMutations')}: ${summary.pendingMutations}` : undefined,
      icon: Droplets,
      accent: 'text-sky-700 bg-sky-50 border-sky-200',
    },
  ];

  const quickActions = [
    { label: t('dashboard.actionNamuna8'), to: '/properties', icon: FilePlus2, badge: null },
    { label: t('dashboard.actionOnboarding'), to: '/onboarding', icon: Landmark, badge: null },
    { label: t('dashboard.actionAudit'), to: '/audit', icon: History, badge: null },
    { label: t('dashboard.actionBills'), to: '/namuna-9ka/generate', icon: FileSpreadsheet, badge: t('dashboard.sprintBadge', { n: 4 }) },
    { label: t('dashboard.actionCollect'), to: '/namuna-10/collect', icon: ReceiptText, badge: t('dashboard.sprintBadge', { n: 5 }) },
    { label: t('dashboard.actionMutation'), to: '/namuna-8/mutation/new', icon: GitBranch, badge: t('dashboard.sprintBadge', { n: 3 }) },
  ];

  const taxResult = taxPreviewQuery.data;
  const firstFloor = taxResult?.floorBreakdowns[0];
  const areaSqFtPreview = Math.max(0, Number(lengthFt) || 0) * Math.max(0, Number(widthFt) || 0);
  const slabTotal = taxResult
    ? taxResult.lightingTax + taxResult.healthTax + taxResult.sanitationTax + taxResult.toiletTax
    : 0;

  return (
    <div className="space-y-6">
      {/* Executive Teal Hero Banner with Global Search */}
      <section className="rounded-2xl bg-gradient-to-r from-brand-800 via-brand-700 to-brand-600 p-6 text-white shadow-sm space-y-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <p className="text-xs font-semibold text-brand-100">{t('dashboard.greeting')}</p>
            <h1 className="text-2xl font-extrabold sm:text-3xl text-white">
              {summary
                ? i18n.language === 'mr'
                  ? `${summary.panchayatNameMr} (${summary.panchayatNameEn})`
                  : `${summary.panchayatNameEn} (${summary.panchayatNameMr})`
                : t('dashboard.title')}
            </h1>
            <p className="text-xs sm:text-sm text-brand-100">
              {summary ? (
                <span className="inline-flex flex-wrap items-center gap-2">
                  <span className="mono rounded-md bg-white/15 px-2 py-0.5 text-xs font-bold text-white">
                    {t('dashboard.lgdPrefix')} {summary.lgdCode}
                  </span>
                  <span>·</span>
                  <span>
                    {t('dashboard.ayBadge')} {summary.activeAssessmentYear}
                  </span>
                </span>
              ) : (
                t('dashboard.lead')
              )}
            </p>
          </div>

          {/* Global Property & Citizen Search */}
          <div className="relative w-full sm:w-80 lg:w-96">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-brand-200" />
            <input
              ref={searchInputRef}
              type="text"
              aria-label={t('dashboard.searchPlaceholder')}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder={t('dashboard.searchPlaceholder')}
              className="w-full rounded-xl border border-white/25 bg-white/15 pl-9 pr-14 py-2.5 text-xs font-medium text-white placeholder:text-brand-100/80 focus:bg-white focus:text-ink focus:placeholder:text-slate-400 focus:outline-none transition"
            />
            {searchTerm ? (
              <button
                type="button"
                aria-label={t('dashboard.clearSearch')}
                onClick={() => {
                  setSearchTerm('');
                  setDebouncedSearch('');
                }}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-white/80 hover:bg-white/20"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            ) : (
              <kbd className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 rounded border border-white/30 bg-white/10 px-1.5 py-0.5 mono text-[10px] text-white">
                /
              </kbd>
            )}
          </div>
        </div>

        {/* Search Results Panel inside Hero */}
        {canQueryTenantDashboard && debouncedSearch.length >= 2 && (
          <div className="rounded-xl border border-white/20 bg-white text-ink p-4 text-xs shadow-md space-y-3">
            {searchQuery.isFetching && (
              <p className="text-muted animate-pulse">{t('onboarding.loading')}</p>
            )}
            {searchQuery.isError && (
              <p className="font-semibold text-rose-700">{t('dashboard.searchError')}</p>
            )}
            {searchQuery.data && (
              <>
                <p className="font-bold text-brand-800">
                  {t('dashboard.searchHits', {
                    props: searchQuery.data.properties.length,
                    citizens: searchQuery.data.citizens.length,
                  })}
                </p>
                {searchQuery.data.properties.length === 0 &&
                searchQuery.data.citizens.length === 0 ? (
                  <p className="text-muted">
                    {t('dashboard.searchNoHits', { q: debouncedSearch })}
                  </p>
                ) : (
                  <div className="grid gap-2.5 sm:grid-cols-2">
                    {searchQuery.data.properties.map((p) => (
                      <Link
                        key={p.id}
                        to="/properties"
                        className="flex items-center justify-between rounded-lg border border-brand-200 bg-brand-50/50 p-2.5 hover:bg-brand-100/70 transition"
                      >
                        <div>
                          <span className="mono font-bold text-brand-800">
                            {p.wardNo} / {p.propertyNo}
                          </span>{' '}
                          <span className="text-slate-600">
                            · {p.totalAreaSqFt} {t('dashboard.sqFtUnit')} ({p.waterConnectionCode})
                          </span>
                        </div>
                        <ArrowRight className="h-3.5 w-3.5 text-brand-700" />
                      </Link>
                    ))}
                    {searchQuery.data.citizens.map((c) => (
                      <div
                        key={c.id}
                        className="rounded-lg border border-line bg-surface p-2.5"
                      >
                        <p className="font-bold text-ink">
                          {c.fullNameMr} ({c.fullNameEn})
                        </p>
                        <p className="mono text-[11px] text-muted">
                          {c.citizenCode}
                          {c.wardNo ? ` · ${c.wardNo}` : ''}
                          {c.aadhaarMasked ? ` · ${c.aadhaarMasked}` : ''}
                        </p>
                      </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        )}
      </section>

      {/* Sign-In Notice (only after session hydration completes cleanly) */}
      {!isBootstrapping && !canQueryTenantDashboard && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-xs text-amber-950 shadow-xs">
          <span className="font-medium">{t('dashboard.signInPrompt')}</span>
          <div className="flex flex-wrap gap-2">
            <Link
              to="/login"
              className="rounded-lg bg-brand-700 px-3.5 py-2 font-bold text-white hover:bg-brand-800 transition"
            >
              {t('nav.login')}
            </Link>
            <Link
              to="/onboarding"
              className="rounded-lg border border-brand-700 bg-white px-3.5 py-2 font-bold text-brand-800 hover:bg-brand-50 transition"
            >
              {t('nav.onboarding')}
            </Link>
          </div>
        </div>
      )}

      {/* System Setup Checklist Banner (Interactive & Collapsible) */}
      {summary && (
        <section className="rounded-2xl border border-brand-200 bg-gradient-to-br from-brand-50/70 via-white to-teal-50/40 p-5 shadow-xs space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-sm font-extrabold text-ink">{t('dashboard.checklistTitle')}</h2>
                <span className="rounded-full bg-brand-700 px-2.5 py-0.5 mono text-[11px] font-bold text-white">
                  {t('dashboard.progressLabel', { pct: summary.setupProgressPct })}
                </span>
              </div>
              <p className="mt-0.5 text-xs text-muted">{t('dashboard.checklistLead')}</p>
            </div>

            <div className="flex items-center gap-2">
              <Link
                to="/onboarding"
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-brand-800 transition"
              >
                <span>{t('dashboard.openWizardBtn')}</span>
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
              <button
                type="button"
                aria-expanded={checklistExpanded}
                onClick={() => setChecklistExpanded((v) => !v)}
                className="rounded-lg border border-line bg-white p-2 text-slate-600 hover:bg-slate-100"
              >
                {checklistExpanded ? (
                  <ChevronUp className="h-4 w-4" />
                ) : (
                  <ChevronDown className="h-4 w-4" />
                )}
              </button>
            </div>
          </div>

          <div className="h-2 w-full overflow-hidden rounded-full bg-slate-200">
            <div
              className="h-full rounded-full bg-gradient-to-r from-brand-700 to-brand-500 transition-all duration-300"
              style={{ width: `${Math.min(100, Math.max(0, summary.setupProgressPct))}%` }}
            />
          </div>

          {checklistExpanded && (
            <div className="grid gap-2.5 sm:grid-cols-2 lg:grid-cols-5">
              {summary.checklistSteps.map((step) => (
                <Link
                  key={step.stepNumber}
                  to={`/onboarding?step=${step.stepNumber}`}
                  className={`group flex flex-col justify-between rounded-xl border p-3 text-xs transition ${
                    step.isCompleted
                      ? 'border-emerald-200 bg-white hover:border-emerald-400'
                      : 'border-amber-200 bg-amber-50/50 hover:border-amber-400'
                  }`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="mono text-[11px] font-bold text-muted">
                      #{step.stepNumber}
                    </span>
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold ${
                        step.isCompleted
                          ? 'bg-emerald-100 text-emerald-800'
                          : 'bg-amber-100 text-amber-900'
                      }`}
                    >
                      {step.isCompleted && <CheckCircle2 className="h-3 w-3" />}
                      {step.isCompleted ? t('dashboard.done') : t('dashboard.pending')}
                    </span>
                  </div>
                  <p className="mt-2 font-bold text-ink group-hover:text-brand-700">
                    {t(`dashboard.checklistSteps.${step.stepNumber}`)}
                  </p>
                </Link>
              ))}
            </div>
          )}
        </section>
      )}

      {/* KPI Metrics Grid */}
      {(summaryQuery.isLoading || isBootstrapping) && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3, 4, 5, 6].map((n) => (
            <div key={n} className="h-24 animate-pulse rounded-2xl border border-line bg-white" />
          ))}
        </div>
      )}

      {summaryQuery.error instanceof ApiProblemError && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs font-medium text-rose-800">
          {summaryQuery.error.message}
        </div>
      )}

      {!summaryQuery.isLoading && !isBootstrapping && (
        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {kpiCards.map((kpi) => {
            const Icon = kpi.icon;
            return (
              <div
                key={kpi.label}
                className="flex items-center justify-between gap-3 rounded-2xl border border-line bg-card p-5 shadow-xs transition hover:border-brand-300"
              >
                <div className="min-w-0">
                  <p className="text-xs font-semibold text-muted">{kpi.label}</p>
                  <p className="mt-1 mono text-2xl font-extrabold text-ink truncate">
                    {kpi.value}
                  </p>
                  {kpi.sub && <p className="mt-0.5 mono text-[11px] text-muted">{kpi.sub}</p>}
                </div>
                <div
                  className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl border ${kpi.accent}`}
                >
                  <Icon className="h-5 w-5" />
                </div>
              </div>
            );
          })}
        </section>
      )}

      {/* Quick Actions */}
      <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-3">
        <p className="text-xs font-bold uppercase tracking-wider text-muted">
          {t('dashboard.quickActionsTitle')}
        </p>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {quickActions.map((act) => {
            const Icon = act.icon;
            return (
              <Link
                key={act.to}
                to={act.to}
                className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface px-4 py-3 text-xs font-bold text-ink transition hover:border-brand-500 hover:bg-brand-50 hover:text-brand-800"
              >
                <span className="flex items-center gap-3 min-w-0">
                  <Icon className="h-5 w-5 shrink-0 text-brand-700" />
                  <span className="truncate">{act.label}</span>
                </span>
                {act.badge && (
                  <span className="shrink-0 rounded-full bg-slate-200 px-2 py-0.5 text-[10px] font-semibold text-slate-700">
                    {act.badge}
                  </span>
                )}
              </Link>
            );
          })}
        </div>
      </section>

      {/* Live Audit Activity Timeline */}
      {summary && (
        <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Activity className="h-4 w-4 text-brand-700" />
              <h2 className="text-sm font-extrabold text-ink">
                {t('dashboard.recentActivityTitle')}
              </h2>
            </div>
            <Link to="/audit" className="text-xs font-bold text-brand-700 hover:underline">
              {t('nav.audit')} →
            </Link>
          </div>
          {summary.recentActivity.length === 0 ? (
            <p className="text-xs text-muted">{t('dashboard.noActivity')}</p>
          ) : (
            <div className="max-h-64 divide-y divide-slate-100 overflow-y-auto rounded-xl border border-line">
              {summary.recentActivity.map((ev) => (
                <div
                  key={ev.id}
                  className="flex flex-wrap items-center justify-between gap-2 bg-white px-4 py-2.5 text-xs hover:bg-surface"
                >
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-ink">{ev.entityName}</span>
                    <span className="rounded bg-brand-100 px-1.5 py-0.5 mono text-[10px] font-bold text-brand-800">
                      {ev.action}
                    </span>
                    {ev.actorRole && <span className="text-[11px] text-muted">{ev.actorRole}</span>}
                  </div>
                  <span className="mono text-[11px] text-muted">
                    {formatDateTime(ev.timestampUtc, i18n.language)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {/* Authoritative Server ADR-016 Capital Value Tax Simulator */}
      <section className="rounded-2xl border border-brand-200 bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-50 text-brand-700 border border-brand-200">
              <Calculator className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-extrabold text-ink">{t('dashboard.taxPreviewTitle')}</h2>
              <p className="text-xs text-brand-800">{t('dashboard.goldenHint')}</p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={loadGoldenVector}
              className="inline-flex items-center gap-1.5 rounded-lg border border-brand-300 bg-brand-50 px-3 py-1.5 text-xs font-bold text-brand-800 hover:bg-brand-100 transition"
            >
              <Sparkles className="h-3.5 w-3.5" />
              {t('dashboard.loadGoldenBtn')}
            </button>
            <span className="rounded-full bg-brand-700 px-3 py-1 text-[11px] font-bold text-white">
              {t('dashboard.taxPreviewBadge')}
            </span>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label className="text-xs font-bold text-slate-700 space-y-1">
            <span>{t('dashboard.constructionType')}</span>
            <select
              value={constructionCode}
              onChange={(e) => applyConstructionPreset(e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold"
            >
              {CONSTRUCTION_PRESETS.map((c) => (
                <option key={c.code} value={c.code}>
                  {t(`dashboard.constructions.${c.code}`)}
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs font-bold text-slate-700 space-y-1">
            <span>{t('dashboard.usageType')}</span>
            <select
              value={usageCode}
              onChange={(e) => setUsageCode(e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold"
            >
              {USAGE_WEIGHTS.map((u) => (
                <option key={u.code} value={u.code}>
                  {t(`dashboard.usages.${u.code}`)}
                </option>
              ))}
            </select>
          </label>

          <div className="grid grid-cols-2 gap-2">
            <label className="text-xs font-bold text-slate-700 space-y-1">
              <span>{t('dashboard.lengthFt')}</span>
              <input
                type="number"
                min={0}
                value={lengthFt}
                onChange={(e) => setLengthFt(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
              />
            </label>
            <label className="text-xs font-bold text-slate-700 space-y-1">
              <span>{t('dashboard.widthFt')}</span>
              <input
                type="number"
                min={0}
                value={widthFt}
                onChange={(e) => setWidthFt(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
              />
            </label>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <label className="text-xs font-bold text-slate-700 space-y-1">
              <span>{t('dashboard.buildingAge')}</span>
              <input
                type="number"
                min={0}
                max={120}
                value={ageYears}
                onChange={(e) => setAgeYears(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
              />
            </label>
            <label className="text-xs font-bold text-slate-700 space-y-1">
              <span>{t('dashboard.waterTax')}</span>
              <input
                type="number"
                min={0}
                value={waterTax}
                onChange={(e) => setWaterTax(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
              />
            </label>
          </div>

          <label className="text-xs font-bold text-slate-700 space-y-1">
            <span>{t('dashboard.landRate')}</span>
            <input
              type="number"
              min={0}
              value={landRate}
              onChange={(e) => setLandRate(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
            />
          </label>
          <label className="text-xs font-bold text-slate-700 space-y-1">
            <span>{t('dashboard.rrRate')}</span>
            <input
              type="number"
              min={0}
              value={rrRate}
              onChange={(e) => setRrRate(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
            />
          </label>
          <label className="text-xs font-bold text-slate-700 space-y-1">
            <span>{t('dashboard.taxRate')}</span>
            <input
              type="number"
              step="0.1"
              min={0}
              value={taxRate}
              onChange={(e) => setTaxRate(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 mono text-xs"
            />
          </label>
        </div>

        {taxPreviewQuery.isError && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-2.5 text-xs text-rose-800">
            {t('dashboard.taxCalcError')}
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 rounded-xl border border-brand-200 bg-brand-50/70 p-4">
          <div>
            <p className="text-[11px] font-semibold text-muted">
              {t('dashboard.buildingTax')}
              {firstFloor
                ? ` (${firstFloor.areaSqm} ${t('dashboard.sqMUnit')} · ${firstFloor.depreciationPercentage}%)`
                : ` (${areaSqFtPreview} ${t('dashboard.sqFtUnit')})`}
            </p>
            <p className="mt-1 mono text-lg font-extrabold text-ink">
              {taxPreviewQuery.isFetching ? '...' : formatInr((taxResult?.buildingTax ?? 0) + (taxResult?.openLandTax ?? 0))}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-semibold text-muted">
              {t('dashboard.slabTax')} ({areaSqFtPreview} {t('dashboard.sqFtUnit')})
            </p>
            <p className="mt-1 mono text-lg font-extrabold text-ink">
              {taxPreviewQuery.isFetching ? '...' : formatInr(slabTotal)}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-semibold text-muted">
              {t('dashboard.waterAndOtherTax')}
            </p>
            <p className="mt-1 mono text-lg font-extrabold text-ink">
              {taxPreviewQuery.isFetching ? '...' : formatInr((taxResult?.waterTax ?? 0) + (taxResult?.otherTax ?? 0))}
            </p>
          </div>
          <div className="rounded-lg bg-white px-3 py-2 border border-brand-200">
            <p className="text-[11px] font-bold text-brand-800">{t('dashboard.totalTax')}</p>
            <p className="mono text-2xl font-extrabold text-brand-700">
              {taxPreviewQuery.isFetching ? '...' : formatInr(taxResult?.totalTax ?? 0)}
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}
