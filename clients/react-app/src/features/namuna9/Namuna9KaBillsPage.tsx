import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  ExternalLink,
  Eye,
  FileSpreadsheet,
  Filter,
  Lock,
  Printer,
  QrCode,
  RefreshCw,
  Search,
  ShieldAlert,
  Sparkles,
  X,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import {
  namuna9KaApi,
  type Namuna9KaBillRowDto,
  type Namuna9KaLayoutMode,
  type Namuna9KaSplitMode,
  type Namuna9KaWorkspaceDto,
} from '../../services/namuna9KaApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { Namuna9KaBillPreviewModal } from './Namuna9KaBillPreviewModal';

type LayoutChoice = 'counterfoil_2in1' | 'batch_2in1' | 'a5_portrait';

function resolvePdfLayoutParams(choice: LayoutChoice): {
  layout: Namuna9KaLayoutMode;
  splitMode: Namuna9KaSplitMode;
} {
  if (choice === 'a5_portrait') {
    return { layout: 'a5', splitMode: 'counterfoil' };
  }
  if (choice === 'batch_2in1') {
    return { layout: '2in1_landscape', splitMode: 'batch2in1' };
  }
  return { layout: '2in1_landscape', splitMode: 'counterfoil' };
}

function billStatusBadgeClass(status: string): string {
  switch (status) {
    case 'Paid':
      return 'border-emerald-200 bg-emerald-50 text-emerald-800';
    case 'PartiallyPaid':
      return 'border-amber-200 bg-amber-50 text-amber-800';
    case 'Issued':
      return 'border-brand-200 bg-brand-50 text-brand-800';
    default:
      return 'border-slate-200 bg-slate-100 text-slate-700';
  }
}

export function Namuna9KaBillsPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const initialYearParam = searchParams.get('yearId') ?? undefined;
  const propertyIdParam = searchParams.get('propertyId') ?? '';

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      user &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Auditor', 'Surveyor'].includes(
        user.role,
      ),
  );
  const canAdminWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin'].includes(user.role),
  );

  const [selectedYearId, setSelectedYearId] = useState<string | undefined>(initialYearParam);
  const [wardFilter, setWardFilter] = useState<string>('');
  const [searchInput, setSearchInput] = useState<string>('');
  const [appliedSearch, setAppliedSearch] = useState<string>('');
  const [pendingOnly, setPendingOnly] = useState<boolean>(false);

  const [layoutChoice, setLayoutChoice] = useState<LayoutChoice>('counterfoil_2in1');
  const [issueDate, setIssueDate] = useState<string>('');
  const [regenerateIfChanged, setRegenerateIfChanged] = useState<boolean>(false);

  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [previewBill, setPreviewBill] = useState<Namuna9KaBillRowDto | null>(null);

  const [generating, setGenerating] = useState<boolean>(false);
  const [downloadingBatchPdf, setDownloadingBatchPdf] = useState<boolean>(false);
  const [downloadingRowId, setDownloadingRowId] = useState<string | null>(null);

  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [actionWarning, setActionWarning] = useState<string | null>(null);

  const propertyFilterIds = useMemo(
    () =>
      propertyIdParam
        .split(',')
        .map((id) => id.trim())
        .filter(Boolean),
    [propertyIdParam],
  );

  // Cached unfiltered index so Ward filter chips remain complete even while filtering
  const wardsIndexQuery = useQuery<Namuna9KaWorkspaceDto>({
    queryKey: ['namuna9ka-wards-index', user?.activeTenantId, selectedYearId],
    queryFn: () => namuna9KaApi.getBills({ yearId: selectedYearId }),
    enabled: canRead,
    staleTime: 60_000,
  });

  const billsQuery = useQuery<Namuna9KaWorkspaceDto>({
    queryKey: [
      'namuna9ka-bills',
      user?.activeTenantId,
      selectedYearId,
      wardFilter,
      appliedSearch,
      pendingOnly,
      propertyIdParam,
    ],
    queryFn: () =>
      namuna9KaApi.getBills({
        yearId: selectedYearId,
        wardNo: wardFilter || undefined,
        search: appliedSearch || undefined,
        pendingOnly,
        propertyIds: propertyFilterIds.length > 0 ? propertyFilterIds : undefined,
      }),
    enabled: canRead,
  });

  const workspace = billsQuery.data;
  const rows = useMemo(() => workspace?.items ?? [], [workspace?.items]);
  const isLocked = Boolean(workspace?.selectedYear.isLocked);

  const wardOptions = useMemo(() => {
    const sourceItems = wardsIndexQuery.data?.items ?? rows;
    const set = new Set<string>();
    for (const r of sourceItems) {
      if (r.wardNo) set.add(r.wardNo);
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  }, [wardsIndexQuery.data?.items, rows]);

  // Prune stale selections synchronously from rendered rows
  const visibleRowIdSet = useMemo(() => new Set(rows.map((r) => r.propertyId)), [rows]);
  const effectiveSelectedIds = useMemo(
    () => selectedIds.filter((id) => visibleRowIdSet.has(id)),
    [selectedIds, visibleRowIdSet],
  );
  const allVisibleSelected = rows.length > 0 && effectiveSelectedIds.length === rows.length;

  const visibleTotals = useMemo(() => {
    let issuedCount = 0;
    let pendingGenCount = 0;
    let staleCount = 0;
    let generalTotal = 0;
    let waterTotal = 0;
    let rebateTotal = 0;
    let netPayable = 0;

    for (const r of rows) {
      if (r.billId) issuedCount += 1;
      else pendingGenCount += 1;
      if (r.isStaleBill) staleCount += 1;
      generalTotal += r.generalDemandTotal;
      waterTotal += r.waterDemandTotal;
      rebateTotal += r.rebateAmount;
      netPayable += r.netPayableAmount;
    }
    return {
      issuedCount,
      pendingGenCount,
      staleCount,
      generalTotal,
      waterTotal,
      rebateTotal,
      netPayable,
    };
  }, [rows]);

  const invalidateCaches = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['namuna9ka-bills'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna9ka-wards-index'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna9-register'] }),
      queryClient.invalidateQueries({ queryKey: ['property-detail'] }),
    ]);
  };

  const clearFeedback = () => {
    setErrSummary(null);
    setErrItems([]);
    setActionSuccess(null);
    setActionWarning(null);
  };

  const switchYear = (yearId: string) => {
    setSelectedYearId(yearId);
    setSelectedIds([]);
    clearFeedback();
    const next = new URLSearchParams(searchParams);
    next.set('yearId', yearId);
    setSearchParams(next, { replace: true });
  };

  const clearPropertyDeepLink = () => {
    const next = new URLSearchParams(searchParams);
    next.delete('propertyId');
    setSearchParams(next, { replace: true });
    setSelectedIds([]);
  };

  const handleSelectAll = (checked: boolean) => {
    if (checked) {
      setSelectedIds(rows.map((r) => r.propertyId));
    } else {
      setSelectedIds([]);
    }
  };

  const toggleRowSelection = (propertyId: string) => {
    setSelectedIds((prev) =>
      prev.includes(propertyId) ? prev.filter((id) => id !== propertyId) : [...prev, propertyId],
    );
  };

  const handleGenerateBills = async () => {
    if (!workspace) return;
    clearFeedback();
    setGenerating(true);

    const targetPropertyIds =
      effectiveSelectedIds.length > 0
        ? effectiveSelectedIds
        : propertyFilterIds.length > 0
          ? propertyFilterIds
          : undefined;

    try {
      const res = await namuna9KaApi.generateBills({
        assessmentYearId: workspace.selectedYear.id,
        wardNo: !targetPropertyIds && wardFilter ? wardFilter : undefined,
        propertyIds: targetPropertyIds,
        issueDate: issueDate || undefined,
        regenerateIfChanged,
      });

      setActionSuccess(
        t('namuna9Ka.generateSuccessMsg', {
          year: res.yearLabel,
          generated: res.generatedCount,
          reused: res.reusedCount,
          superseded: res.supersededCount,
        }),
      );
      if (res.warningMessage) {
        setActionWarning(res.warningMessage);
      }
      setSelectedIds([]);
      await invalidateCaches();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9Ka.errGenerateFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setGenerating(false);
    }
  };

  const handleDownloadBatchPdf = async () => {
    if (!workspace) return;
    clearFeedback();
    setDownloadingBatchPdf(true);

    const { layout, splitMode } = resolvePdfLayoutParams(layoutChoice);
    const targetPropertyIds =
      effectiveSelectedIds.length > 0
        ? effectiveSelectedIds
        : propertyFilterIds.length > 0
          ? propertyFilterIds
          : undefined;

    try {
      await namuna9KaApi.downloadBillsPdf({
        yearId: workspace.selectedYear.id,
        wardNo: !targetPropertyIds && wardFilter ? wardFilter : undefined,
        search: !targetPropertyIds && appliedSearch ? appliedSearch : undefined,
        propertyIds: targetPropertyIds,
        layout,
        splitMode,
      });
      setActionSuccess(t('namuna9Ka.pdfDownloadedMsg'));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9Ka.errPdfFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setDownloadingBatchPdf(false);
    }
  };

  const handleDownloadSinglePdf = async (propertyId: string) => {
    if (!workspace) return;
    clearFeedback();
    setDownloadingRowId(propertyId);
    const { layout, splitMode } = resolvePdfLayoutParams(layoutChoice);

    try {
      await namuna9KaApi.downloadBillsPdf({
        yearId: workspace.selectedYear.id,
        propertyIds: [propertyId],
        layout,
        splitMode,
      });
      setActionSuccess(t('namuna9Ka.pdfDownloadedMsg'));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9Ka.errPdfFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setDownloadingRowId(null);
    }
  };

  const billStatusLabel = (status: string): string => {
    switch (status) {
      case 'Paid':
        return t('namuna9Ka.statusPaid');
      case 'PartiallyPaid':
        return t('namuna9Ka.statusPartiallyPaid');
      case 'Issued':
        return t('namuna9Ka.statusIssued');
      default:
        return t('namuna9Ka.statusPendingGen');
    }
  };

  if (!canRead) {
    return (
      <div className="rounded-2xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-900">
        <div className="flex items-start gap-3">
          <ShieldAlert className="mt-0.5 h-5 w-5 shrink-0 text-amber-700" />
          <div>
            <h1 className="font-bold">{t('namuna9.accessRestrictedTitle')}</h1>
            <p className="mt-1 text-xs">{t('namuna9.accessRestrictedDesc')}</p>
            <Link
              to="/login"
              className="mt-3 inline-flex items-center rounded-xl bg-brand-700 px-4 py-2 text-xs font-bold text-white"
            >
              {t('nav.login')}
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header & Action Controls */}
      <section className="rounded-2xl border border-line bg-card p-4 shadow-xs sm:p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1.5">
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted">
              <Link
                to="/namuna-9"
                className="inline-flex items-center gap-1 font-semibold text-brand-700 hover:underline"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                {t('nav.namuna9')}
              </Link>
              <span>/</span>
              <Link to="/properties" className="font-semibold text-slate-600 hover:underline">
                {t('nav.properties')}
              </Link>
              <span>/</span>
              <span className="font-semibold text-ink">{t('nav.demandBills')}</span>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-xl font-extrabold tracking-tight text-ink sm:text-2xl">
                {t('namuna9Ka.pageTitle')}
              </h1>
              {workspace && (
                <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-[11px] font-bold text-emerald-800">
                  {t('namuna9Ka.policyBadge', {
                    days: workspace.noticeGraceDays,
                    pct: workspace.earlyRebatePct,
                  })}
                </span>
              )}
              {workspace?.hasUpiConfigured && workspace.configuredUpiId && (
                <span className="inline-flex items-center gap-1 rounded-full border border-brand-200 bg-brand-50 px-2.5 py-0.5 font-mono text-[11px] font-bold text-brand-800">
                  <QrCode className="h-3 w-3" />
                  {workspace.configuredUpiId}
                </span>
              )}
            </div>

            <p className="text-xs text-muted sm:text-sm">{t('namuna9Ka.pageSubtitle')}</p>
          </div>

          {/* Primary Toolbar Controls */}
          <div className="flex flex-wrap items-center gap-2">
            {workspace && workspace.assessmentYears.length > 0 && (
              <div className="flex items-center gap-1.5 rounded-xl border border-line bg-slate-50 px-2.5 py-1.5">
                <span className="text-[11px] font-bold text-muted">
                  {t('namuna9.assessmentYearLabel')}:
                </span>
                <select
                  aria-label={t('namuna9.assessmentYearLabel')}
                  value={workspace.selectedYear.id}
                  onChange={(e) => switchYear(e.target.value)}
                  className="bg-transparent text-xs font-extrabold text-ink focus:outline-none"
                >
                  {workspace.assessmentYears.map((yr) => (
                    <option key={yr.id} value={yr.id}>
                      {yr.yearLabel}
                      {yr.isLocked ? ` (${t('namuna9Ka.lockedTag')})` : ''}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {canAdminWrite && (
              <>
                <div className="flex items-center gap-1.5 rounded-xl border border-line bg-white px-2.5 py-1.5">
                  <label htmlFor="bill-issue-date" className="text-[11px] font-bold text-muted">
                    {t('namuna9Ka.issueDateLabel')}:
                  </label>
                  <input
                    id="bill-issue-date"
                    type="date"
                    value={issueDate}
                    disabled={isLocked}
                    onChange={(e) => setIssueDate(e.target.value)}
                    className="text-xs font-semibold text-ink focus:outline-none disabled:opacity-50"
                  />
                </div>

                <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-2 text-xs font-semibold text-slate-700">
                  <input
                    type="checkbox"
                    checked={regenerateIfChanged}
                    disabled={isLocked}
                    onChange={(e) => setRegenerateIfChanged(e.target.checked)}
                    className="h-3.5 w-3.5 rounded accent-brand-700"
                  />
                  {t('namuna9Ka.regenerateChangedLabel')}
                </label>

                <button
                  type="button"
                  disabled={isLocked || generating || !workspace || rows.length === 0}
                  onClick={handleGenerateBills}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-emerald-300 bg-emerald-600 px-3.5 py-2 text-xs font-bold text-white shadow-xs hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Sparkles className="h-3.5 w-3.5" />
                  {generating
                    ? t('namuna9.processingLabel')
                    : effectiveSelectedIds.length > 0
                      ? t('namuna9Ka.generateSelectedBtn', { count: effectiveSelectedIds.length })
                      : t('namuna9Ka.generateBillsBtn')}
                </button>
              </>
            )}

            <button
              type="button"
              disabled={!workspace || downloadingBatchPdf || rows.length === 0}
              onClick={handleDownloadBatchPdf}
              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-xs font-bold text-white shadow-xs hover:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <Printer className="h-3.5 w-3.5" />
              {downloadingBatchPdf
                ? t('namuna9.processingLabel')
                : effectiveSelectedIds.length > 0
                  ? t('namuna9Ka.printSelectedPdfBtn', { count: effectiveSelectedIds.length })
                  : t('namuna9Ka.printVisiblePdfBtn', { count: rows.length })}
            </button>
          </div>
        </div>

        {/* Active Property Deep-Link Pill */}
        {propertyFilterIds.length > 0 && (
          <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-line pt-3">
            <span className="inline-flex items-center gap-2 rounded-full border border-brand-200 bg-brand-50 px-3 py-1 text-xs font-bold text-brand-800">
              {t('namuna9Ka.filteredPropertyPill', {
                count: propertyFilterIds.length,
              })}
              <button
                type="button"
                onClick={clearPropertyDeepLink}
                className="inline-flex items-center gap-1 rounded-full bg-white px-2 py-0.5 text-[11px] font-extrabold text-brand-700 hover:bg-brand-100"
              >
                <X className="h-3 w-3" />
                {t('namuna9Ka.showAllBillsBtn')}
              </button>
            </span>
          </div>
        )}
      </section>

      {/* Locked Financial Year Notice */}
      {isLocked && workspace && (
        <div className="flex items-start gap-3 rounded-2xl border border-slate-300 bg-slate-100 px-4 py-3 text-xs text-slate-800">
          <Lock className="mt-0.5 h-4 w-4 shrink-0 text-slate-700" />
          <div>
            <p className="font-bold">
              {t('namuna9Ka.lockedBannerTitle', { year: workspace.selectedYear.yearLabel })}
            </p>
            <p className="mt-0.5 text-slate-600">{t('namuna9Ka.lockedBannerDesc')}</p>
          </div>
        </div>
      )}

      {/* GP Bank Account / UPI Missing Warning Banner */}
      {workspace && !workspace.hasUpiConfigured && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-xs text-amber-950">
          <div className="flex items-start gap-2.5">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-700" />
            <div>
              <p className="font-extrabold">{t('namuna9Ka.upiMissingTitle')}</p>
              <p className="mt-0.5 text-amber-900">{t('namuna9Ka.upiMissingDesc')}</p>
            </div>
          </div>
          <Link
            to="/onboarding"
            className="inline-flex items-center gap-1.5 rounded-xl bg-amber-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-amber-800"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            {t('namuna9Ka.configureUpiLink')}
          </Link>
        </div>
      )}

      {/* Error & Success Feedback */}
      <ValidationSummary summary={errSummary} items={errItems} />

      {actionWarning && (
        <div className="flex items-start justify-between gap-2 rounded-xl border border-amber-300 bg-amber-50 px-4 py-2.5 text-xs font-semibold text-amber-900">
          <div className="flex items-center gap-2">
            <AlertTriangle className="h-4 w-4 shrink-0 text-amber-700" />
            <span>{actionWarning}</span>
          </div>
          <button
            type="button"
            onClick={() => setActionWarning(null)}
            className="font-bold text-amber-800 hover:underline"
          >
            {t('namuna9Ka.dismissBtn')}
          </button>
        </div>
      )}

      {actionSuccess && (
        <div className="flex items-center justify-between gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-xs font-bold text-emerald-800">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 shrink-0" />
            <span>{actionSuccess}</span>
          </div>
          <button
            type="button"
            onClick={() => setActionSuccess(null)}
            className="text-emerald-700 hover:underline"
          >
            {t('namuna9Ka.dismissBtn')}
          </button>
        </div>
      )}

      {/* 5 Statutory KPI Cards (matching template 12) */}
      {workspace && (
        <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <div className="rounded-2xl border border-line border-l-4 border-l-slate-700 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9Ka.kpiTotalBills')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-ink">
              {visibleTotals.issuedCount}
              <span className="ml-1 text-xs font-semibold text-muted">/ {rows.length}</span>
            </p>
            <p className="mt-1 text-[11px] text-muted">
              {t('namuna9Ka.kpiTotalBillsSub', {
                pending: visibleTotals.pendingGenCount,
                stale: visibleTotals.staleCount,
              })}
            </p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-brand-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9Ka.kpiGeneralDemand')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-ink">
              {formatInr(visibleTotals.generalTotal)}
            </p>
            <p className="mt-1 text-[11px] text-muted">{t('namuna9Ka.kpiGeneralDemandSub')}</p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-cyan-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9Ka.kpiWaterDemand')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-ink">
              {formatInr(visibleTotals.waterTotal)}
            </p>
            <p className="mt-1 text-[11px] text-muted">{t('namuna9Ka.kpiWaterDemandSub')}</p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-emerald-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9Ka.kpiTotalRebate')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-emerald-700">
              -{formatInr(visibleTotals.rebateTotal)}
            </p>
            <p className="mt-1 text-[11px] text-muted">
              {t('namuna9Ka.kpiTotalRebateSub', { pct: workspace.earlyRebatePct })}
            </p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-rose-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9Ka.kpiNetPayable')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-rose-700">
              {formatInr(visibleTotals.netPayable)}
            </p>
            <p className="mt-1 text-[11px] text-muted">
              {t('namuna9Ka.kpiNetPayableSub', {
                totalYear: formatInr(workspace.totalNetPayable),
              })}
            </p>
          </div>
        </section>
      )}

      {/* Filter Bar & Ward Chips */}
      <section className="space-y-3 rounded-2xl border border-line bg-card p-4 shadow-2xs">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-2.5 h-3.5 w-3.5 text-muted" />
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') setAppliedSearch(searchInput.trim());
                }}
                placeholder={t('namuna9Ka.searchPlaceholder')}
                className="w-60 rounded-xl border border-line bg-white py-1.5 pl-9 pr-3 text-xs text-ink focus:border-brand-600 focus:outline-none sm:w-72"
              />
            </div>

            <button
              type="button"
              onClick={() => setAppliedSearch(searchInput.trim())}
              className="inline-flex items-center gap-1 rounded-xl border border-line bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100"
            >
              <Filter className="h-3.5 w-3.5" />
              {t('namuna9.filterBtn')}
            </button>

            <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-1.5 text-xs font-semibold text-slate-700">
              <input
                type="checkbox"
                checked={pendingOnly}
                onChange={(e) => setPendingOnly(e.target.checked)}
                className="h-3.5 w-3.5 rounded accent-brand-700"
              />
              {t('namuna9.pendingOnlyLabel')}
            </label>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <label htmlFor="bill-pdf-layout" className="text-[11px] font-bold text-muted">
              {t('namuna9Ka.layoutLabel')}:
            </label>
            <select
              id="bill-pdf-layout"
              value={layoutChoice}
              onChange={(e) => setLayoutChoice(e.target.value as LayoutChoice)}
              className="rounded-xl border border-line bg-white px-3 py-1.5 text-xs font-bold text-ink focus:border-brand-600 focus:outline-none"
            >
              <option value="counterfoil_2in1">{t('namuna9Ka.layoutCounterfoil')}</option>
              <option value="batch_2in1">{t('namuna9Ka.layoutBatch2in1')}</option>
              <option value="a5_portrait">{t('namuna9Ka.layoutA5')}</option>
            </select>

            <button
              type="button"
              onClick={() => billsQuery.refetch()}
              title={t('namuna9Ka.refreshTooltip')}
              className="inline-flex items-center gap-1 rounded-xl border border-line bg-white px-2.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${billsQuery.isFetching ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {/* Ward Chips */}
        {wardOptions.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5 border-t border-line pt-2.5">
            <span className="mr-1 text-[11px] font-bold uppercase tracking-wider text-muted">
              {t('namuna9Ka.wardsLabel')}:
            </span>
            <button
              type="button"
              onClick={() => setWardFilter('')}
              className={`rounded-full border px-3 py-1 text-xs font-bold transition ${
                wardFilter === ''
                  ? 'border-brand-700 bg-brand-700 text-white'
                  : 'border-line bg-white text-slate-700 hover:bg-slate-50'
              }`}
            >
              {t('namuna9.allWards')}
            </button>
            {wardOptions.map((w) => (
              <button
                key={w}
                type="button"
                onClick={() => setWardFilter(w)}
                className={`rounded-full border px-3 py-1 text-xs font-bold transition ${
                  wardFilter === w
                    ? 'border-brand-700 bg-brand-700 text-white'
                    : 'border-line bg-white text-slate-700 hover:bg-slate-50'
                }`}
              >
                {t('namuna9.wardOption', { ward: w })}
              </button>
            ))}
          </div>
        )}
      </section>

      {/* Main Demand Bills Table */}
      <section className="overflow-hidden rounded-2xl border border-line bg-card shadow-2xs">
        {billsQuery.isLoading ? (
          <div className="flex items-center justify-center py-16 text-xs font-semibold text-muted">
            {t('namuna9Ka.loadingBills')}
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 py-16 text-center">
            <FileSpreadsheet className="mx-auto h-10 w-10 text-slate-300" />
            <p className="mt-2 text-sm font-bold text-ink">{t('namuna9Ka.emptyTitle')}</p>
            <p className="mt-1 text-xs text-muted">{t('namuna9Ka.emptyDesc')}</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1060px] border-collapse text-left text-xs">
              <thead>
                <tr className="border-b border-line bg-slate-50 text-[11px] font-bold uppercase tracking-wide text-slate-600">
                  <th className="w-10 px-3 py-3 text-center">
                    <input
                      type="checkbox"
                      aria-label={t('namuna9.selectAllLabel')}
                      checked={allVisibleSelected}
                      onChange={(e) => handleSelectAll(e.target.checked)}
                      className="h-3.5 w-3.5 rounded accent-brand-700"
                    />
                  </th>
                  <th className="px-3 py-3">{t('namuna9Ka.colBillNo')}</th>
                  <th className="px-3 py-3">{t('namuna9.colWardProp')}</th>
                  <th className="px-3 py-3">{t('namuna9Ka.colOwnerMobile')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colArrears')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colGeneral')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colWater')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colFeesPenalty')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colRebate')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colNetPayable')}</th>
                  <th className="px-3 py-3">{t('namuna9Ka.colStatus')}</th>
                  <th className="px-3 py-3 text-right">{t('namuna9Ka.colActions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {rows.map((row) => {
                  const selected = effectiveSelectedIds.includes(row.propertyId);
                  const ownerName = row.primaryOwnerNameMr || row.primaryOwnerNameEn || '—';
                  const rowBusy = downloadingRowId === row.propertyId;

                  return (
                    <tr
                      key={row.demandId}
                      className={`transition hover:bg-brand-50/30 ${
                        selected ? 'bg-brand-50/50' : 'bg-white'
                      }`}
                    >
                      <td className="px-3 py-3 text-center">
                        <input
                          type="checkbox"
                          aria-label={`${row.wardNo}/${row.propertyNo}`}
                          checked={selected}
                          onChange={() => toggleRowSelection(row.propertyId)}
                          className="h-3.5 w-3.5 rounded accent-brand-700"
                        />
                      </td>

                      {/* Bill No + dates + stale indicator */}
                      <td className="px-3 py-3 align-top">
                        <div className="flex flex-wrap items-center gap-1.5">
                          {row.billNo ? (
                            <span className="font-mono font-extrabold text-brand-800">
                              {row.billNo}
                            </span>
                          ) : (
                            <span className="rounded bg-amber-50 px-1.5 py-0.5 text-[11px] font-bold text-amber-800">
                              {t('namuna9Ka.draftPendingBadge')}
                            </span>
                          )}
                          {row.isStaleBill && (
                            <span
                              title={t('namuna9Ka.staleBillHint')}
                              className="rounded border border-amber-300 bg-amber-100 px-1.5 py-0.5 text-[10px] font-extrabold text-amber-900"
                            >
                              {t('namuna9Ka.staleBadge')}
                            </span>
                          )}
                        </div>
                        {row.issueDate && (
                          <p className="mt-0.5 text-[10px] text-muted">
                            {formatDateOnly(row.issueDate, i18n.language)}
                            {row.dueDate ? ` → ${formatDateOnly(row.dueDate, i18n.language)}` : ''}
                          </p>
                        )}
                      </td>

                      {/* Ward / Property No */}
                      <td className="px-3 py-3 align-top">
                        <span className="font-mono font-bold text-ink">
                          {row.wardNo} / {row.propertyNo}
                        </span>
                        {row.isTaxExempt && (
                          <span className="mt-0.5 block w-fit rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-600">
                            {t('namuna9Ka.taxExemptTag')}
                          </span>
                        )}
                      </td>

                      {/* Owner + Masked Mobile */}
                      <td className="px-3 py-3 align-top">
                        <p className="font-bold text-ink">{ownerName}</p>
                        {(row.primaryOwnerMobileMasked || row.primaryOwnerAadhaarMasked) && (
                          <p className="mt-0.5 font-mono text-[10px] text-muted">
                            {row.primaryOwnerMobileMasked || row.primaryOwnerAadhaarMasked}
                          </p>
                        )}
                      </td>

                      <td className="px-3 py-3 text-right tabular-nums font-medium text-slate-700">
                        {formatInr(row.arrearsAmount)}
                      </td>
                      <td className="px-3 py-3 text-right tabular-nums font-medium text-slate-700">
                        {formatInr(row.generalDemandTotal)}
                      </td>
                      <td className="px-3 py-3 text-right tabular-nums font-medium text-slate-700">
                        {formatInr(row.waterDemandTotal)}
                      </td>
                      <td className="px-3 py-3 text-right tabular-nums font-medium text-slate-700">
                        {formatInr(row.noticeAndPenaltyAmount)}
                      </td>
                      <td className="px-3 py-3 text-right tabular-nums font-semibold text-emerald-700">
                        {row.rebateAmount > 0 ? `-${formatInr(row.rebateAmount)}` : formatInr(0)}
                      </td>
                      <td className="px-3 py-3 text-right tabular-nums">
                        <span className="font-extrabold text-rose-700">
                          {formatInr(row.netPayableAmount)}
                        </span>
                        {row.paidAmount > 0 && (
                          <span className="block text-[10px] text-emerald-700">
                            {t('namuna9Ka.paidSubHint', { amt: formatInr(row.paidAmount) })}
                          </span>
                        )}
                      </td>

                      {/* Status Badge */}
                      <td className="px-3 py-3 align-top">
                        <span
                          className={`inline-flex rounded-full border px-2.5 py-0.5 text-[11px] font-bold ${billStatusBadgeClass(row.billStatus)}`}
                        >
                          {billStatusLabel(row.billStatus)}
                        </span>
                      </td>

                      {/* Actions */}
                      <td className="px-3 py-3 text-right align-top">
                        <div className="inline-flex items-center justify-end gap-1.5">
                          <button
                            type="button"
                            onClick={() => setPreviewBill(row)}
                            title={t('namuna9Ka.previewBtn')}
                            className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2.5 py-1.5 text-[11px] font-bold text-brand-700 hover:bg-brand-50"
                          >
                            <Eye className="h-3.5 w-3.5" />
                            <QrCode className="h-3.5 w-3.5" />
                            <span className="hidden xl:inline">{t('namuna9Ka.previewBtn')}</span>
                          </button>

                          <button
                            type="button"
                            disabled={rowBusy}
                            onClick={() => handleDownloadSinglePdf(row.propertyId)}
                            title={t('namuna9Ka.downloadSinglePdfBtn')}
                            className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1.5 text-[11px] font-bold text-brand-800 hover:bg-brand-100 disabled:opacity-50"
                          >
                            <Printer className="h-3.5 w-3.5" />
                          </button>

                          <Link
                            to={`/namuna-8/view/${row.propertyId}`}
                            title={t('namuna9Ka.viewProperty360')}
                            className="inline-flex items-center rounded-lg border border-line bg-white p-1.5 text-slate-600 hover:bg-slate-50"
                          >
                            <ExternalLink className="h-3.5 w-3.5" />
                          </Link>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Footer Note */}
      <p className="rounded-xl border border-line bg-slate-50 px-4 py-2.5 text-[11px] text-muted">
        {t('namuna9Ka.footerNote')}
      </p>

      {/* Live UPI QR & Counterfoil Preview Modal */}
      {workspace && previewBill && (
        <Namuna9KaBillPreviewModal
          workspace={workspace}
          bill={previewBill}
          downloadingPdf={downloadingRowId === previewBill.propertyId}
          onClose={() => setPreviewBill(null)}
          onDownloadSinglePdf={handleDownloadSinglePdf}
        />
      )}
    </div>
  );
}
