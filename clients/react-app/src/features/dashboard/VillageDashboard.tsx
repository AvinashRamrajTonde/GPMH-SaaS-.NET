import { useQuery } from '@tanstack/react-query';
import {
  Activity,
  AlertCircle,
  Building2,
  Calculator,
  CheckCircle2,
  Droplets,
  FilePlus2,
  FileSpreadsheet,
  GitBranch,
  IndianRupee,
  Landmark,
  ReceiptText,
  Search,
} from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { sprint2Api, type DashboardSearchResult } from '../../services/sprint2Api';

const SQFT_TO_SQM = 0.09290304;
const SLAB_RATES = [
  { maxSqFt: 300, total: 180 },
  { maxSqFt: 700, total: 220 },
  { maxSqFt: Number.POSITIVE_INFINITY, total: 300 },
];

function resolveDepreciationFactor(age: number): number {
  if (age <= 2) return 1.0;
  if (age <= 5) return 0.95;
  if (age <= 10) return 0.85;
  if (age <= 20) return 0.75;
  if (age <= 30) return 0.6;
  if (age <= 40) return 0.45;
  if (age <= 50) return 0.35;
  if (age <= 60) return 0.25;
  return 0.15;
}

function formatInr(amount: number): string {
  return `₹ ${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function VillageDashboard() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();
  const searchInputRef = useRef<HTMLInputElement>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [searchResults, setSearchResults] = useState<DashboardSearchResult | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);

  // Golden vector preview state
  const [lengthFt, setLengthFt] = useState(15);
  const [widthFt, setWidthFt] = useState(15);
  const [landRate, setLandRate] = useState(760);
  const [rrRate, setRrRate] = useState(5852);
  const [ageYears, setAgeYears] = useState(5);
  const [taxRate, setTaxRate] = useState(0.5);

  const canQueryTenantDashboard = Boolean(accessToken && user?.activeTenantId);

  const summaryQuery = useQuery({
    queryKey: ['dashboard', 'summary', user?.activeTenantId],
    queryFn: sprint2Api.getDashboardSummary,
    enabled: canQueryTenantDashboard,
  });

  // Global '/' keyboard shortcut to jump directly to Property/Citizen search
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

  async function runGlobalSearch(q: string) {
    setSearchTerm(q);
    if (!canQueryTenantDashboard || q.trim().length < 2) {
      setSearchResults(null);
      return;
    }
    setSearchLoading(true);
    try {
      const hits = await sprint2Api.searchDashboard(q);
      setSearchResults(hits);
    } finally {
      setSearchLoading(false);
    }
  }

  const preview = useMemo(() => {
    const areaSqFt = Math.max(0, lengthFt) * Math.max(0, widthFt);
    const areaSqm = Math.round(areaSqFt * SQFT_TO_SQM * 100) / 100;
    const factor = resolveDepreciationFactor(Math.max(0, ageYears));
    const capitalValue = areaSqm * landRate + areaSqm * rrRate * factor;
    const buildingTax = Math.round((capitalValue * 1.0 * taxRate) / 1000);
    const slabTax =
      areaSqFt === 0 ? 0 : (SLAB_RATES.find((s) => areaSqFt <= s.maxSqFt)?.total ?? 300);
    return {
      areaSqFt,
      areaSqm,
      factorPct: Math.round(factor * 100),
      buildingTax,
      slabTax,
      totalTax: buildingTax + slabTax,
    };
  }, [lengthFt, widthFt, landRate, rrRate, ageYears, taxRate]);

  const summary = summaryQuery.data;

  const kpiCards = [
    {
      label: t('dashboard.kpiProperties'),
      value: summary ? String(summary.totalProperties) : '—',
      icon: Building2,
      accent: 'text-emerald-700 bg-emerald-50',
    },
    {
      label: t('dashboard.kpiDemand'),
      value: summary ? formatInr(summary.currentFyDemand) : '₹ 0.00',
      icon: Landmark,
      accent: 'text-indigo-700 bg-indigo-50',
    },
    {
      label: t('dashboard.kpiCollection'),
      value: summary ? formatInr(summary.todayCollection) : '₹ 0.00',
      sub: summary ? `MTD: ${formatInr(summary.monthCollection)}` : undefined,
      icon: IndianRupee,
      accent: 'text-teal-700 bg-teal-50',
    },
    {
      label: t('dashboard.kpiArrears'),
      value: summary ? formatInr(summary.outstandingArrears) : '₹ 0.00',
      icon: AlertCircle,
      accent: 'text-amber-700 bg-amber-50',
    },
    {
      label: t('dashboard.kpiMutations'),
      value: summary ? String(summary.pendingMutations) : '0',
      icon: GitBranch,
      accent: 'text-purple-700 bg-purple-50',
    },
    {
      label: t('dashboard.kpiWater'),
      value: summary ? formatInr(summary.waterAndSpecialCollection) : '₹ 0.00',
      icon: Droplets,
      accent: 'text-sky-700 bg-sky-50',
    },
  ];

  const quickActions = [
    { label: t('dashboard.actionNamuna8'), to: '/namuna-8/add', icon: FilePlus2 },
    { label: t('dashboard.actionCollect'), to: '/namuna-10/collect', icon: ReceiptText },
    { label: t('dashboard.actionBills'), to: '/namuna-9ka/generate', icon: FileSpreadsheet },
    { label: t('dashboard.actionMutation'), to: '/namuna-8/mutation/new', icon: GitBranch },
  ];

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">
              {summary ? `${summary.panchayatNameMr} (${summary.panchayatNameEn})` : t('dashboard.title')}
            </h1>
            <p className="mt-1 text-sm text-slate-600">
              {summary
                ? `LGD ${summary.lgdCode} · ${t('dashboard.ayBadge')}: ${summary.activeAssessmentYear}`
                : t('dashboard.lead')}
            </p>
          </div>

          <div className="relative w-full sm:w-80">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              ref={searchInputRef}
              type="text"
              value={searchTerm}
              onChange={(e) => void runGlobalSearch(e.target.value)}
              placeholder={t('dashboard.searchPlaceholder')}
              className="w-full rounded-xl border border-slate-300 bg-slate-50 pl-9 pr-10 py-2 text-xs focus:bg-white focus:border-emerald-600 focus:outline-none"
            />
            <kbd className="pointer-events-none absolute right-2.5 top-2 rounded border border-slate-300 bg-white px-1.5 py-0.5 font-mono text-[10px] text-slate-500">
              /
            </kbd>
          </div>
        </div>

        {!canQueryTenantDashboard && (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-xs text-amber-900">
            <span>{t('dashboard.signInPrompt')}</span>
            <div className="flex gap-2">
              <Link
                to="/login"
                className="rounded-lg bg-emerald-700 px-3 py-1.5 font-bold text-white hover:bg-emerald-800"
              >
                {t('nav.login')}
              </Link>
              <Link
                to="/onboarding"
                className="rounded-lg border border-emerald-700 px-3 py-1.5 font-bold text-emerald-800 hover:bg-emerald-50"
              >
                {t('nav.onboarding')}
              </Link>
            </div>
          </div>
        )}

        {summaryQuery.isLoading && (
          <div className="animate-pulse grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {[1, 2, 3, 4, 5, 6].map((n) => (
              <div key={n} className="h-24 rounded-xl bg-slate-200" />
            ))}
          </div>
        )}

        {summaryQuery.error instanceof ApiProblemError && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs text-rose-800">
            {summaryQuery.error.message}
          </div>
        )}

        {searchLoading && (
          <p className="text-xs text-slate-500">{t('onboarding.loading')}</p>
        )}

        {searchResults && (
          <div className="rounded-xl border border-emerald-200 bg-emerald-50/40 p-4 text-xs space-y-2">
            <p className="font-bold text-emerald-900">
              {t('dashboard.searchHits', {
                props: searchResults.properties.length,
                citizens: searchResults.citizens.length,
              })}
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              {searchResults.properties.map((p) => (
                <div key={p.id} className="rounded-lg border border-emerald-200 bg-white p-2.5">
                  <span className="font-mono font-bold text-emerald-800">
                    {p.wardNo} / {p.propertyNo}
                  </span>{' '}
                  · {p.totalAreaSqFt} sq.ft ({p.waterConnectionCode})
                </div>
              ))}
              {searchResults.citizens.map((c) => (
                <div key={c.id} className="rounded-lg border border-emerald-200 bg-white p-2.5">
                  <span className="font-bold text-slate-900">
                    {c.fullNameMr} ({c.fullNameEn})
                  </span>{' '}
                  · {c.citizenCode} {c.aadhaarMasked ? `· ${c.aadhaarMasked}` : ''}
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {kpiCards.map((kpi) => {
            const Icon = kpi.icon;
            return (
              <div
                key={kpi.label}
                className="rounded-xl border border-slate-200 bg-white p-4 flex items-center justify-between gap-3 shadow-xs"
              >
                <div>
                  <p className="text-xs font-semibold text-slate-500">{kpi.label}</p>
                  <p className="mt-1 font-mono text-xl font-extrabold text-slate-900 tabular-nums">
                    {kpi.value}
                  </p>
                  {kpi.sub && <p className="text-[11px] font-mono text-slate-500 tabular-nums">{kpi.sub}</p>}
                </div>
                <div className={`flex h-11 w-11 items-center justify-center rounded-xl ${kpi.accent}`}>
                  <Icon className="h-5 w-5" />
                </div>
              </div>
            );
          })}
        </div>

        <div>
          <p className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-500">
            {t('dashboard.quickActionsTitle')}
          </p>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {quickActions.map((act) => {
              const Icon = act.icon;
              return (
                <Link
                  key={act.to}
                  to={act.to}
                  className="flex items-center gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm font-bold text-slate-800 transition hover:border-emerald-400 hover:bg-emerald-50 hover:text-emerald-900"
                >
                  <Icon className="h-5 w-5 text-emerald-700 shrink-0" />
                  <span>{act.label}</span>
                </Link>
              );
            })}
          </div>
        </div>
      </section>

      {summary && (
        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-bold text-slate-900">{t('dashboard.checklistTitle')}</h2>
              <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 font-mono text-xs font-bold text-emerald-800">
                {summary.setupProgressPct}%
              </span>
            </div>
            <div className="space-y-2">
              {summary.checklistSteps.map((step) => (
                <div
                  key={step.stepNumber}
                  className="flex items-center justify-between rounded-lg border border-slate-100 bg-slate-50 px-3 py-2 text-xs"
                >
                  <span className="font-semibold text-slate-800">
                    {step.stepNumber}. {t(`onboarding.step${step.stepNumber}Nav`)}
                    {step.detail ? ` — ${step.detail}` : ''}
                  </span>
                  <span
                    className={`inline-flex items-center gap-1 font-bold ${
                      step.isCompleted ? 'text-emerald-700' : 'text-amber-700'
                    }`}
                  >
                    {step.isCompleted && <CheckCircle2 className="h-3.5 w-3.5" />}
                    {step.isCompleted ? t('dashboard.done') : t('dashboard.pending')}
                  </span>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm space-y-3">
            <div className="flex items-center gap-2">
              <Activity className="h-4 w-4 text-emerald-700" />
              <h2 className="text-sm font-bold text-slate-900">{t('dashboard.recentActivityTitle')}</h2>
            </div>
            {summary.recentActivity.length === 0 ? (
              <p className="text-xs text-slate-500">{t('dashboard.noActivity')}</p>
            ) : (
              <div className="space-y-2 max-h-64 overflow-y-auto">
                {summary.recentActivity.map((ev) => (
                  <div
                    key={ev.id}
                    className="flex items-center justify-between rounded-lg border border-slate-100 bg-slate-50 px-3 py-2 text-xs"
                  >
                    <div>
                      <span className="font-bold text-slate-800">{ev.entityName}</span>{' '}
                      <span className="rounded bg-slate-200 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-slate-700">
                        {ev.action}
                      </span>
                      {ev.actorRole && (
                        <span className="ml-2 text-[11px] text-slate-500">{ev.actorRole}</span>
                      )}
                    </div>
                    <span className="font-mono text-[10.5px] text-slate-500">
                      {new Date(ev.timestampUtc).toLocaleString()}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </section>
      )}

      <section className="rounded-2xl border border-emerald-200 bg-white p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Calculator className="h-5 w-5 text-emerald-700" />
            <h2 className="text-lg font-bold text-slate-900">{t('dashboard.taxPreviewTitle')}</h2>
          </div>
          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800">
            ADR-016 Locked
          </span>
        </div>
        <p className="mt-1 text-xs text-emerald-800">{t('dashboard.goldenHint')}</p>

        <div className="mt-4 grid gap-4 sm:grid-cols-3 lg:grid-cols-6">
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.lengthFt')}
            <input
              type="number"
              min={0}
              value={lengthFt}
              onChange={(e) => setLengthFt(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.widthFt')}
            <input
              type="number"
              min={0}
              value={widthFt}
              onChange={(e) => setWidthFt(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.landRate')}
            <input
              type="number"
              min={0}
              value={landRate}
              onChange={(e) => setLandRate(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.rrRate')}
            <input
              type="number"
              min={0}
              value={rrRate}
              onChange={(e) => setRrRate(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.buildingAge')}
            <input
              type="number"
              min={0}
              value={ageYears}
              onChange={(e) => setAgeYears(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.taxRate')}
            <input
              type="number"
              step="0.1"
              min={0}
              value={taxRate}
              onChange={(e) => setTaxRate(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
        </div>

        <div className="mt-4 grid gap-4 sm:grid-cols-3 rounded-xl bg-emerald-50/70 p-4 border border-emerald-100">
          <div>
            <p className="text-xs text-slate-600">
              {t('dashboard.buildingTax')} ({preview.areaSqm} sq.m · {preview.factorPct}%)
            </p>
            <p className="text-xl font-bold text-slate-900 tabular-nums">₹ {preview.buildingTax}</p>
          </div>
          <div>
            <p className="text-xs text-slate-600">
              {t('dashboard.slabTax')} ({preview.areaSqFt} sq.ft)
            </p>
            <p className="text-xl font-bold text-slate-900 tabular-nums">₹ {preview.slabTax}</p>
          </div>
          <div>
            <p className="text-xs font-semibold text-emerald-800">{t('dashboard.totalTax')}</p>
            <p className="text-2xl font-extrabold text-emerald-700 tabular-nums">₹ {preview.totalTax}</p>
          </div>
        </div>
      </section>
    </div>
  );
}
