import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  CheckCircle2,
  ExternalLink,
  FileSpreadsheet,
  FileText,
  Filter,
  Lock,
  Percent,
  Printer,
  RefreshCw,
  RotateCcw,
  Save,
  Search,
  Send,
  ShieldAlert,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import {
  namuna9Api,
  type Namuna9DemandRowDto,
  type Namuna9Format,
  type Namuna9WorkspaceDto,
} from '../../services/namuna9Api';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { EarlyRebateModal, FyRolloverModal, PrintRegisterModal } from './Namuna9Modals';

type SpreadPage = 'demand' | 'recovery';

interface FeeDraft {
  noticeFee: number;
  warrantFee: number;
  penaltyAmount: number;
  rebateAmount: number;
}

function fmtNum(val: number): string {
  return val.toLocaleString('en-IN', { maximumFractionDigits: 2 });
}

function toDraft(row: Namuna9DemandRowDto): FeeDraft {
  return {
    noticeFee: row.noticeFee,
    warrantFee: row.warrantFee,
    penaltyAmount: row.penaltyAmount,
    rebateAmount: row.rebateAmount,
  };
}

function isDraftDirty(row: Namuna9DemandRowDto, draft: FeeDraft | undefined): boolean {
  if (!draft) return false;
  return (
    draft.noticeFee !== row.noticeFee ||
    draft.warrantFee !== row.warrantFee ||
    draft.penaltyAmount !== row.penaltyAmount ||
    draft.rebateAmount !== row.rebateAmount
  );
}

function computeLiveRowTotals(
  row: Namuna9DemandRowDto,
  draft: FeeDraft | undefined,
  format: Namuna9Format,
) {
  const notice = draft ? draft.noticeFee : row.noticeFee;
  const warrant = draft ? draft.warrantFee : row.warrantFee;
  const penalty = draft ? draft.penaltyAmount : row.penaltyAmount;
  const rebate = draft ? draft.rebateAmount : row.rebateAmount;

  const feeDelta =
    notice - row.noticeFee + (warrant - row.warrantFee) + (penalty - row.penaltyAmount) - (rebate - row.rebateAmount);

  const netCombined = Math.max(0, row.netDemand + feeDelta);
  const balanceCombined = Math.max(0, netCombined - row.paidAmount);

  const waterDemand = row.generalWaterTax.total + row.specialWaterTax.total;
  const waterCollected = row.collectedByHead.generalWaterTax + row.collectedByHead.specialWaterTax;
  const waterBalance = row.balanceByHead.generalWaterTax + row.balanceByHead.specialWaterTax;

  if (format === 'B') {
    return {
      notice: 0,
      warrant: 0,
      penalty: 0,
      rebate: 0,
      demand: waterDemand,
      collected: waterCollected,
      balance: waterBalance,
    };
  }

  if (format === 'A') {
    return {
      notice,
      warrant,
      penalty,
      rebate,
      demand: Math.max(0, netCombined - waterDemand),
      collected: Math.max(0, row.paidAmount - waterCollected),
      balance: Math.max(0, balanceCombined - waterBalance),
    };
  }

  return {
    notice,
    warrant,
    penalty,
    rebate,
    demand: netCombined,
    collected: row.paidAmount,
    balance: balanceCombined,
  };
}

export function Namuna9DemandRegisterPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [selectedYearId, setSelectedYearId] = useState<string | undefined>(undefined);
  const [format, setFormat] = useState<Namuna9Format>('C');
  const [spreadPage, setSpreadPage] = useState<SpreadPage>('demand');
  const [wardFilter, setWardFilter] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [pendingOnly, setPendingOnly] = useState(false);

  const [drafts, setDrafts] = useState<Record<string, FeeDraft>>({});
  const [batchRemarks, setBatchRemarks] = useState('');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  const [savingAdjustments, setSavingAdjustments] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [showRebateModal, setShowRebateModal] = useState(false);
  const [showRolloverModal, setShowRolloverModal] = useState(false);
  const [showPrintModal, setShowPrintModal] = useState(false);

  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor', 'Auditor'].includes(
        user.role,
      ),
  );

  const canAdminWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin'].includes(user.role),
  );

  const registerQuery = useQuery({
    queryKey: [
      'namuna9-register',
      user?.activeTenantId,
      selectedYearId ?? 'active',
      wardFilter,
      appliedSearch,
      format,
      pendingOnly,
    ],
    queryFn: () =>
      namuna9Api.getRegister({
        yearId: selectedYearId,
        wardNo: wardFilter || undefined,
        search: appliedSearch || undefined,
        format,
        pendingOnly,
      }),
    enabled: canRead,
  });

  const wardsIndexQuery = useQuery({
    queryKey: ['namuna9-wards-index', user?.activeTenantId, selectedYearId ?? 'active'],
    queryFn: () =>
      namuna9Api.getRegister({
        yearId: selectedYearId,
        format: 'C',
      }),
    enabled: canRead,
    staleTime: 60_000,
  });

  const workspace: Namuna9WorkspaceDto | undefined = registerQuery.data;

  const wardOptions = useMemo(() => {
    const items = wardsIndexQuery.data?.items ?? workspace?.items ?? [];
    const unique = new Set(items.map((r) => r.wardNo).filter((w) => w.trim().length > 0));
    if (wardFilter) unique.add(wardFilter);
    return Array.from(unique).sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  }, [wardsIndexQuery.data, workspace, wardFilter]);

  const rows = useMemo(() => workspace?.items ?? [], [workspace]);
  const isLocked = workspace?.selectedYear.isLocked ?? false;
  const canEditFees = canAdminWrite && !isLocked && format !== 'B';

  const dirtyRows = useMemo(() => {
    return rows.filter((r) => isDraftDirty(r, drafts[r.propertyId]));
  }, [rows, drafts]);

  const draftStatusCount = useMemo(
    () => rows.filter((r) => r.status.toLowerCase() === 'draft').length,
    [rows],
  );

  const liveTotals = useMemo(() => {
    let demand = 0;
    let collected = 0;
    let rebate = 0;
    let balance = 0;
    let houseArr = 0;
    let houseCur = 0;
    let lightingArr = 0;
    let lightingCur = 0;
    let healthArr = 0;
    let healthCur = 0;
    let sanArr = 0;
    let sanCur = 0;
    let otherArr = 0;
    let otherCur = 0;
    let genWaterArr = 0;
    let genWaterCur = 0;
    let specWaterArr = 0;
    let specWaterCur = 0;
    let notice = 0;
    let warrant = 0;
    let penalty = 0;

    for (const r of rows) {
      const calc = computeLiveRowTotals(r, drafts[r.propertyId], format);
      demand += calc.demand;
      collected += calc.collected;
      rebate += calc.rebate;
      balance += calc.balance;
      houseArr += r.houseTax.arrears;
      houseCur += r.houseTax.current;
      lightingArr += r.lightingTax.arrears;
      lightingCur += r.lightingTax.current;
      healthArr += r.healthTax.arrears;
      healthCur += r.healthTax.current;
      sanArr += r.sanitationTax.arrears + r.toiletTax.arrears;
      sanCur += r.sanitationTax.current + r.toiletTax.current;
      otherArr += r.otherTax.arrears;
      otherCur += r.otherTax.current;
      genWaterArr += r.generalWaterTax.arrears;
      genWaterCur += r.generalWaterTax.current;
      specWaterArr += r.specialWaterTax.arrears;
      specWaterCur += r.specialWaterTax.current;
      notice += calc.notice;
      warrant += calc.warrant;
      penalty += calc.penalty + (format === 'B' ? 0 : r.carriedFeesAndPenalty);
    }

    return {
      demand,
      collected,
      rebate,
      balance,
      houseArr,
      houseCur,
      houseTot: houseArr + houseCur,
      lightingArr,
      lightingCur,
      lightingTot: lightingArr + lightingCur,
      healthArr,
      healthCur,
      healthTot: healthArr + healthCur,
      sanArr,
      sanCur,
      sanTot: sanArr + sanCur,
      otherArr,
      otherCur,
      otherTot: otherArr + otherCur,
      genWaterArr,
      genWaterCur,
      genWaterTot: genWaterArr + genWaterCur,
      specWaterArr,
      specWaterCur,
      specWaterTot: specWaterArr + specWaterCur,
      notice,
      warrant,
      penalty,
    };
  }, [rows, drafts, format]);

  const invalidateNamuna9Caches = async () => {
    await queryClient.invalidateQueries({ queryKey: ['namuna9-register'] });
    await queryClient.invalidateQueries({ queryKey: ['namuna9-wards-index'] });
    await queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] });
  };

  const handleFeeChange = (row: Namuna9DemandRowDto, field: keyof FeeDraft, rawValue: string) => {
    const parsed = Number(rawValue);
    const cleanVal = Number.isFinite(parsed) && parsed >= 0 ? Math.round(parsed * 100) / 100 : 0;
    setDrafts((prev) => {
      const current = prev[row.propertyId] ?? toDraft(row);
      return {
        ...prev,
        [row.propertyId]: {
          ...current,
          [field]: cleanVal,
        },
      };
    });
  };

  const handleSaveAdjustments = async () => {
    if (!workspace || dirtyRows.length === 0) return;
    setSavingAdjustments(true);
    setErrSummary(null);
    setErrItems([]);
    setActionSuccess(null);
    try {
      const adjustments = dirtyRows.map((r) => {
        const d = drafts[r.propertyId] ?? toDraft(r);
        return {
          propertyId: r.propertyId,
          noticeFee: d.noticeFee,
          warrantFee: d.warrantFee,
          penaltyAmount: d.penaltyAmount,
          rebateAmount: d.rebateAmount,
          remarks: r.remarks ?? undefined,
        };
      });

      const res = await namuna9Api.adjustFees({
        assessmentYearId: workspace.selectedYear.id,
        adjustments,
        remarks: batchRemarks.trim() || undefined,
      });

      setDrafts({});
      setBatchRemarks('');
      setActionSuccess(
        t('namuna9.adjustmentsSavedMsg', { count: res.updatedCount, year: res.yearLabel }),
      );
      await invalidateNamuna9Caches();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9.errAdjustFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSavingAdjustments(false);
    }
  };

  const handlePublishDrafts = async () => {
    if (!workspace) return;
    setPublishing(true);
    setErrSummary(null);
    setErrItems([]);
    setActionSuccess(null);
    try {
      const res = await namuna9Api.publishDrafts({
        assessmentYearId: workspace.selectedYear.id,
        propertyIds: selectedIds.length > 0 ? selectedIds : undefined,
      });
      setSelectedIds([]);
      setActionSuccess(
        t('namuna9.publishSuccessMsg', {
          published: res.publishedCount,
          skipped: res.skippedCount,
          year: res.yearLabel,
        }),
      );
      await invalidateNamuna9Caches();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9.errPublishFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setPublishing(false);
    }
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

  const switchYear = (yearId: string) => {
    setSelectedYearId(yearId);
    setDrafts({});
    setSelectedIds([]);
    setActionSuccess(null);
    setErrSummary(null);
    setErrItems([]);
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
      {/* Header & Statutory Action Strip */}
      <section className="rounded-2xl border border-line bg-card p-4 shadow-xs sm:p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1.5">
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted">
              <Link to="/properties" className="font-semibold text-brand-700 hover:underline">
                {t('nav.properties')}
              </Link>
              <span>/</span>
              <span>{t('nav.namuna9')}</span>
              {workspace?.formulaRates && (
                <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-[11px] font-bold text-emerald-800">
                  {t('namuna9.formulaRebateBadge', {
                    pct: workspace.formulaRates.earlyRebatePct,
                    cutoff: formatDateOnly(workspace.formulaRates.defaultRebateCutoffDate, i18n.language),
                  })}
                </span>
              )}
            </div>
            <h1 className="text-xl font-extrabold tracking-tight text-ink sm:text-2xl">
              {t('namuna9.pageTitle')}
            </h1>
            <p className="text-xs text-muted sm:text-sm">{t('namuna9.pageSubtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {canAdminWrite && (
              <>
                <button
                  type="button"
                  disabled={isLocked || publishing || rows.length === 0}
                  onClick={handlePublishDrafts}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-emerald-300 bg-emerald-50 px-3.5 py-2 text-xs font-bold text-emerald-800 hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Send className="h-3.5 w-3.5" />
                  {publishing
                    ? t('namuna9.processingLabel')
                    : t('namuna9.publishDraftsBtn', { count: draftStatusCount })}
                </button>

                <button
                  type="button"
                  disabled={isLocked || rows.length === 0}
                  onClick={() => setShowRebateModal(true)}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-amber-300 bg-amber-50 px-3.5 py-2 text-xs font-bold text-amber-800 hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Percent className="h-3.5 w-3.5" />
                  {t('namuna9.applyRebateBtn')}
                </button>

                <button
                  type="button"
                  disabled={isLocked || !workspace}
                  onClick={() => setShowRolloverModal(true)}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-rose-300 bg-rose-50 px-3.5 py-2 text-xs font-bold text-rose-800 hover:bg-rose-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <RefreshCw className="h-3.5 w-3.5" />
                  {t('namuna9.fyRolloverBtn')}
                </button>
              </>
            )}

            <button
              type="button"
              disabled={!workspace}
              onClick={() => setShowPrintModal(true)}
              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-xs font-bold text-white shadow-xs hover:bg-brand-800 disabled:opacity-50"
            >
              <Printer className="h-3.5 w-3.5" />
              {t('namuna9.printAndExportBtn')}
            </button>

            <Link
              to={
                workspace
                  ? `/namuna-9ka/generate?yearId=${encodeURIComponent(workspace.selectedYear.id)}`
                  : '/namuna-9ka/generate'
              }
              className="inline-flex items-center gap-1.5 rounded-xl border border-brand-200 bg-brand-50 px-3.5 py-2 text-xs font-bold text-brand-800 hover:bg-brand-100"
            >
              <FileSpreadsheet className="h-3.5 w-3.5" />
              {t('namuna9Ka.openBillsWorkspaceBtn')}
            </Link>
          </div>
        </div>

        {/* Assessment Year Selector Pills */}
        {workspace && workspace.assessmentYears.length > 0 && (
          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-line pt-3">
            <span className="text-[11px] font-bold uppercase tracking-wider text-muted">
              {t('namuna9.assessmentYearLabel')}:
            </span>
            {workspace.assessmentYears.map((yr) => {
              const selected = yr.id === workspace.selectedYear.id;
              return (
                <button
                  key={yr.id}
                  type="button"
                  onClick={() => switchYear(yr.id)}
                  className={`inline-flex items-center gap-1.5 rounded-xl border px-3 py-1.5 text-xs font-bold transition ${
                    selected
                      ? 'border-brand-700 bg-brand-700 text-white shadow-2xs'
                      : 'border-line bg-white text-slate-700 hover:bg-slate-50'
                  }`}
                >
                  {yr.isLocked && <Lock className="h-3 w-3" />}
                  <span>{yr.yearLabel}</span>
                  {yr.isActive && (
                    <span
                      className={`rounded px-1.5 py-0.5 text-[10px] ${
                        selected ? 'bg-white/20 text-white' : 'bg-emerald-100 text-emerald-800'
                      }`}
                    >
                      {t('namuna9.yearActiveBadge')}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        )}
      </section>

      {/* Locked / Role / Validation / Success Banners */}
      {isLocked && workspace && (
        <div className="flex items-center gap-2.5 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-xs font-semibold text-amber-900">
          <Lock className="h-4 w-4 shrink-0 text-amber-700" />
          <span>{t('namuna9.yearLockedBanner', { year: workspace.selectedYear.yearLabel })}</span>
        </div>
      )}

      {!isLocked && !canAdminWrite && (
        <div className="flex items-center gap-2.5 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-xs font-medium text-slate-700">
          <AlertCircle className="h-4 w-4 shrink-0 text-slate-500" />
          <span>{t('namuna9.readOnlyRoleHint')}</span>
        </div>
      )}

      {actionSuccess && (
        <div
          role="status"
          className="flex items-center justify-between gap-2 rounded-2xl border border-emerald-300 bg-emerald-50 px-4 py-3 text-xs font-bold text-emerald-900"
        >
          <span className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-700" />
            {actionSuccess}
          </span>
          <button
            type="button"
            onClick={() => setActionSuccess(null)}
            className="text-emerald-700 hover:underline"
          >
            {t('namuna9.closeBtn')}
          </button>
        </div>
      )}

      <ValidationSummary summary={errSummary} items={errItems} />

      {/* 4 Statutory KPI Summary Cards */}
      {workspace && (
        <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="rounded-2xl border border-line border-l-4 border-l-sky-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9.kpiTotalDemand')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-ink">
              {formatInr(liveTotals.demand)}
            </p>
            <p className="mt-1 text-[11px] text-muted">
              {t('namuna9.kpiDemandSub', {
                count: rows.length,
                general: formatInr(workspace.kpis.generalTaxDemand),
                water: formatInr(workspace.kpis.waterTaxDemand),
              })}
            </p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-emerald-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9.kpiTotalCollected')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-emerald-700">
              {formatInr(liveTotals.collected)}
            </p>
            <p className="mt-1 text-[11px] text-muted">{t('namuna9.kpiCollectedSub')}</p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-amber-500 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9.kpiTotalRebate')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-amber-700">
              {formatInr(liveTotals.rebate)}
            </p>
            <p className="mt-1 text-[11px] text-muted">
              {t('namuna9.kpiRebateSub', { pct: workspace.formulaRates.earlyRebatePct })}
            </p>
          </div>

          <div className="rounded-2xl border border-line border-l-4 border-l-rose-600 bg-card p-4 shadow-2xs">
            <p className="text-xs font-semibold text-muted">{t('namuna9.kpiBalanceDues')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-rose-700">
              {formatInr(liveTotals.balance)}
            </p>
            <p className="mt-1 text-[11px] text-muted">{t('namuna9.kpiBalanceSub')}</p>
          </div>
        </section>
      )}

      {/* Filter Bar, Format Switcher & 2-Page Spread Tabs */}
      <section className="rounded-2xl border border-line bg-card p-4 shadow-2xs">
        <div className="flex flex-wrap items-center justify-between gap-3">
          {/* Search + Ward + Pending */}
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                type="search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') setAppliedSearch(searchInput.trim());
                }}
                placeholder={t('namuna9.searchPlaceholder')}
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

            <select
              value={wardFilter}
              aria-label={t('namuna9.wardFilterLabel')}
              onChange={(e) => setWardFilter(e.target.value)}
              className="rounded-xl border border-line bg-white px-3 py-1.5 text-xs font-semibold text-ink focus:border-brand-600 focus:outline-none"
            >
              <option value="">{t('namuna9.allWards')}</option>
              {wardOptions.map((w) => (
                <option key={w} value={w}>
                  {t('namuna9.wardOption', { ward: w })}
                </option>
              ))}
            </select>

            <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-1.5 text-xs font-semibold text-slate-700">
              <input
                type="checkbox"
                checked={pendingOnly}
                onChange={(e) => setPendingOnly(e.target.checked)}
                className="h-3.5 w-3.5 rounded border-slate-300 text-brand-700"
              />
              <span>{t('namuna9.pendingOnlyLabel')}</span>
            </label>
          </div>

          {/* Format Switcher A/B/C & Spread Toggle Page 1/Page 2 */}
          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex overflow-hidden rounded-xl border border-line bg-slate-50 p-0.5 text-xs font-bold">
              {(['C', 'A', 'B'] as const).map((fmt) => (
                <button
                  key={fmt}
                  type="button"
                  onClick={() => {
                    setFormat(fmt);
                    setDrafts({});
                  }}
                  className={`rounded-lg px-2.5 py-1 transition ${
                    format === fmt
                      ? 'bg-brand-700 text-white shadow-2xs'
                      : 'text-slate-600 hover:text-ink'
                  }`}
                >
                  {t(`namuna9.formatShort${fmt}`)}
                </button>
              ))}
            </div>

            <div className="inline-flex overflow-hidden rounded-xl border border-line bg-slate-50 p-0.5 text-xs font-bold">
              <button
                type="button"
                onClick={() => setSpreadPage('demand')}
                className={`rounded-lg px-3 py-1 transition ${
                  spreadPage === 'demand'
                    ? 'bg-slate-900 text-white shadow-2xs'
                    : 'text-slate-600 hover:text-ink'
                }`}
              >
                {t('namuna9.spreadDemandTab')}
              </button>
              <button
                type="button"
                onClick={() => setSpreadPage('recovery')}
                className={`rounded-lg px-3 py-1 transition ${
                  spreadPage === 'recovery'
                    ? 'bg-slate-900 text-white shadow-2xs'
                    : 'text-slate-600 hover:text-ink'
                }`}
              >
                {t('namuna9.spreadRecoveryTab')}
              </button>
            </div>
          </div>
        </div>

        {/* Sticky Save Dirty Bar */}
        {dirtyRows.length > 0 && (
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50 px-3.5 py-2.5">
            <div className="flex flex-wrap items-center gap-2 text-xs font-bold text-amber-900">
              <span>{t('namuna9.unsavedChangesCount', { count: dirtyRows.length })}</span>
              <input
                type="text"
                value={batchRemarks}
                onChange={(e) => setBatchRemarks(e.target.value)}
                placeholder={t('namuna9.remarksPlaceholder')}
                className="w-56 rounded-lg border border-amber-300 bg-white px-2.5 py-1 text-xs font-normal text-ink focus:outline-none sm:w-72"
              />
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => {
                  setDrafts({});
                  setBatchRemarks('');
                }}
                className="inline-flex items-center gap-1 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-semibold text-amber-900 hover:bg-amber-100"
              >
                <RotateCcw className="h-3.5 w-3.5" />
                {t('namuna9.discardChangesBtn')}
              </button>
              <button
                type="button"
                disabled={savingAdjustments}
                onClick={handleSaveAdjustments}
                className="inline-flex items-center gap-1.5 rounded-lg bg-brand-700 px-3.5 py-1.5 text-xs font-bold text-white hover:bg-brand-800 disabled:opacity-50"
              >
                <Save className="h-3.5 w-3.5" />
                {savingAdjustments
                  ? t('namuna9.processingLabel')
                  : t('namuna9.saveChangesBtn', { count: dirtyRows.length })}
              </button>
            </div>
          </div>
        )}
      </section>

      {/* Main Spread Data Table */}
      <section className="overflow-hidden rounded-2xl border border-line bg-card shadow-xs">
        {registerQuery.isLoading ? (
          <div className="space-y-2 p-6">
            <div className="h-8 animate-pulse rounded-lg bg-slate-200/70" />
            <div className="h-8 animate-pulse rounded-lg bg-slate-200/70" />
            <div className="h-8 animate-pulse rounded-lg bg-slate-200/70" />
          </div>
        ) : rows.length === 0 ? (
          <div className="p-10 text-center text-sm text-muted">
            <FileText className="mx-auto mb-2 h-8 w-8 text-slate-400" />
            <p className="font-semibold text-ink">{t('namuna9.emptyTitle')}</p>
            <p className="mt-1 text-xs">{t('namuna9.emptySubtitle')}</p>
          </div>
        ) : spreadPage === 'demand' ? (
          <div className="overflow-x-auto">
            <table className="min-w-full border-collapse text-xs">
              <thead>
                <tr className="bg-brand-800 text-white">
                  <th rowSpan={2} className="border border-brand-700 px-2 py-2 text-center">
                    <input
                      type="checkbox"
                      aria-label={t('namuna9.selectAllLabel')}
                      checked={rows.length > 0 && selectedIds.length === rows.length}
                      onChange={(e) => handleSelectAll(e.target.checked)}
                      className="h-3.5 w-3.5 rounded"
                    />
                  </th>
                  <th rowSpan={2} className="border border-brand-700 px-2 py-2 text-center">
                    {t('namuna9.colSr')}
                  </th>
                  <th rowSpan={2} className="border border-brand-700 px-2 py-2 text-center">
                    {t('namuna9.colWardProp')}
                  </th>
                  <th rowSpan={2} className="min-w-[160px] border border-brand-700 px-3 py-2 text-left">
                    {t('namuna9.colOwner')}
                  </th>

                  {format !== 'B' && (
                    <>
                      <th colSpan={3} className="border border-brand-700 bg-brand-700 px-2 py-1 text-center">
                        {t('namuna9.headHouse')}
                      </th>
                      <th colSpan={3} className="border border-brand-700 bg-brand-700 px-2 py-1 text-center">
                        {t('namuna9.headLighting')}
                      </th>
                      <th colSpan={3} className="border border-brand-700 bg-brand-700 px-2 py-1 text-center">
                        {t('namuna9.headHealth')}
                      </th>
                      <th colSpan={3} className="border border-brand-700 bg-brand-700 px-2 py-1 text-center">
                        {t('namuna9.headSanitation')}
                      </th>
                      <th colSpan={3} className="border border-brand-700 bg-brand-700 px-2 py-1 text-center">
                        {t('namuna9.headOther')}
                      </th>
                    </>
                  )}

                  {format !== 'A' && (
                    <>
                      <th colSpan={3} className="border border-brand-700 bg-sky-800 px-2 py-1 text-center">
                        {t('namuna9.headGenWater')}
                      </th>
                      <th colSpan={3} className="border border-brand-700 bg-sky-800 px-2 py-1 text-center">
                        {t('namuna9.headSpecWater')}
                      </th>
                    </>
                  )}

                  {format !== 'B' && (
                    <>
                      <th rowSpan={2} className="border border-brand-700 bg-amber-900/70 px-2 py-2 text-center">
                        {t('namuna9.colNoticeFee')}
                      </th>
                      <th rowSpan={2} className="border border-brand-700 bg-amber-900/70 px-2 py-2 text-center">
                        {t('namuna9.colWarrantFee')}
                      </th>
                      <th rowSpan={2} className="border border-brand-700 bg-amber-900/70 px-2 py-2 text-center">
                        {t('namuna9.colPenalty')}
                      </th>
                      <th rowSpan={2} className="border border-brand-700 bg-amber-900/70 px-2 py-2 text-center">
                        {t('namuna9.colRebate')}
                      </th>
                    </>
                  )}

                  <th rowSpan={2} className="border border-brand-700 bg-emerald-900 px-3 py-2 text-right">
                    {t('namuna9.colNetDemand')}
                  </th>
                  <th rowSpan={2} className="border border-brand-700 px-2 py-2 text-center">
                    {t('namuna9.colStatusActions')}
                  </th>
                </tr>

                <tr className="bg-slate-100 text-[11px] font-bold text-slate-700">
                  {format !== 'B' &&
                    [0, 1, 2, 3, 4].map((idx) => (
                      <SubHeadGroup
                        key={`gen-sub-${idx}`}
                        arrLabel={t('namuna9.subArr')}
                        curLabel={t('namuna9.subCur')}
                        totLabel={t('namuna9.subTot')}
                      />
                    ))}
                  {format !== 'A' &&
                    [0, 1].map((idx) => (
                      <SubHeadGroup
                        key={`wat-sub-${idx}`}
                        arrLabel={t('namuna9.subArr')}
                        curLabel={t('namuna9.subCur')}
                        totLabel={t('namuna9.subTot')}
                      />
                    ))}
                </tr>
              </thead>

              <tbody>
                {rows.map((row, idx) => {
                  const rowDraft = drafts[row.propertyId];
                  const dirty = isDraftDirty(row, rowDraft);
                  const live = computeLiveRowTotals(row, rowDraft, format);
                  const currentNotice = rowDraft ? rowDraft.noticeFee : row.noticeFee;
                  const currentWarrant = rowDraft ? rowDraft.warrantFee : row.warrantFee;
                  const currentPenalty = rowDraft ? rowDraft.penaltyAmount : row.penaltyAmount;
                  const currentRebate = rowDraft ? rowDraft.rebateAmount : row.rebateAmount;

                  return (
                    <tr
                      key={row.demandId}
                      className={`border-b border-line transition ${
                        dirty ? 'bg-amber-50/70' : idx % 2 === 1 ? 'bg-slate-50/60' : 'bg-white'
                      }`}
                    >
                      <td className="border-r border-line px-2 py-2 text-center">
                        <input
                          type="checkbox"
                          checked={selectedIds.includes(row.propertyId)}
                          onChange={() => toggleRowSelection(row.propertyId)}
                          className="h-3.5 w-3.5 rounded border-slate-300"
                        />
                      </td>
                      <td className="border-r border-line px-2 py-2 text-center tabular-nums text-muted">
                        {idx + 1}
                      </td>
                      <td className="border-r border-line px-2 py-2 text-center font-bold tabular-nums text-ink">
                        {row.wardNo}/{row.propertyNo}
                      </td>
                      <td className="border-r border-line px-3 py-2">
                        <p className="font-bold text-ink">
                          {row.primaryOwnerNameMr || row.primaryOwnerNameEn || '—'}
                        </p>
                        <div className="mt-0.5 flex flex-wrap gap-1">
                          {row.isTaxExempt && (
                            <span className="rounded bg-purple-100 px-1.5 py-0.5 text-[10px] font-bold text-purple-800">
                              {t('namuna9.badgeExempt')}
                            </span>
                          )}
                          {row.earlyRebateApplied && (
                            <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-bold text-amber-800">
                              {t('namuna9.badgeEarlyRebate')}
                            </span>
                          )}
                        </div>
                      </td>

                      {format !== 'B' && (
                        <>
                          <HeadCells
                            arrears={row.houseTax.arrears}
                            current={row.houseTax.current}
                            total={row.houseTax.total}
                          />
                          <HeadCells
                            arrears={row.lightingTax.arrears}
                            current={row.lightingTax.current}
                            total={row.lightingTax.total}
                          />
                          <HeadCells
                            arrears={row.healthTax.arrears}
                            current={row.healthTax.current}
                            total={row.healthTax.total}
                          />
                          <HeadCells
                            arrears={row.sanitationTax.arrears + row.toiletTax.arrears}
                            current={row.sanitationTax.current + row.toiletTax.current}
                            total={row.sanitationTax.total + row.toiletTax.total}
                          />
                          <HeadCells
                            arrears={row.otherTax.arrears}
                            current={row.otherTax.current}
                            total={row.otherTax.total}
                          />
                        </>
                      )}

                      {format !== 'A' && (
                        <>
                          <HeadCells
                            arrears={row.generalWaterTax.arrears}
                            current={row.generalWaterTax.current}
                            total={row.generalWaterTax.total}
                          />
                          <HeadCells
                            arrears={row.specialWaterTax.arrears}
                            current={row.specialWaterTax.current}
                            total={row.specialWaterTax.total}
                          />
                        </>
                      )}

                      {format !== 'B' && (
                        <>
                          <td className="border-r border-line px-1.5 py-1.5 text-right">
                            <input
                              type="number"
                              min={0}
                              step="1"
                              disabled={!canEditFees}
                              aria-label={`${t('namuna9.colNoticeFee')} ${row.wardNo}/${row.propertyNo}`}
                              value={currentNotice}
                              onChange={(e) => handleFeeChange(row, 'noticeFee', e.target.value)}
                              className="w-16 rounded-lg border border-line bg-white px-1.5 py-1 text-right text-xs tabular-nums disabled:bg-slate-100"
                            />
                          </td>
                          <td className="border-r border-line px-1.5 py-1.5 text-right">
                            <input
                              type="number"
                              min={0}
                              step="1"
                              disabled={!canEditFees}
                              aria-label={`${t('namuna9.colWarrantFee')} ${row.wardNo}/${row.propertyNo}`}
                              value={currentWarrant}
                              onChange={(e) => handleFeeChange(row, 'warrantFee', e.target.value)}
                              className="w-16 rounded-lg border border-line bg-white px-1.5 py-1 text-right text-xs tabular-nums disabled:bg-slate-100"
                            />
                          </td>
                          <td className="border-r border-line px-1.5 py-1.5 text-right">
                            <div className="flex flex-col items-end gap-0.5">
                              <input
                                type="number"
                                min={0}
                                step="1"
                                disabled={!canEditFees}
                                aria-label={`${t('namuna9.colPenalty')} ${row.wardNo}/${row.propertyNo}`}
                                value={currentPenalty}
                                onChange={(e) => handleFeeChange(row, 'penaltyAmount', e.target.value)}
                                className="w-16 rounded-lg border border-line bg-white px-1.5 py-1 text-right text-xs tabular-nums disabled:bg-slate-100"
                              />
                              {row.carriedFeesAndPenalty > 0 && (
                                <span className="text-[10px] text-amber-700 tabular-nums">
                                  +{fmtNum(row.carriedFeesAndPenalty)}
                                </span>
                              )}
                            </div>
                          </td>
                          <td className="border-r border-line px-1.5 py-1.5 text-right">
                            <input
                              type="number"
                              min={0}
                              step="1"
                              disabled={!canEditFees}
                              aria-label={`${t('namuna9.colRebate')} ${row.wardNo}/${row.propertyNo}`}
                              value={currentRebate}
                              onChange={(e) => handleFeeChange(row, 'rebateAmount', e.target.value)}
                              className="w-16 rounded-lg border border-line bg-white px-1.5 py-1 text-right text-xs tabular-nums disabled:bg-slate-100"
                            />
                          </td>
                        </>
                      )}

                      <td className="border-r border-line bg-emerald-50/60 px-3 py-2 text-right font-extrabold tabular-nums text-emerald-900">
                        {formatInr(live.demand)}
                      </td>
                      <td className="px-2 py-2 text-center">
                        <div className="flex flex-col items-center gap-1">
                          <StatusBadge status={row.status} />
                          <div className="flex items-center gap-1.5">
                            <Link
                              to={`/namuna-8/view/${row.propertyId}`}
                              className="inline-flex items-center gap-0.5 text-[11px] font-semibold text-brand-700 hover:underline"
                            >
                              {t('namuna9.link360')}
                              <ExternalLink className="h-3 w-3" />
                            </Link>
                            <Link
                              to={`/namuna-9ka/generate?propertyId=${row.propertyId}`}
                              className="text-[11px] font-semibold text-indigo-700 hover:underline"
                            >
                              {t('namuna9.link9Ka')}
                            </Link>
                          </div>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>

              <tfoot>
                <tr className="border-t-2 border-slate-400 bg-emerald-50 font-extrabold text-ink">
                  <td colSpan={4} className="border-r border-line px-3 py-2.5 text-right">
                    {t('namuna9.totalsRowLabel', { count: rows.length })}
                  </td>
                  {format !== 'B' && (
                    <>
                      <HeadCells
                        arrears={liveTotals.houseArr}
                        current={liveTotals.houseCur}
                        total={liveTotals.houseTot}
                      />
                      <HeadCells
                        arrears={liveTotals.lightingArr}
                        current={liveTotals.lightingCur}
                        total={liveTotals.lightingTot}
                      />
                      <HeadCells
                        arrears={liveTotals.healthArr}
                        current={liveTotals.healthCur}
                        total={liveTotals.healthTot}
                      />
                      <HeadCells
                        arrears={liveTotals.sanArr}
                        current={liveTotals.sanCur}
                        total={liveTotals.sanTot}
                      />
                      <HeadCells
                        arrears={liveTotals.otherArr}
                        current={liveTotals.otherCur}
                        total={liveTotals.otherTot}
                      />
                    </>
                  )}
                  {format !== 'A' && (
                    <>
                      <HeadCells
                        arrears={liveTotals.genWaterArr}
                        current={liveTotals.genWaterCur}
                        total={liveTotals.genWaterTot}
                      />
                      <HeadCells
                        arrears={liveTotals.specWaterArr}
                        current={liveTotals.specWaterCur}
                        total={liveTotals.specWaterTot}
                      />
                    </>
                  )}
                  {format !== 'B' && (
                    <>
                      <td className="border-r border-line px-2 py-2 text-right tabular-nums">
                        {fmtNum(liveTotals.notice)}
                      </td>
                      <td className="border-r border-line px-2 py-2 text-right tabular-nums">
                        {fmtNum(liveTotals.warrant)}
                      </td>
                      <td className="border-r border-line px-2 py-2 text-right tabular-nums">
                        {fmtNum(liveTotals.penalty)}
                      </td>
                      <td className="border-r border-line px-2 py-2 text-right tabular-nums">
                        {fmtNum(liveTotals.rebate)}
                      </td>
                    </>
                  )}
                  <td className="border-r border-line bg-emerald-100 px-3 py-2 text-right tabular-nums text-emerald-950">
                    {formatInr(liveTotals.demand)}
                  </td>
                  <td className="px-2 py-2" />
                </tr>
              </tfoot>
            </table>
          </div>
        ) : (
          /* Page 2: Recovery & Balance Register (वसुली व बाकी नोंदवही) */
          <div className="overflow-x-auto">
            <table className="min-w-full border-collapse text-xs">
              <thead>
                <tr className="text-white">
                  <th rowSpan={2} className="border border-slate-700 bg-slate-900 px-2 py-2 text-center">
                    {t('namuna9.colSr')}
                  </th>
                  <th rowSpan={2} className="border border-slate-700 bg-slate-900 px-2 py-2 text-center">
                    {t('namuna9.colWardProp')}
                  </th>
                  <th rowSpan={2} className="min-w-[150px] border border-slate-700 bg-slate-900 px-3 py-2 text-left">
                    {t('namuna9.colOwner')}
                  </th>
                  <th
                    colSpan={format === 'C' ? 9 : format === 'A' ? 7 : 3}
                    className="border border-emerald-700 bg-emerald-800 px-2 py-1 text-center"
                  >
                    {t('namuna9.groupRecovery')}
                  </th>
                  <th colSpan={2} className="border border-amber-700 bg-amber-800 px-2 py-1 text-center">
                    {t('namuna9.groupRebate')}
                  </th>
                  <th
                    colSpan={format === 'C' ? 9 : format === 'A' ? 7 : 3}
                    className="border border-rose-700 bg-rose-900 px-2 py-1 text-center"
                  >
                    {t('namuna9.groupBalance')}
                  </th>
                  <th rowSpan={2} className="border border-slate-700 bg-slate-900 px-2 py-2 text-center">
                    {t('namuna9.colStatusActions')}
                  </th>
                </tr>

                <tr className="bg-slate-100 text-[11px] font-bold text-slate-700">
                  {format !== 'B' && (
                    <>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortHouse')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortLighting')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortHealth')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortSanitation')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortOther')}</th>
                    </>
                  )}
                  {format !== 'A' && (
                    <>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortGenWater')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortSpecWater')}</th>
                    </>
                  )}
                  {format !== 'B' && (
                    <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortFees')}</th>
                  )}
                  <th className="border border-line bg-emerald-50 px-2 py-1.5 text-right text-emerald-900">
                    {t('namuna9.colTotalCollected')}
                  </th>

                  <th className="border border-line bg-amber-50 px-2 py-1.5 text-right text-amber-900">
                    {t('namuna9.colRebateGranted')}
                  </th>
                  <th className="border border-line bg-amber-50 px-2 py-1.5 text-center text-amber-900">
                    {t('namuna9.colCutoff')}
                  </th>

                  {format !== 'B' && (
                    <>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortHouse')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortLighting')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortHealth')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortSanitation')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortOther')}</th>
                    </>
                  )}
                  {format !== 'A' && (
                    <>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortGenWater')}</th>
                      <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortSpecWater')}</th>
                    </>
                  )}
                  {format !== 'B' && (
                    <th className="border border-line px-2 py-1.5 text-right">{t('namuna9.shortFees')}</th>
                  )}
                  <th className="border border-line bg-rose-50 px-2 py-1.5 text-right text-rose-900">
                    {t('namuna9.colPendingDues')}
                  </th>
                </tr>
              </thead>

              <tbody>
                {rows.map((row, idx) => {
                  const live = computeLiveRowTotals(row, drafts[row.propertyId], format);
                  const feesPaid =
                    row.collectedByHead.noticeFeePaid +
                    row.collectedByHead.warrantFeePaid +
                    row.collectedByHead.penaltyPaid;

                  return (
                    <tr
                      key={row.demandId}
                      className={`border-b border-line ${idx % 2 === 1 ? 'bg-slate-50/60' : 'bg-white'}`}
                    >
                      <td className="border-r border-line px-2 py-2 text-center tabular-nums text-muted">
                        {idx + 1}
                      </td>
                      <td className="border-r border-line px-2 py-2 text-center font-bold tabular-nums text-ink">
                        {row.wardNo}/{row.propertyNo}
                      </td>
                      <td className="border-r border-line px-3 py-2 font-semibold text-ink">
                        {row.primaryOwnerNameMr || row.primaryOwnerNameEn || '—'}
                      </td>

                      {format !== 'B' && (
                        <>
                          <NumCell val={row.collectedByHead.houseTax} />
                          <NumCell val={row.collectedByHead.lightingTax} />
                          <NumCell val={row.collectedByHead.healthTax} />
                          <NumCell
                            val={row.collectedByHead.sanitationTax + row.collectedByHead.toiletTax}
                          />
                          <NumCell val={row.collectedByHead.otherTax} />
                        </>
                      )}
                      {format !== 'A' && (
                        <>
                          <NumCell val={row.collectedByHead.generalWaterTax} />
                          <NumCell val={row.collectedByHead.specialWaterTax} />
                        </>
                      )}
                      {format !== 'B' && <NumCell val={feesPaid} />}
                      <td className="border-r border-line bg-emerald-50/60 px-2 py-2 text-right font-extrabold tabular-nums text-emerald-900">
                        {formatInr(live.collected)}
                      </td>

                      <td className="border-r border-line bg-amber-50/50 px-2 py-2 text-right font-bold tabular-nums text-amber-900">
                        {fmtNum(live.rebate)}
                      </td>
                      <td className="border-r border-line bg-amber-50/50 px-2 py-2 text-center text-[11px] text-amber-800">
                        {row.rebateCutoffDate ? formatDateOnly(row.rebateCutoffDate, i18n.language) : '—'}
                      </td>

                      {format !== 'B' && (
                        <>
                          <NumCell val={row.balanceByHead.houseTax} />
                          <NumCell val={row.balanceByHead.lightingTax} />
                          <NumCell val={row.balanceByHead.healthTax} />
                          <NumCell
                            val={row.balanceByHead.sanitationTax + row.balanceByHead.toiletTax}
                          />
                          <NumCell val={row.balanceByHead.otherTax} />
                        </>
                      )}
                      {format !== 'A' && (
                        <>
                          <NumCell val={row.balanceByHead.generalWaterTax} />
                          <NumCell val={row.balanceByHead.specialWaterTax} />
                        </>
                      )}
                      {format !== 'B' && <NumCell val={row.balanceByHead.feesAndPenalty} />}
                      <td className="border-r border-line bg-rose-50/60 px-2 py-2 text-right font-extrabold tabular-nums text-rose-900">
                        {formatInr(live.balance)}
                      </td>

                      <td className="px-2 py-2 text-center">
                        <div className="flex flex-col items-center gap-1">
                          <StatusBadge status={row.status} />
                          <div className="flex items-center gap-1.5">
                            <Link
                              to={`/namuna-8/view/${row.propertyId}`}
                              className="inline-flex items-center gap-0.5 text-[11px] font-semibold text-brand-700 hover:underline"
                            >
                              {t('namuna9.link360')}
                              <ExternalLink className="h-3 w-3" />
                            </Link>
                            <Link
                              to={`/namuna-9ka/generate?propertyId=${row.propertyId}`}
                              className="text-[11px] font-semibold text-indigo-700 hover:underline"
                            >
                              {t('namuna9.link9Ka')}
                            </Link>
                            {live.balance > 0 && (
                              <Link
                                to={`/namuna-10/collect?propertyId=${row.propertyId}&yearId=${workspace!.selectedYear.id}`}
                                className="text-[11px] font-bold text-emerald-700 hover:underline"
                              >
                                {t('namuna10.linkCollectShort')}
                              </Link>
                            )}
                          </div>
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

      {/* Modals */}
      {showRebateModal && workspace && (
        <EarlyRebateModal
          workspace={workspace}
          selectedPropertyIds={selectedIds}
          onClose={() => setShowRebateModal(false)}
          onSuccess={async (res) => {
            setShowRebateModal(false);
            setSelectedIds([]);
            setActionSuccess(
              t('namuna9.rebateSuccessMsg', {
                applied: res.appliedCount,
                total: formatInr(res.totalRebateGranted),
                year: res.yearLabel,
              }),
            );
            await invalidateNamuna9Caches();
          }}
        />
      )}

      {showRolloverModal && workspace && (
        <FyRolloverModal
          workspace={workspace}
          onClose={() => setShowRolloverModal(false)}
          onSuccess={async (res) => {
            setShowRolloverModal(false);
            setSelectedYearId(res.targetYearId);
            setDrafts({});
            setSelectedIds([]);
            setActionSuccess(
              t('namuna9.rolloverSuccessMsg', {
                source: res.sourceYearLabel,
                target: res.targetYearLabel,
                count: res.propertiesRolledOver,
                arrears: formatInr(res.totalCarriedArrears),
              }),
            );
            await invalidateNamuna9Caches();
          }}
        />
      )}

      {showPrintModal && workspace && (
        <PrintRegisterModal
          workspace={workspace}
          initialFormat={format}
          wardNo={wardFilter}
          search={appliedSearch}
          pendingOnly={pendingOnly}
          onClose={() => setShowPrintModal(false)}
          onActionSuccess={(msg) => setActionSuccess(msg)}
        />
      )}
    </div>
  );
}

function SubHeadGroup({
  arrLabel,
  curLabel,
  totLabel,
}: {
  arrLabel: string;
  curLabel: string;
  totLabel: string;
}) {
  return (
    <>
      <th className="border border-line px-1.5 py-1 text-right">{arrLabel}</th>
      <th className="border border-line px-1.5 py-1 text-right">{curLabel}</th>
      <th className="border border-line bg-slate-200/70 px-1.5 py-1 text-right">{totLabel}</th>
    </>
  );
}

function HeadCells({
  arrears,
  current,
  total,
}: {
  arrears: number;
  current: number;
  total: number;
}) {
  return (
    <>
      <td className="border-r border-line px-1.5 py-2 text-right tabular-nums text-slate-600">
        {fmtNum(arrears)}
      </td>
      <td className="border-r border-line px-1.5 py-2 text-right tabular-nums text-slate-700">
        {fmtNum(current)}
      </td>
      <td className="border-r border-line bg-slate-100/80 px-1.5 py-2 text-right font-bold tabular-nums text-ink">
        {fmtNum(total)}
      </td>
    </>
  );
}

function NumCell({ val }: { val: number }) {
  return (
    <td className="border-r border-line px-2 py-2 text-right tabular-nums text-slate-700">
      {fmtNum(val)}
    </td>
  );
}

function StatusBadge({ status }: { status: string }) {
  const { t } = useTranslation();
  const normalized = status.toLowerCase();
  const tone =
    normalized === 'settled'
      ? 'bg-emerald-100 text-emerald-800 border-emerald-200'
      : normalized === 'partiallypaid'
        ? 'bg-amber-100 text-amber-800 border-amber-200'
        : normalized === 'published'
          ? 'bg-sky-100 text-sky-800 border-sky-200'
          : 'bg-slate-100 text-slate-700 border-slate-200';

  const label =
    normalized === 'settled'
      ? t('namuna9.statusSettled')
      : normalized === 'partiallypaid'
        ? t('namuna9.statusPartial')
        : normalized === 'published'
          ? t('namuna9.statusPublished')
          : t('namuna9.statusDraft');

  return (
    <span className={`inline-block rounded-full border px-2 py-0.5 text-[10px] font-bold ${tone}`}>
      {label}
    </span>
  );
}
