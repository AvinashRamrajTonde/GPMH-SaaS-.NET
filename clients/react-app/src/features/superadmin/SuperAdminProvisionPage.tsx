import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Building2,
  CreditCard,
  Crown,
  Eye,
  History,
  KeyRound,
  PauseCircle,
  PlayCircle,
  Plus,
  RefreshCw,
  Search,
  Settings,
  Sparkles,
  Trash2,
  UserCheck,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { TenantStatusBadge } from '../../components/StatusBadge';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { sprint2Api, type SuperAdminTenantSummary } from '../../services/sprint2Api';
import {
  superAdminApi,
  type SupportImpersonationResult,
  type TenantStatusAction,
} from '../../services/superAdminApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation } from '../../utils/validation';
import { PaymentApprovalsTab } from './PaymentApprovalsTab';
import { AgentKycTab, ProvisionGpTab } from './ProvisionAndAgentsTabs';
import {
  ImpersonateModal,
  ResetPasswordModal,
  StatusActionModal,
} from './SuperAdminActionModals';
import { TenantDetailDrawer } from './TenantDetailDrawer';
import { AuditLogsTab, PlatformSettingsTab, TrashTab } from './TrashAuditSettingsTabs';

type HQTab =
  | 'directory'
  | 'approvals'
  | 'trash'
  | 'provision'
  | 'agents'
  | 'audit'
  | 'settings';

export function SuperAdminProvisionPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken, startSupportImpersonation } = useAuth();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState<HQTab>('directory');
  const [tenantSearch, setTenantSearch] = useState('');
  const [districtFilter, setDistrictFilter] = useState('ALL');
  const [talukaFilter, setTalukaFilter] = useState('ALL');
  const [statusFilter, setStatusFilter] = useState('ALL');

  const [drawerTenantId, setDrawerTenantId] = useState<string | null>(null);
  const [impersonateTarget, setImpersonateTarget] = useState<SuperAdminTenantSummary | null>(null);
  const [statusModal, setStatusModal] = useState<{
    tenant: SuperAdminTenantSummary;
    action: TenantStatusAction;
  } | null>(null);
  const [resetPassTarget, setResetPassTarget] = useState<SuperAdminTenantSummary | null>(null);
  const [directoryError, setDirectoryError] = useState<string | null>(null);
  const [softDeletingId, setSoftDeletingId] = useState<string | null>(null);

  const hasCommandCenterAccess = Boolean(
    accessToken && (user?.role === 'SuperAdmin' || user?.role === 'Support_Admin'),
  );
  const canMutate = user?.role === 'SuperAdmin';

  const statsQuery = useQuery({
    queryKey: ['superadmin', 'stats'],
    queryFn: superAdminApi.getStats,
    enabled: hasCommandCenterAccess,
  });

  const tenantsQuery = useQuery({
    queryKey: ['superadmin', 'tenants', 'directory'],
    queryFn: () => superAdminApi.listTenants(),
    enabled: hasCommandCenterAccess,
  });

  const agentsQuery = useQuery({
    queryKey: ['superadmin', 'agents'],
    queryFn: sprint2Api.listSuperAdminAgents,
    enabled: hasCommandCenterAccess,
  });

  const districts = useMemo(() => {
    const set = new Set<string>();
    for (const item of tenantsQuery.data ?? []) {
      if (item.districtName) set.add(item.districtName);
    }
    return Array.from(set).sort();
  }, [tenantsQuery.data]);

  const talukas = useMemo(() => {
    const set = new Set<string>();
    for (const item of tenantsQuery.data ?? []) {
      if (districtFilter !== 'ALL' && item.districtName !== districtFilter) continue;
      if (item.tehsilName) set.add(item.tehsilName);
    }
    return Array.from(set).sort();
  }, [tenantsQuery.data, districtFilter]);

  const filteredTenants = useMemo(() => {
    const items = tenantsQuery.data ?? [];
    const q = tenantSearch.trim().toLowerCase();
    return items.filter((item) => {
      if (districtFilter !== 'ALL' && item.districtName !== districtFilter) return false;
      if (talukaFilter !== 'ALL' && item.tehsilName !== talukaFilter) return false;
      const effective = item.effectiveStatus || item.status;
      if (statusFilter !== 'ALL' && effective !== statusFilter && item.status !== statusFilter) {
        return false;
      }
      if (!q) return true;
      return (
        item.panchayatNameEn.toLowerCase().includes(q) ||
        item.panchayatNameMr.toLowerCase().includes(q) ||
        item.lgdCode.includes(q) ||
        item.districtName.toLowerCase().includes(q) ||
        item.tehsilName.toLowerCase().includes(q) ||
        (item.adminUsername ?? '').toLowerCase().includes(q)
      );
    });
  }, [tenantsQuery.data, tenantSearch, districtFilter, talukaFilter, statusFilter]);

  const stats = statsQuery.data;
  const pendingAgents = useMemo(
    () => (agentsQuery.data ?? []).filter((a) => a.kycStatus === 'PendingApproval').length,
    [agentsQuery.data],
  );

  async function refreshSuperAdminQueries() {
    await queryClient.invalidateQueries({ queryKey: ['superadmin'] });
  }

  async function handleSoftDelete(item: SuperAdminTenantSummary) {
    setDirectoryError(null);
    setSoftDeletingId(item.tenantId);
    try {
      await superAdminApi.softDeleteTenant(item.tenantId);
      await refreshSuperAdminQueries();
    } catch (err) {
      setDirectoryError(extractProblemValidation(err, 'Failed to move GP to trash.').summary);
    } finally {
      setSoftDeletingId(null);
    }
  }

  function handleImpersonationStarted(result: SupportImpersonationResult) {
    setImpersonateTarget(null);
    setDrawerTenantId(null);
    queryClient.clear();
    startSupportImpersonation(result);
    void navigate('/dashboard');
  }

  if (!hasCommandCenterAccess) {
    return (
      <div className="mx-auto max-w-lg rounded-2xl border border-amber-500/30 bg-slate-900 p-6 text-sm text-amber-100 space-y-3 shadow-xl">
        <div className="flex items-center gap-2 font-extrabold text-amber-300">
          <Crown className="h-5 w-5" />
          <h1>{t('superadmin.title')}</h1>
        </div>
        <p className="text-xs text-slate-300">{t('superadmin.requireSuperAdmin')}</p>
        <Link
          to="/login"
          className="inline-block rounded-xl bg-amber-500 px-4 py-2 text-xs font-bold text-slate-950 hover:bg-amber-400"
        >
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  const tabs: {
    id: HQTab;
    label: string;
    icon: typeof Building2;
    badgeCount?: number;
    badgeTone?: 'amber' | 'rose' | 'slate';
    hideWhenReadOnly?: boolean;
  }[] = [
    {
      id: 'directory',
      label: t('superadmin.tabDirectory'),
      icon: Building2,
      badgeCount: (tenantsQuery.data ?? []).length,
      badgeTone: 'slate',
    },
    {
      id: 'approvals',
      label: t('superadminBilling.tabApprovals'),
      icon: CreditCard,
      badgeCount: stats?.pendingPaymentSubmissions ?? 0,
      badgeTone: 'rose',
    },
    {
      id: 'trash',
      label: t('superadminBilling.tabTrash'),
      icon: Trash2,
      badgeCount: stats?.trashCount ?? 0,
      badgeTone: 'rose',
    },
    {
      id: 'provision',
      label: t('superadmin.tabProvision'),
      icon: Plus,
      hideWhenReadOnly: true,
    },
    {
      id: 'agents',
      label: t('superadminBilling.tabAgentsIncluded'),
      icon: UserCheck,
      badgeCount: pendingAgents,
      badgeTone: 'amber',
    },
    {
      id: 'audit',
      label: t('superadminBilling.tabAudit'),
      icon: History,
    },
    {
      id: 'settings',
      label: t('superadminBilling.tabSettings'),
      icon: Settings,
    },
  ];

  return (
    <div className="space-y-6">
      {!canMutate && (
        <div className="rounded-xl border border-sky-500/40 bg-sky-950/50 px-4 py-2.5 text-xs font-semibold text-sky-200 flex items-center gap-2 no-print">
          <Eye className="h-4 w-4 shrink-0 text-sky-400" />
          <span>{t('superadminBilling.readOnlyNotice')}</span>
        </div>
      )}

      {/* 4 Statewide Command Center KPI Cards */}
      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 no-print">
        {/* KPI 1: Total Registered GPs */}
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <p className="text-xs font-semibold text-slate-400">
            {t('superadminBilling.kpiTotalVillages')}
          </p>
          <p className="mt-1 mono text-2xl font-extrabold text-white">
            {stats?.totalVillages ?? (tenantsQuery.data?.length ?? 0)}
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-slate-400">
            <span className="inline-flex items-center gap-1">
              <span className="h-2 w-2 rounded-full bg-emerald-400" />
              {t('superadminBilling.kpiActivePill')}:{' '}
              <strong className="text-white">{stats?.activeVillages ?? 0}</strong>
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-2 w-2 rounded-full bg-amber-400" />
              {t('superadminBilling.kpiPendingPill')}:{' '}
              <strong className="text-white">{stats?.pendingActivation ?? 0}</strong>
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-2 w-2 rounded-full bg-rose-400" />
              {t('superadminBilling.kpiExpiredPill')}:{' '}
              <strong className="text-white">{stats?.expiredVillages ?? 0}</strong>
            </span>
          </div>
        </div>

        {/* KPI 2: Statewide Tax Collection */}
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <div className="flex items-start justify-between gap-2">
            <p className="text-xs font-semibold text-slate-400">
              {t('superadminBilling.kpiStateTaxTitle')}
            </p>
            <span className="rounded bg-indigo-500/15 px-2 py-0.5 mono text-[10px] font-bold text-indigo-300">
              {t('superadminBilling.kpiCollectedPct', {
                pct: (stats?.collectionPercentage ?? 0).toFixed(1),
              })}
            </span>
          </div>
          <p className="mt-1 mono text-2xl font-extrabold text-emerald-400">
            {formatInr(stats?.totalTaxCollectedStatewide ?? 0)}
          </p>
          <p className="mt-2 text-[11px] text-slate-400">
            {t('superadminBilling.kpiDemandedLabel')}{' '}
            <strong className="mono text-slate-200">
              {formatInr(stats?.totalTaxDemandedStatewide ?? 0)}
            </strong>
          </p>
        </div>

        {/* KPI 3: Properties & Citizens */}
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <p className="text-xs font-semibold text-slate-400">
            {t('superadminBilling.kpiCitizensPropsTitle')}
          </p>
          <p className="mt-1 mono text-2xl font-extrabold text-sky-400">
            {(stats?.totalProperties ?? 0).toLocaleString('en-IN')}
          </p>
          <div className="mt-2 flex items-center justify-between text-[11px] text-slate-400">
            <span>
              {t('superadminBilling.kpiCitizensLabel')}{' '}
              <strong className="mono text-slate-200">
                {(stats?.totalCitizens ?? 0).toLocaleString('en-IN')}
              </strong>
            </span>
            <span>
              {t('superadminBilling.kpiPropsLabel')}{' '}
              <strong className="mono text-emerald-300">{stats?.totalProperties ?? 0}</strong>
            </span>
          </div>
        </div>

        {/* KPI 4: Platform Revenue + Operational Queues */}
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <div className="flex items-start justify-between gap-2">
            <p className="text-xs font-semibold text-slate-400">
              {t('superadminBilling.kpiRevenueTitle')}
            </p>
            <span className="rounded bg-amber-500/15 px-2 py-0.5 mono text-[10px] font-bold text-amber-300">
              {t('superadminBilling.kpiExpiring30d', { count: stats?.expiringIn30Days ?? 0 })}
            </span>
          </div>
          <p className="mt-1 mono text-2xl font-extrabold text-amber-400">
            {formatInr(stats?.totalPlatformRevenue ?? 0)}
          </p>
          <div className="mt-2 flex flex-wrap items-center justify-between gap-1 text-[11px] text-slate-400">
            <button
              type="button"
              onClick={() => setActiveTab('approvals')}
              className="font-bold text-amber-300 hover:underline"
            >
              {t('superadminBilling.kpiApprovalsPending', {
                count: stats?.pendingPaymentSubmissions ?? 0,
              })}
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('trash')}
              className="mono font-bold text-rose-400 hover:underline"
            >
              {t('superadminBilling.kpiTrashCount', { count: stats?.trashCount ?? 0 })}
            </button>
          </div>
        </div>
      </section>

      {/* HQ Navigation Tabs */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-3 no-print">
        <nav className="flex flex-wrap gap-2" role="tablist">
          {tabs
            .filter((tab) => !(tab.hideWhenReadOnly && !canMutate))
            .map((tab) => {
              const Icon = tab.icon;
              const active = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  role="tab"
                  aria-selected={active}
                  onClick={() => setActiveTab(tab.id)}
                  className={`inline-flex items-center gap-2 rounded-xl border px-3.5 py-2 text-xs font-bold transition ${
                    active
                      ? 'border-amber-500/50 bg-amber-500/15 text-amber-300'
                      : 'border-slate-800 bg-slate-900 text-slate-400 hover:border-slate-700 hover:text-slate-200'
                  }`}
                >
                  <Icon className="h-4 w-4" />
                  <span>{tab.label}</span>
                  {typeof tab.badgeCount === 'number' && tab.badgeCount > 0 && (
                    <span
                      className={`rounded-full px-1.5 py-0.5 mono text-[10px] font-extrabold ${
                        tab.badgeTone === 'rose'
                          ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40'
                          : tab.badgeTone === 'amber'
                            ? 'bg-amber-500 text-slate-950'
                            : 'bg-slate-800 text-slate-300'
                      }`}
                    >
                      {tab.badgeCount}
                    </span>
                  )}
                </button>
              );
            })}
        </nav>

        <Link
          to="/superadmin/agents"
          className="inline-flex items-center gap-2 rounded-xl border border-emerald-500/40 bg-emerald-500/15 px-3.5 py-2 text-xs font-extrabold text-emerald-300 hover:bg-emerald-500/25 transition"
        >
          <UserCheck className="h-4 w-4" />
          <span>{t('nav.superadminAgents')} →</span>
        </Link>
      </div>

      {/* TAB 1: Villages Directory */}
      {activeTab === 'directory' && (
        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-sm space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-sm font-extrabold text-white">{t('superadmin.tenantsListTitle')}</h2>
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-500" />
                <input
                  type="text"
                  aria-label={t('superadminBilling.searchPlaceholderExtended')}
                  value={tenantSearch}
                  onChange={(e) => setTenantSearch(e.target.value)}
                  placeholder={t('superadminBilling.searchPlaceholderExtended')}
                  className="w-56 sm:w-64 rounded-lg border border-slate-700 bg-slate-950 pl-8 pr-3 py-1.5 text-slate-100 placeholder:text-slate-500 focus:border-amber-400"
                />
              </div>
              <select
                aria-label={t('superadmin.allDistricts')}
                value={districtFilter}
                onChange={(e) => {
                  setDistrictFilter(e.target.value);
                  setTalukaFilter('ALL');
                }}
                className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-slate-200"
              >
                <option value="ALL">{t('superadmin.allDistricts')}</option>
                {districts.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
              <select
                aria-label={t('superadminBilling.allTalukas')}
                value={talukaFilter}
                onChange={(e) => setTalukaFilter(e.target.value)}
                className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-slate-200"
              >
                <option value="ALL">{t('superadminBilling.allTalukas')}</option>
                {talukas.map((tal) => (
                  <option key={tal} value={tal}>
                    {tal}
                  </option>
                ))}
              </select>
              <select
                aria-label={t('superadmin.allStatuses')}
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-slate-200"
              >
                <option value="ALL">{t('superadmin.allStatuses')}</option>
                <option value="Active">{t('tenantStatus.Active')}</option>
                <option value="Trial">{t('tenantStatus.Trial')}</option>
                <option value="PendingApproval">{t('tenantStatus.PendingApproval')}</option>
                <option value="Expired">{t('tenantStatus.Expired')}</option>
                <option value="Suspended">{t('tenantStatus.Suspended')}</option>
              </select>
            </div>
          </div>

          {directoryError && (
            <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-xs text-rose-200">
              {directoryError}
            </div>
          )}

          {tenantsQuery.isLoading && (
            <div className="space-y-2">
              {[1, 2, 3].map((n) => (
                <div key={n} className="h-14 animate-pulse rounded-xl bg-slate-800" />
              ))}
            </div>
          )}

          {tenantsQuery.error instanceof ApiProblemError && (
            <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-xs text-rose-200">
              {tenantsQuery.error.message}
            </div>
          )}

          {!tenantsQuery.isLoading && filteredTenants.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/60 p-8 text-center text-xs text-slate-400">
              {t('superadmin.noTenantsMatch')}
            </div>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-slate-800">
              <table className="min-w-full border-collapse text-left text-xs">
                <thead className="bg-slate-950 text-[11px] font-bold uppercase text-slate-400 border-b border-slate-800">
                  <tr>
                    <th className="px-3.5 py-3">LGD</th>
                    <th className="px-3.5 py-3">{t('onboarding.panchayat')}</th>
                    <th className="px-3.5 py-3">{t('onboarding.district')}</th>
                    <th className="px-3.5 py-3">{t('superadminBilling.propertiesShort')}</th>
                    <th className="px-3.5 py-3">{t('superadminBilling.collectedShort')}</th>
                    <th className="px-3.5 py-3">Status &amp; Expiry</th>
                    <th className="px-3.5 py-3">{t('superadminBilling.actionsCol')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {filteredTenants.map((item) => {
                    const effectiveStatus = item.effectiveStatus || item.status;
                    const days = item.daysUntilExpiry;
                    return (
                      <tr key={item.tenantId} className="hover:bg-slate-800/40">
                        <td className="px-3.5 py-3 mono font-bold text-amber-300">{item.lgdCode}</td>
                        <td className="px-3.5 py-3">
                          <button
                            type="button"
                            onClick={() => setDrawerTenantId(item.tenantId)}
                            className="text-left group"
                          >
                            <p className="font-bold text-white group-hover:text-amber-300 transition">
                              {item.panchayatNameMr}
                            </p>
                            <p className="text-[11px] text-slate-400">
                              {item.panchayatNameEn}
                              {item.adminUsername ? ` · ${item.adminUsername}` : ''}
                            </p>
                          </button>
                        </td>
                        <td className="px-3.5 py-3 text-slate-300">
                          {item.districtName} · {item.tehsilName}
                        </td>
                        <td className="px-3.5 py-3 mono text-[11px] text-slate-300">
                          <span className="font-bold text-sky-300">{item.propertiesCount ?? 0}</span> /{' '}
                          <span>{item.citizensCount ?? 0}</span>
                        </td>
                        <td className="px-3.5 py-3 mono text-[11px]">
                          <p className="font-bold text-emerald-400">{formatInr(item.taxCollected ?? 0)}</p>
                          <p className="text-[10px] text-slate-500">/ {formatInr(item.taxDemanded ?? 0)}</p>
                        </td>
                        <td className="px-3.5 py-3 space-y-1">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <TenantStatusBadge status={effectiveStatus} dark />
                            {item.isTrial && (
                              <span className="rounded bg-sky-500/20 px-1.5 py-0.5 text-[9px] font-bold text-sky-300 border border-sky-500/30">
                                {t('superadminBilling.trialBadge')}
                              </span>
                            )}
                          </div>
                          <p className="mono text-[10px] text-slate-400">
                            {formatDateOnly(item.subscriptionExpiresAtUtc, i18n.language)}
                            {typeof days === 'number' && (
                              <span
                                className={`ml-1.5 font-bold ${
                                  days < 0
                                    ? 'text-rose-400'
                                    : days <= 30
                                      ? 'text-amber-300'
                                      : 'text-emerald-400'
                                }`}
                              >
                                ({days < 0
                                  ? t('superadminBilling.expiredTag')
                                  : t('superadminBilling.daysLeft', { days })})
                              </span>
                            )}
                          </p>
                        </td>
                        <td className="px-3.5 py-3">
                          <div className="flex flex-wrap items-center gap-1">
                            <button
                              type="button"
                              title={t('superadminBilling.btnView360')}
                              onClick={() => setDrawerTenantId(item.tenantId)}
                              className="rounded-lg border border-slate-700 bg-slate-800 px-2 py-1 text-[11px] font-semibold text-slate-200 hover:bg-slate-700"
                            >
                              360°
                            </button>
                            <button
                              type="button"
                              title={t('superadminBilling.btnImpersonate')}
                              onClick={() => setImpersonateTarget(item)}
                              className="inline-flex items-center gap-1 rounded-lg bg-amber-500/20 border border-amber-500/40 px-2 py-1 text-[11px] font-bold text-amber-300 hover:bg-amber-500/30"
                            >
                              <Eye className="h-3 w-3" />
                              15m
                            </button>
                            {canMutate && (
                              <>
                                <button
                                  type="button"
                                  title={t('superadminBilling.btnDirectRenew')}
                                  onClick={() => setStatusModal({ tenant: item, action: 'DirectRenew' })}
                                  className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-1.5 text-emerald-300 hover:bg-emerald-500/25"
                                >
                                  <RefreshCw className="h-3.5 w-3.5" />
                                </button>
                                <button
                                  type="button"
                                  title={t('superadminBilling.btnGrantTrial')}
                                  onClick={() => setStatusModal({ tenant: item, action: 'GrantTrial' })}
                                  className="rounded-lg border border-sky-500/30 bg-sky-500/10 p-1.5 text-sky-300 hover:bg-sky-500/25"
                                >
                                  <Sparkles className="h-3.5 w-3.5" />
                                </button>
                                <button
                                  type="button"
                                  title={
                                    effectiveStatus === 'Suspended'
                                      ? t('superadminBilling.btnActivate')
                                      : t('superadminBilling.btnSuspend')
                                  }
                                  onClick={() =>
                                    setStatusModal({
                                      tenant: item,
                                      action: effectiveStatus === 'Suspended' ? 'Activate' : 'Suspend',
                                    })
                                  }
                                  className="rounded-lg border border-slate-700 bg-slate-800 p-1.5 text-slate-300 hover:bg-slate-700"
                                >
                                  {effectiveStatus === 'Suspended' ? (
                                    <PlayCircle className="h-3.5 w-3.5 text-emerald-400" />
                                  ) : (
                                    <PauseCircle className="h-3.5 w-3.5 text-amber-400" />
                                  )}
                                </button>
                                <button
                                  type="button"
                                  title={t('superadminBilling.btnResetPass')}
                                  onClick={() => setResetPassTarget(item)}
                                  className="rounded-lg border border-slate-700 bg-slate-800 p-1.5 text-slate-300 hover:bg-slate-700"
                                >
                                  <KeyRound className="h-3.5 w-3.5" />
                                </button>
                                <button
                                  type="button"
                                  title={t('superadminBilling.btnSoftDelete')}
                                  disabled={softDeletingId === item.tenantId}
                                  onClick={() => void handleSoftDelete(item)}
                                  className="rounded-lg border border-rose-800 bg-rose-950/60 p-1.5 text-rose-300 hover:bg-rose-900/60 disabled:opacity-50"
                                >
                                  <Trash2 className="h-3.5 w-3.5" />
                                </button>
                              </>
                            )}
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
      )}

      {activeTab === 'approvals' && <PaymentApprovalsTab canMutate={canMutate} />}

      {activeTab === 'trash' && <TrashTab canMutate={canMutate} />}

      {activeTab === 'provision' && canMutate && <ProvisionGpTab />}

      {activeTab === 'agents' && (
        <AgentKycTab
          agents={agentsQuery.data ?? []}
          isLoading={agentsQuery.isLoading}
          canMutate={canMutate}
        />
      )}

      {activeTab === 'audit' && <AuditLogsTab tenants={tenantsQuery.data ?? []} />}

      {activeTab === 'settings' && <PlatformSettingsTab canMutate={canMutate} />}

      {drawerTenantId && (
        <TenantDetailDrawer
          tenantId={drawerTenantId}
          canMutate={canMutate}
          onClose={() => setDrawerTenantId(null)}
          onImpersonate={(tenant) => setImpersonateTarget(tenant)}
          onStatusAction={(tenant, action) => setStatusModal({ tenant, action })}
          onResetPassword={(tenant) => setResetPassTarget(tenant)}
        />
      )}

      {impersonateTarget && (
        <ImpersonateModal
          key={impersonateTarget.tenantId}
          tenant={impersonateTarget}
          onClose={() => setImpersonateTarget(null)}
          onStarted={handleImpersonationStarted}
        />
      )}

      {statusModal && (
        <StatusActionModal
          key={`${statusModal.tenant.tenantId}-${statusModal.action}`}
          tenant={statusModal.tenant}
          action={statusModal.action}
          onClose={() => setStatusModal(null)}
          onCompleted={() => {
            setStatusModal(null);
            void refreshSuperAdminQueries();
          }}
        />
      )}

      {resetPassTarget && (
        <ResetPasswordModal
          key={resetPassTarget.tenantId}
          tenant={resetPassTarget}
          onClose={() => setResetPassTarget(null)}
        />
      )}
    </div>
  );
}
