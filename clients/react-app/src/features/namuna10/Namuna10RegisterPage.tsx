import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  CheckCircle2,
  ExternalLink,
  FileSpreadsheet,
  Filter,
  Info,
  Lock,
  Printer,
  ReceiptText,
  RefreshCw,
  Search,
  ShieldAlert,
  Wallet,
  X,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import {
  namuna10Api,
  PaymentMode,
  type Namuna10ReceiptsPageDto,
  type Namuna10RegisterRowDto,
  type Namuna10RegisterWorkspaceDto,
  type PaymentModeCode,
  type TaxReceiptDto,
} from '../../services/namuna10Api';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { TaxCollectionModal } from './TaxCollectionModal';

type WorkspaceTab = 'register' | 'receipts';
type DuesStatusFilter = '' | 'due' | 'unpaid' | 'partial' | 'paid';

export function Namuna10RegisterPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const initialYearParam = searchParams.get('yearId') ?? undefined;
  const deepLinkPropertyId = searchParams.get('propertyId');

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      user &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Auditor', 'Surveyor'].includes(
        user.role,
      ),
  );
  const canCollect = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator'].includes(user.role),
  );

  const [activeTab, setActiveTab] = useState<WorkspaceTab>('register');
  const [selectedYearId, setSelectedYearId] = useState<string | undefined>(initialYearParam);

  // Tab 1 filters
  const [wardFilter, setWardFilter] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<DuesStatusFilter>('');
  const [searchInput, setSearchInput] = useState<string>('');
  const [appliedSearch, setAppliedSearch] = useState<string>('');

  // Tab 2 receipt filters
  const [recFromDate, setRecFromDate] = useState<string>('');
  const [recToDate, setRecToDate] = useState<string>('');
  const [recModeFilter, setRecModeFilter] = useState<string>('');
  const [recSearchInput, setRecSearchInput] = useState<string>('');
  const [appliedRecSearch, setAppliedRecSearch] = useState<string>('');
  const [includeCancelled, setIncludeCancelled] = useState<boolean>(true);

  // Modal & feedback state
  const [modalPropertyId, setModalPropertyId] = useState<string | null>(null);
  const [deepLinkDismissed, setDeepLinkDismissed] = useState<boolean>(false);
  const [downloadingReceiptId, setDownloadingReceiptId] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [infoMsg, setInfoMsg] = useState<string | null>(null);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  // Cached unfiltered index so ward chips & deep-link modal lookup remain intact while filtering
  const wardsIndexQuery = useQuery<Namuna10RegisterWorkspaceDto>({
    queryKey: ['namuna10-wards-index', user?.activeTenantId, selectedYearId],
    queryFn: () => namuna10Api.getRegister({ yearId: selectedYearId }),
    enabled: canRead,
    staleTime: 60_000,
  });

  const registerQuery = useQuery<Namuna10RegisterWorkspaceDto>({
    queryKey: [
      'namuna10-register',
      user?.activeTenantId,
      selectedYearId,
      wardFilter,
      appliedSearch,
      statusFilter,
    ],
    queryFn: () =>
      namuna10Api.getRegister({
        yearId: selectedYearId,
        wardNo: wardFilter || undefined,
        search: appliedSearch || undefined,
        status: statusFilter || undefined,
      }),
    enabled: canRead,
  });

  const receiptsQuery = useQuery<Namuna10ReceiptsPageDto>({
    queryKey: [
      'namuna10-receipts',
      user?.activeTenantId,
      selectedYearId,
      wardFilter,
      recFromDate,
      recToDate,
      recModeFilter,
      appliedRecSearch,
      includeCancelled,
    ],
    queryFn: () =>
      namuna10Api.listReceipts({
        yearId: selectedYearId,
        wardNo: wardFilter || undefined,
        fromDate: recFromDate || undefined,
        toDate: recToDate || undefined,
        mode: recModeFilter ? (Number(recModeFilter) as PaymentModeCode) : undefined,
        search: appliedRecSearch || undefined,
        includeCancelled,
      }),
    enabled: canRead && activeTab === 'receipts',
  });

  const workspace = registerQuery.data;
  const rows = useMemo(() => workspace?.items ?? [], [workspace?.items]);
  const allYearRows = useMemo(
    () => wardsIndexQuery.data?.items ?? rows,
    [wardsIndexQuery.data?.items, rows],
  );
  const isLocked = Boolean(workspace?.selectedYear.isLocked);

  const wardOptions = useMemo(() => {
    const set = new Set<string>();
    for (const r of allYearRows) {
      if (r.wardNo) set.add(r.wardNo);
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  }, [allYearRows]);

  // Open collection modal either on explicit click OR when deep-linked ?propertyId= has dues
  const targetModalPropertyId = modalPropertyId ?? (!deepLinkDismissed ? deepLinkPropertyId : null);
  const modalRow: Namuna10RegisterRowDto | null = useMemo(() => {
    if (!targetModalPropertyId) return null;
    return (
      rows.find((r) => r.propertyId === targetModalPropertyId) ??
      allYearRows.find((r) => r.propertyId === targetModalPropertyId) ??
      null
    );
  }, [targetModalPropertyId, rows, allYearRows]);

  const closeModal = () => {
    setModalPropertyId(null);
    setDeepLinkDismissed(true);
    if (searchParams.has('propertyId')) {
      const next = new URLSearchParams(searchParams);
      next.delete('propertyId');
      setSearchParams(next, { replace: true });
    }
  };

  const switchYear = (yearId: string) => {
    setSelectedYearId(yearId);
    setModalPropertyId(null);
    setSuccessMsg(null);
    setInfoMsg(null);
    setErrSummary(null);
    setErrItems([]);
    const next = new URLSearchParams(searchParams);
    next.set('yearId', yearId);
    setSearchParams(next, { replace: true });
  };

  const invalidateAllLedgers = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['namuna10-register'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna10-wards-index'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna10-receipts'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna10-property-receipts'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna9-register'] }),
      queryClient.invalidateQueries({ queryKey: ['namuna9ka-bills'] }),
      queryClient.invalidateQueries({ queryKey: ['property-detail'] }),
      queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] }),
      queryClient.invalidateQueries({ queryKey: ['partition-eligibility'] }),
    ]);
  };

  const handleCollectionCompleted = async (receipt: TaxReceiptDto, pdfNotice?: string | null) => {
    setSuccessMsg(
      t('namuna10.collectionSuccessMsg', {
        receiptNo: receipt.receiptNo,
        amount: formatInr(receipt.totalPaid),
        prop: `${receipt.wardNo}/${receipt.propertyNo}`,
      }),
    );
    setInfoMsg(pdfNotice ?? null);
    await invalidateAllLedgers();
    closeModal();
  };

  const handleDownloadReceiptPdf = async (receiptId: string, receiptNo: string) => {
    setDownloadingReceiptId(receiptId);
    setErrSummary(null);
    setErrItems([]);
    setInfoMsg(null);
    try {
      await namuna10Api.downloadDualReceiptPdf(receiptId, receiptNo);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna10.pdfNotAvailableYet'));
      setInfoMsg(parsed.summary || t('namuna10.pdfNotAvailableYet'));
    } finally {
      setDownloadingReceiptId(null);
    }
  };

  const modeLabel = (mode: PaymentModeCode): string => {
    switch (mode) {
      case PaymentMode.Upi:
        return t('namuna10.modeUpi');
      case PaymentMode.Cheque:
        return t('namuna10.modeCheque');
      case PaymentMode.NeftRtgs:
        return t('namuna10.modeNeft');
      case PaymentMode.OnlineGateway:
        return t('namuna10.modeOnline');
      default:
        return t('namuna10.modeCash');
    }
  };

  const renderStatusPill = (row: Namuna10RegisterRowDto) => {
    if (row.remainingBalance <= 0) {
      return (
        <span className="inline-flex rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-[11px] font-bold text-emerald-800">
          {t('namuna10.statusPaid')}
        </span>
      );
    }
    if (row.paidAmount > 0) {
      return (
        <span className="inline-flex rounded-full border border-amber-200 bg-amber-50 px-2.5 py-0.5 text-[11px] font-bold text-amber-800">
          {t('namuna10.statusPartial')}
        </span>
      );
    }
    return (
      <span className="inline-flex rounded-full border border-rose-200 bg-rose-50 px-2.5 py-0.5 text-[11px] font-bold text-rose-800">
        {t('namuna10.statusPending')}
      </span>
    );
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

  const receiptItems = receiptsQuery.data?.items ?? [];

  return (
    <div className="space-y-4">
      {/* Top Header */}
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
              <Link to="/namuna-9ka/generate" className="font-semibold text-slate-600 hover:underline">
                {t('nav.demandBills')}
              </Link>
              <span>/</span>
              <span className="font-semibold text-ink">{t('nav.receipts')}</span>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-xl font-extrabold tracking-tight text-ink sm:text-2xl">
                {t('namuna10.pageTitle')}
              </h1>
              {workspace && (
                <span className="rounded-full border border-indigo-200 bg-indigo-50 px-2.5 py-0.5 text-[11px] font-bold text-indigo-900">
                  {t('namuna10.fifoBadge')}
                </span>
              )}
            </div>

            <p className="text-xs text-muted sm:text-sm">{t('namuna10.pageSubtitle')}</p>
          </div>

          {/* Year Dropdown & Tab Switcher */}
          <div className="flex flex-wrap items-center gap-2.5">
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

            <div className="inline-flex overflow-hidden rounded-xl border border-line bg-slate-50 p-1 text-xs font-bold">
              <button
                type="button"
                onClick={() => setActiveTab('register')}
                className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 transition ${
                  activeTab === 'register'
                    ? 'bg-indigo-700 text-white shadow-2xs'
                    : 'text-slate-600 hover:text-ink'
                }`}
              >
                <Wallet className="h-3.5 w-3.5" />
                {t('namuna10.tabDuesRegister')}
              </button>
              <button
                type="button"
                onClick={() => setActiveTab('receipts')}
                className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 transition ${
                  activeTab === 'receipts'
                    ? 'bg-indigo-700 text-white shadow-2xs'
                    : 'text-slate-600 hover:text-ink'
                }`}
              >
                <ReceiptText className="h-3.5 w-3.5" />
                {t('namuna10.tabReceiptsLedger')}
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* Locked FY Banner */}
      {isLocked && workspace && (
        <div className="flex items-start gap-3 rounded-2xl border border-slate-300 bg-slate-100 px-4 py-3 text-xs text-slate-800">
          <Lock className="mt-0.5 h-4 w-4 shrink-0 text-slate-700" />
          <div>
            <p className="font-bold">
              {t('namuna9Ka.lockedBannerTitle', { year: workspace.selectedYear.yearLabel })}
            </p>
            <p className="mt-0.5 text-slate-600">{t('namuna10.lockedBannerDesc')}</p>
          </div>
        </div>
      )}

      {/* Alerts */}
      <ValidationSummary summary={errSummary} items={errItems} />

      {successMsg && (
        <div className="flex items-center justify-between gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-xs font-bold text-emerald-800">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4 shrink-0" />
            <span>{successMsg}</span>
          </div>
          <button
            type="button"
            onClick={() => setSuccessMsg(null)}
            className="text-emerald-700 hover:underline"
          >
            {t('namuna9Ka.dismissBtn')}
          </button>
        </div>
      )}

      {infoMsg && (
        <div className="flex items-center justify-between gap-2 rounded-xl border border-brand-200 bg-brand-50 px-4 py-2.5 text-xs font-semibold text-brand-900">
          <div className="flex items-center gap-2">
            <Info className="h-4 w-4 shrink-0 text-brand-700" />
            <span>{infoMsg}</span>
          </div>
          <button
            type="button"
            onClick={() => setInfoMsg(null)}
            className="font-bold text-brand-800 hover:underline"
          >
            {t('namuna9Ka.dismissBtn')}
          </button>
        </div>
      )}

      {/* 6 KPI Summary Cards (matching template 14) */}
      {workspace && (
        <section className="grid grid-cols-2 gap-3 lg:grid-cols-3 xl:grid-cols-6">
          <div className="rounded-2xl border border-line border-l-4 border-l-slate-700 bg-card p-3.5 shadow-2xs">
            <p className="text-[11px] font-semibold text-muted">{t('namuna10.kpiProperties')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-ink">
              {workspace.kpis.totalProperties}
            </p>
          </div>
          <div className="rounded-2xl border border-line border-l-4 border-l-indigo-600 bg-card p-3.5 shadow-2xs">
            <p className="text-[11px] font-semibold text-muted">{t('namuna10.kpiDemand')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-ink">
              {formatInr(workspace.kpis.totalDemand)}
            </p>
          </div>
          <div className="rounded-2xl border border-line border-l-4 border-l-emerald-600 bg-card p-3.5 shadow-2xs">
            <p className="text-[11px] font-semibold text-muted">{t('namuna10.kpiCollected')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-emerald-700">
              {formatInr(workspace.kpis.totalCollected)}
            </p>
          </div>
          <div className="rounded-2xl border border-line border-l-4 border-l-rose-600 bg-card p-3.5 shadow-2xs">
            <p className="text-[11px] font-semibold text-muted">{t('namuna10.kpiBalance')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-rose-700">
              {formatInr(workspace.kpis.totalBalance)}
            </p>
          </div>
          <div className="rounded-2xl border border-line border-l-4 border-l-amber-500 bg-card p-3.5 shadow-2xs">
            <p className="text-[11px] font-semibold text-muted">{t('namuna10.kpiPartPaid')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-amber-700">
              {workspace.kpis.partiallyPaidCount}
            </p>
          </div>
          <div className="rounded-2xl border border-line border-l-4 border-l-teal-600 bg-card p-3.5 shadow-2xs">
            <p className="text-[11px] font-semibold text-muted">{t('namuna10.kpiFullPaid')}</p>
            <p className="mt-1 text-xl font-extrabold tabular-nums text-teal-700">
              {workspace.kpis.fullyPaidCount}
            </p>
          </div>
        </section>
      )}

      {/* TAB 1: PROPERTY DUES & COLLECTION REGISTER */}
      {activeTab === 'register' && (
        <>
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
                    placeholder={t('namuna10.searchPlaceholder')}
                    className="w-64 rounded-xl border border-line bg-white py-1.5 pl-9 pr-3 text-xs text-ink focus:border-indigo-600 focus:outline-none sm:w-72"
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
              </div>

              <div className="flex flex-wrap items-center gap-1.5">
                {[
                  { key: '' as DuesStatusFilter, label: t('namuna10.statusFilterAll') },
                  { key: 'due' as DuesStatusFilter, label: t('namuna10.statusFilterDue') },
                  { key: 'partial' as DuesStatusFilter, label: t('namuna10.statusPartial') },
                  { key: 'paid' as DuesStatusFilter, label: t('namuna10.statusPaid') },
                ].map((st) => (
                  <button
                    key={st.key || 'all'}
                    type="button"
                    onClick={() => setStatusFilter(st.key)}
                    className={`rounded-xl border px-3 py-1.5 text-xs font-bold transition ${
                      statusFilter === st.key
                        ? 'border-indigo-700 bg-indigo-700 text-white'
                        : 'border-line bg-white text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    {st.label}
                  </button>
                ))}

                <button
                  type="button"
                  onClick={() => registerQuery.refetch()}
                  className="inline-flex items-center rounded-xl border border-line bg-white p-1.5 text-slate-700 hover:bg-slate-50"
                  title={t('namuna9Ka.refreshTooltip')}
                >
                  <RefreshCw className={`h-3.5 w-3.5 ${registerQuery.isFetching ? 'animate-spin' : ''}`} />
                </button>
              </div>
            </div>

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
                      ? 'border-indigo-700 bg-indigo-700 text-white'
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
                        ? 'border-indigo-700 bg-indigo-700 text-white'
                        : 'border-line bg-white text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    {t('namuna9.wardOption', { ward: w })}
                  </button>
                ))}
              </div>
            )}
          </section>

          <section className="overflow-hidden rounded-2xl border border-line bg-card shadow-2xs">
            {registerQuery.isLoading ? (
              <div className="py-16 text-center text-xs font-semibold text-muted">
                {t('namuna10.loadingRegister')}
              </div>
            ) : rows.length === 0 ? (
              <div className="px-6 py-16 text-center">
                <Wallet className="mx-auto h-10 w-10 text-slate-300" />
                <p className="mt-2 text-sm font-bold text-ink">{t('namuna10.emptyTitle')}</p>
                <p className="mt-1 text-xs text-muted">{t('namuna10.emptyDesc')}</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[980px] border-collapse text-left text-xs">
                  <thead>
                    <tr className="border-b border-line bg-slate-50 text-[11px] font-bold uppercase tracking-wide text-slate-600">
                      <th className="px-3 py-3">{t('namuna9.colWardProp')}</th>
                      <th className="px-3 py-3">{t('namuna9Ka.colOwnerMobile')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna10.colArrearsAndFees')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna9Ka.colCurrent')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna9.colNetDemand')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna10.colCollected')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna10.colBalance')}</th>
                      <th className="px-3 py-3 text-center">{t('namuna9Ka.colStatus')}</th>
                      <th className="px-3 py-3">{t('namuna10.colLastReceipt')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna9Ka.colActions')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {rows.map((row) => {
                      const owner = row.primaryOwnerNameMr || row.primaryOwnerNameEn || '—';
                      const arrPlusFees =
                        row.arrearsDue.total +
                        row.noticeFeeDue +
                        row.warrantFeeDue +
                        row.penaltyDue +
                        row.carriedFeesDue;

                      return (
                        <tr key={row.demandId} className="bg-white transition hover:bg-indigo-50/25">
                          <td className="px-3 py-3 align-top">
                            <span className="font-mono font-extrabold text-ink">
                              {row.wardNo} / {row.propertyNo}
                            </span>
                            {row.activeBillNo && (
                              <p className="mt-0.5 font-mono text-[10px] font-semibold text-indigo-700">
                                {row.activeBillNo}
                              </p>
                            )}
                          </td>

                          <td className="px-3 py-3 align-top">
                            <p className="font-bold text-ink">{owner}</p>
                            {(row.primaryOwnerMobileMasked || row.primaryOwnerAadhaarMasked) && (
                              <p className="mt-0.5 font-mono text-[10px] text-muted">
                                {row.primaryOwnerMobileMasked || row.primaryOwnerAadhaarMasked}
                              </p>
                            )}
                          </td>

                          <td className="px-3 py-3 text-right tabular-nums text-slate-700">
                            {formatInr(arrPlusFees)}
                          </td>
                          <td className="px-3 py-3 text-right tabular-nums text-slate-700">
                            {formatInr(row.currentDue.total)}
                          </td>
                          <td className="px-3 py-3 text-right tabular-nums font-bold text-ink">
                            {formatInr(row.netDemand)}
                          </td>
                          <td className="px-3 py-3 text-right tabular-nums font-bold text-emerald-700">
                            {formatInr(row.paidAmount)}
                          </td>
                          <td className="px-3 py-3 text-right tabular-nums font-extrabold text-rose-700">
                            {formatInr(row.remainingBalance)}
                          </td>

                          <td className="px-3 py-3 text-center align-top">{renderStatusPill(row)}</td>

                          <td className="px-3 py-3 align-top">
                            {row.lastReceipt ? (
                              <div>
                                <span
                                  className={`font-mono text-[11px] font-bold ${
                                    row.lastReceipt.isCancelled
                                      ? 'line-through text-rose-700'
                                      : 'text-indigo-800'
                                  }`}
                                >
                                  {row.lastReceipt.receiptNo}
                                </span>
                                <p className="text-[10px] text-muted">
                                  {formatDateOnly(row.lastReceipt.receiptDate, i18n.language)} ·{' '}
                                  {formatInr(row.lastReceipt.totalPaid)}
                                </p>
                              </div>
                            ) : (
                              <span className="text-[11px] text-muted">—</span>
                            )}
                          </td>

                          <td className="px-3 py-3 text-right align-top">
                            <div className="inline-flex items-center justify-end gap-1.5">
                              <button
                                type="button"
                                onClick={() => setModalPropertyId(row.propertyId)}
                                className={`inline-flex items-center gap-1 rounded-xl px-3 py-1.5 text-[11px] font-extrabold shadow-2xs transition ${
                                  row.remainingBalance > 0 && canCollect && !isLocked
                                    ? 'bg-indigo-700 text-white hover:bg-indigo-800'
                                    : 'border border-line bg-white text-slate-700 hover:bg-slate-50'
                                }`}
                              >
                                <Wallet className="h-3.5 w-3.5" />
                                {row.remainingBalance > 0 && canCollect && !isLocked
                                  ? t('namuna10.collectBtn')
                                  : t('namuna10.historyBtn')}
                              </button>

                              <Link
                                to={`/namuna-9ka/generate?propertyId=${row.propertyId}`}
                                title={t('namuna9.link9Ka')}
                                className="inline-flex items-center rounded-xl border border-line bg-white p-1.5 text-indigo-700 hover:bg-indigo-50"
                              >
                                <FileSpreadsheet className="h-3.5 w-3.5" />
                              </Link>

                              <Link
                                to={`/namuna-8/view/${row.propertyId}`}
                                title={t('namuna9Ka.viewProperty360')}
                                className="inline-flex items-center rounded-xl border border-line bg-white p-1.5 text-slate-600 hover:bg-slate-50"
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
        </>
      )}

      {/* TAB 2: SEARCHABLE RECEIPTS REGISTER */}
      {activeTab === 'receipts' && (
        <>
          <section className="space-y-3 rounded-2xl border border-line bg-card p-4 shadow-2xs">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex flex-wrap items-center gap-2">
                <div className="relative">
                  <Search className="pointer-events-none absolute left-3 top-2.5 h-3.5 w-3.5 text-muted" />
                  <input
                    type="text"
                    value={recSearchInput}
                    onChange={(e) => setRecSearchInput(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') setAppliedRecSearch(recSearchInput.trim());
                    }}
                    placeholder={t('namuna10.searchReceiptsPlaceholder')}
                    className="w-60 rounded-xl border border-line bg-white py-1.5 pl-9 pr-3 text-xs text-ink focus:border-indigo-600 focus:outline-none sm:w-64"
                  />
                </div>
                <button
                  type="button"
                  onClick={() => setAppliedRecSearch(recSearchInput.trim())}
                  className="inline-flex items-center gap-1 rounded-xl border border-line bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100"
                >
                  <Filter className="h-3.5 w-3.5" />
                  {t('namuna9.filterBtn')}
                </button>

                <input
                  type="date"
                  aria-label={t('namuna10.fromDateLabel')}
                  value={recFromDate}
                  onChange={(e) => setRecFromDate(e.target.value)}
                  className="rounded-xl border border-line bg-white px-2.5 py-1.5 text-xs font-semibold text-ink"
                />
                <input
                  type="date"
                  aria-label={t('namuna10.toDateLabel')}
                  value={recToDate}
                  onChange={(e) => setRecToDate(e.target.value)}
                  className="rounded-xl border border-line bg-white px-2.5 py-1.5 text-xs font-semibold text-ink"
                />

                <select
                  aria-label={t('namuna10.paymentModeLabel')}
                  value={recModeFilter}
                  onChange={(e) => setRecModeFilter(e.target.value)}
                  className="rounded-xl border border-line bg-white px-2.5 py-1.5 text-xs font-bold text-ink"
                >
                  <option value="">{t('namuna10.allModesOption')}</option>
                  <option value={String(PaymentMode.Cash)}>{t('namuna10.modeCash')}</option>
                  <option value={String(PaymentMode.Upi)}>{t('namuna10.modeUpi')}</option>
                  <option value={String(PaymentMode.Cheque)}>{t('namuna10.modeCheque')}</option>
                  <option value={String(PaymentMode.NeftRtgs)}>{t('namuna10.modeNeft')}</option>
                </select>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-xl border border-line bg-white px-3 py-1.5 text-xs font-semibold text-slate-700">
                  <input
                    type="checkbox"
                    checked={includeCancelled}
                    onChange={(e) => setIncludeCancelled(e.target.checked)}
                    className="h-3.5 w-3.5 rounded accent-indigo-700"
                  />
                  {t('namuna10.showCancelledLabel')}
                </label>

                {(recFromDate || recToDate || recModeFilter || appliedRecSearch) && (
                  <button
                    type="button"
                    onClick={() => {
                      setRecFromDate('');
                      setRecToDate('');
                      setRecModeFilter('');
                      setRecSearchInput('');
                      setAppliedRecSearch('');
                    }}
                    className="inline-flex items-center gap-1 rounded-xl border border-line bg-slate-50 px-2.5 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-100"
                  >
                    <X className="h-3.5 w-3.5" />
                    {t('namuna10.clearFiltersBtn')}
                  </button>
                )}
              </div>
            </div>

            {receiptsQuery.data && (
              <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line pt-2.5 text-xs">
                <div className="flex flex-wrap items-center gap-4">
                  <span>
                    {t('namuna10.receiptsCountLabel')}:{' '}
                    <strong>{receiptsQuery.data.totalCount}</strong>
                  </span>
                  <span>
                    {t('namuna10.activeCollectedTotalLabel')}:{' '}
                    <strong className="text-emerald-700">
                      {formatInr(receiptsQuery.data.totalCollectedAmount)}
                    </strong>
                  </span>
                  <span>
                    {t('namuna10.cancelledCountLabel')}:{' '}
                    <strong className="text-rose-700">{receiptsQuery.data.cancelledCount}</strong>
                  </span>
                </div>
              </div>
            )}
          </section>

          <section className="overflow-hidden rounded-2xl border border-line bg-card shadow-2xs">
            {receiptsQuery.isLoading ? (
              <div className="py-16 text-center text-xs font-semibold text-muted">
                {t('namuna10.loadingReceipts')}
              </div>
            ) : receiptItems.length === 0 ? (
              <div className="px-6 py-16 text-center">
                <ReceiptText className="mx-auto h-10 w-10 text-slate-300" />
                <p className="mt-2 text-sm font-bold text-ink">{t('namuna10.noReceiptsFoundTitle')}</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[980px] border-collapse text-left text-xs">
                  <thead>
                    <tr className="border-b border-line bg-slate-50 text-[11px] font-bold uppercase tracking-wide text-slate-600">
                      <th className="px-3 py-3">{t('namuna10.colReceiptBook')}</th>
                      <th className="px-3 py-3">{t('namuna10.receiptDateLabel')}</th>
                      <th className="px-3 py-3">{t('namuna9.colWardProp')}</th>
                      <th className="px-3 py-3">{t('namuna10.payerNameLabel')}</th>
                      <th className="px-3 py-3">{t('namuna10.paymentModeLabel')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna10.colAllocatedSummary')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna10.amountPaidLabel')}</th>
                      <th className="px-3 py-3 text-center">{t('namuna9Ka.colStatus')}</th>
                      <th className="px-3 py-3 text-right">{t('namuna9Ka.colActions')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {receiptItems.map((rec) => {
                      const fees =
                        (rec.headAllocation?.noticeFeePaid ?? 0) +
                        (rec.headAllocation?.warrantFeePaid ?? 0) +
                        (rec.headAllocation?.penaltyPaid ?? 0);
                      const arr = rec.headAllocation?.arrearsAllocated?.total ?? 0;
                      const cur = rec.headAllocation?.currentAllocated?.total ?? 0;

                      return (
                        <tr
                          key={rec.receiptId}
                          className={rec.isCancelled ? 'bg-rose-50/40' : 'bg-white hover:bg-slate-50'}
                        >
                          <td className="px-3 py-3 align-top">
                            <span
                              className={`font-mono font-extrabold ${
                                rec.isCancelled ? 'line-through text-rose-700' : 'text-indigo-800'
                              }`}
                            >
                              {rec.receiptNo}
                            </span>
                            <p className="mt-0.5 font-mono text-[10px] text-muted">{rec.bookNo}</p>
                          </td>
                          <td className="px-3 py-3 align-top tabular-nums text-slate-700">
                            {formatDateOnly(rec.receiptDate, i18n.language)}
                          </td>
                          <td className="px-3 py-3 align-top font-mono font-bold text-ink">
                            {rec.wardNo} / {rec.propertyNo}
                          </td>
                          <td className="px-3 py-3 align-top">
                            <p className="font-bold text-ink">{rec.payerName}</p>
                            {rec.primaryOwnerAadhaarMasked && (
                              <p className="mt-0.5 font-mono text-[10px] text-muted">
                                {rec.primaryOwnerAadhaarMasked}
                              </p>
                            )}
                          </td>
                          <td className="px-3 py-3 align-top">
                            <span className="font-bold text-slate-800">{modeLabel(rec.paymentMode)}</span>
                            {rec.referenceNo && (
                              <p className="mt-0.5 font-mono text-[10px] text-muted">
                                {rec.referenceNo}
                              </p>
                            )}
                          </td>
                          <td className="px-3 py-3 text-right align-top text-[11px] text-slate-600 tabular-nums">
                            {fees > 0 && <div>F: {formatInr(fees)}</div>}
                            {arr > 0 && <div>A: {formatInr(arr)}</div>}
                            {cur > 0 && <div>C: {formatInr(cur)}</div>}
                          </td>
                          <td className="px-3 py-3 text-right align-top">
                            <span
                              className={`mono font-extrabold ${
                                rec.isCancelled ? 'line-through text-rose-700' : 'text-emerald-800'
                              }`}
                            >
                              {formatInr(rec.totalPaid)}
                            </span>
                            {rec.discountGiven > 0 && (
                              <p className="text-[10px] text-emerald-700">
                                Rebate {formatInr(rec.discountGiven)}
                              </p>
                            )}
                          </td>
                          <td className="px-3 py-3 text-center align-top">
                            {rec.isCancelled ? (
                              <span className="rounded-full bg-rose-700 px-2.5 py-0.5 text-[10px] font-bold text-white">
                                {t('namuna10.cancelledBadge')}
                              </span>
                            ) : (
                              <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-[10px] font-bold text-emerald-800">
                                {t('namuna10.activeBadge')}
                              </span>
                            )}
                          </td>
                          <td className="px-3 py-3 text-right align-top">
                            <button
                              type="button"
                              disabled={downloadingReceiptId === rec.receiptId}
                              onClick={() => handleDownloadReceiptPdf(rec.receiptId, rec.receiptNo)}
                              className="inline-flex items-center gap-1 rounded-xl border border-line bg-white px-2.5 py-1.5 text-[11px] font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                            >
                              <Printer className="h-3.5 w-3.5" />
                              {t('namuna10.printCopyBtn')}
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}

      {/* POS Collection Modal */}
      {workspace && modalRow && (
        <TaxCollectionModal
          key={`${workspace.selectedYear.id}:${modalRow.propertyId}:${modalRow.paidAmount}`}
          workspace={workspace}
          row={modalRow}
          canCollect={canCollect}
          onClose={closeModal}
          onCollected={handleCollectionCompleted}
        />
      )}
    </div>
  );
}

