import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  BadgeIndianRupee,
  CheckCircle2,
  Clock,
  Crown,
  Download,
  Eye,
  FileDown,
  Plus,
  Search,
  ShieldCheck,
  UserCheck,
  Users,
  Wallet,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { KycStatusBadge } from '../../components/StatusBadge';
import { useAuth } from '../../context/useAuth';
import {
  type AgentPayoutItem,
  agentPortalApi,
  type SuperAdminAgentSummary,
  superAdminAgentsApi,
} from '../../services/agentPortalApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import {
  CreateManualAgentModal,
  ExecuteSettlementModal,
  KycInspectionModal,
} from './AgentGovernanceModals';

type GovernanceTab = 'directory' | 'pendingKyc' | 'settlements' | 'history';

export function SuperAdminAgentsPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const hasAccess = Boolean(
    accessToken && (user?.role === 'SuperAdmin' || user?.role === 'Support_Admin'),
  );
  const canMutate = user?.role === 'SuperAdmin';

  const [activeTab, setActiveTab] = useState<GovernanceTab>('directory');
  const [search, setSearch] = useState('');
  const [districtFilter, setDistrictFilter] = useState('ALL');
  const [talukaFilter, setTalukaFilter] = useState('ALL');
  const [statusFilter, setStatusFilter] = useState('ALL');

  const [manualModalOpen, setManualModalOpen] = useState(false);
  const [inspectAgent, setInspectAgent] = useState<SuperAdminAgentSummary | null>(null);
  const [settlementState, setSettlementState] = useState<{
    open: boolean;
    agentId?: string;
    request?: AgentPayoutItem | null;
  }>({ open: false });

  const agentsQuery = useQuery({
    queryKey: ['superadmin', 'agents-directory'],
    queryFn: () => superAdminAgentsApi.listAgents(),
    enabled: hasAccess,
  });

  const settlementsQuery = useQuery({
    queryKey: ['superadmin', 'agents-settlements'],
    queryFn: superAdminAgentsApi.getSettlements,
    enabled: hasAccess,
  });

  const allAgents = useMemo(() => agentsQuery.data ?? [], [agentsQuery.data]);
  const hub = settlementsQuery.data;

  const districts = useMemo(() => {
    const set = new Set<string>();
    allAgents.forEach((a) => {
      if (a.district) set.add(a.district);
    });
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }, [allAgents]);

  const talukas = useMemo(() => {
    const set = new Set<string>();
    allAgents.forEach((a) => {
      if (districtFilter !== 'ALL' && a.district !== districtFilter) return;
      a.talukasCsv
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean)
        .forEach((tal) => set.add(tal));
    });
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }, [allAgents, districtFilter]);

  const filteredAgents = useMemo(() => {
    const q = search.trim().toLowerCase();
    return allAgents.filter((a) => {
      if (districtFilter !== 'ALL' && a.district !== districtFilter) return false;
      if (
        talukaFilter !== 'ALL' &&
        !a.talukasCsv
          .split(',')
          .map((s) => s.trim().toLowerCase())
          .includes(talukaFilter.toLowerCase())
      ) {
        return false;
      }
      if (statusFilter !== 'ALL' && a.kycStatus !== statusFilter) return false;
      if (!q) return true;
      return (
        a.fullName.toLowerCase().includes(q) ||
        a.agentCode.toLowerCase().includes(q) ||
        a.mobileNo.includes(q) ||
        a.email.toLowerCase().includes(q) ||
        a.district.toLowerCase().includes(q)
      );
    });
  }, [allAgents, districtFilter, talukaFilter, statusFilter, search]);

  const pendingKycAgents = useMemo(
    () => allAgents.filter((a) => a.kycStatus === 'PendingApproval'),
    [allAgents],
  );

  async function refreshQueries() {
    await queryClient.invalidateQueries({ queryKey: ['superadmin'] });
  }

  if (!hasAccess) {
    return (
      <div className="mx-auto max-w-lg rounded-2xl border border-amber-500/30 bg-slate-900 p-6 text-sm text-amber-100 space-y-3 shadow-xl">
        <div className="flex items-center gap-2 font-extrabold text-amber-300">
          <Crown className="h-5 w-5" />
          <h1>{t('superadminAgents.pageTitle')}</h1>
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
    id: GovernanceTab;
    label: string;
    icon: typeof Users;
    badge?: number;
  }[] = [
    {
      id: 'directory',
      label: t('superadminAgents.tabDirectory'),
      icon: Users,
      badge: allAgents.length,
    },
    {
      id: 'pendingKyc',
      label: t('superadminAgents.tabPendingKyc'),
      icon: Clock,
      badge: pendingKycAgents.length,
    },
    {
      id: 'settlements',
      label: t('superadminAgents.tabSettlements'),
      icon: Wallet,
      badge:
        (hub?.pendingWithdrawalRequests.length ?? 0) +
        (hub?.agentPayableSummaries.filter((a) => a.pendingPayableBalance > 0).length ?? 0),
    },
    {
      id: 'history',
      label: t('superadminAgents.tabHistory'),
      icon: CheckCircle2,
      badge: hub?.disbursedHistory.length,
    },
  ];

  return (
    <div className="space-y-6">
      {/* Header */}
      <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-xl flex flex-wrap items-center justify-between gap-4">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2.5">
            <UserCheck className="h-5 w-5 text-amber-400" />
            <h1 className="text-xl font-extrabold text-white">{t('superadminAgents.pageTitle')}</h1>
          </div>
          <p className="text-xs text-slate-400">{t('superadminAgents.pageSubtitle')}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 text-xs">
          <Link
            to="/superadmin"
            className="inline-flex items-center gap-1.5 rounded-xl border border-slate-700 bg-slate-800 px-3.5 py-2 font-bold text-slate-200 hover:bg-slate-700"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            {t('superadminAgents.backToHqBtn')}
          </Link>

          {canMutate && (
            <>
              <button
                type="button"
                onClick={() => setManualModalOpen(true)}
                className="inline-flex items-center gap-1.5 rounded-xl border border-amber-500/40 bg-amber-500/15 px-3.5 py-2 font-extrabold text-amber-300 hover:bg-amber-500/25"
              >
                <Plus className="h-4 w-4" />
                {t('superadminAgents.addAgentBtn')}
              </button>
              <button
                type="button"
                onClick={() => setSettlementState({ open: true })}
                className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-600 px-4 py-2 font-extrabold text-white hover:bg-emerald-500 shadow"
              >
                <BadgeIndianRupee className="h-4 w-4" />
                {t('superadminAgents.executeSettlementBtn')}
              </button>
            </>
          )}
        </div>
      </section>

      {!canMutate && (
        <div className="rounded-xl border border-sky-500/40 bg-sky-950/50 px-4 py-2.5 text-xs font-semibold text-sky-200 flex items-center gap-2">
          <Eye className="h-4 w-4 shrink-0 text-sky-400" />
          <span>{t('superadminAgents.readOnlyBanner')}</span>
        </div>
      )}

      {/* 5 KPI Cards */}
      <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5 text-xs">
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <p className="font-semibold text-slate-400">{t('superadminAgents.kpiTotalAgents')}</p>
          <p className="mono text-2xl font-extrabold text-white mt-1">
            {hub?.totalAgents ?? allAgents.length}
          </p>
        </div>
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <p className="font-semibold text-slate-400">{t('superadminAgents.kpiPendingKyc')}</p>
          <p className="mono text-2xl font-extrabold text-amber-400 mt-1">
            {hub?.pendingKycCount ?? pendingKycAgents.length}
          </p>
        </div>
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <p className="font-semibold text-slate-400">{t('superadminAgents.kpiActiveAgents')}</p>
          <p className="mono text-2xl font-extrabold text-emerald-400 mt-1">
            {hub?.activeAgentsCount ?? allAgents.filter((a) => a.kycStatus === 'Approved').length}
          </p>
        </div>
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <p className="font-semibold text-slate-400">
            {t('superadminAgents.kpiPendingSettlement')}
          </p>
          <p className="mono text-2xl font-extrabold text-rose-400 mt-1">
            {formatInr(hub?.pendingSettlementAmount ?? 0)}
          </p>
        </div>
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <p className="font-semibold text-slate-400">{t('superadminAgents.kpiTotalDisbursed')}</p>
          <p className="mono text-2xl font-extrabold text-sky-400 mt-1">
            {formatInr(hub?.totalDisbursed ?? 0)}
          </p>
        </div>
      </section>

      {/* Tabs */}
      <nav className="flex flex-wrap gap-2 border-b border-slate-800 pb-3" role="tablist">
        {tabs.map((tab) => {
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
              {typeof tab.badge === 'number' && tab.badge > 0 && (
                <span className="rounded-full bg-slate-800 px-1.5 py-0.5 mono text-[10px] font-extrabold text-amber-300">
                  {tab.badge}
                </span>
              )}
            </button>
          );
        })}
      </nav>

      {/* Tab 1: All Agents Directory */}
      {activeTab === 'directory' && (
        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-4">
          <div className="flex flex-wrap items-center gap-2.5 text-xs">
            <div className="relative flex-1 min-w-[220px]">
              <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-500" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t('superadminAgents.searchPlaceholder')}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 pl-9 pr-3 py-1.5 text-slate-100 placeholder:text-slate-500 focus:border-amber-400"
              />
            </div>

            <select
              aria-label={t('superadminAgents.allDistricts')}
              value={districtFilter}
              onChange={(e) => {
                setDistrictFilter(e.target.value);
                setTalukaFilter('ALL');
              }}
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-slate-200"
            >
              <option value="ALL">{t('superadminAgents.allDistricts')}</option>
              {districts.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>

            <select
              aria-label={t('superadminAgents.allTalukas')}
              value={talukaFilter}
              onChange={(e) => setTalukaFilter(e.target.value)}
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-slate-200"
            >
              <option value="ALL">{t('superadminAgents.allTalukas')}</option>
              {talukas.map((tal) => (
                <option key={tal} value={tal}>
                  {tal}
                </option>
              ))}
            </select>

            <select
              aria-label={t('superadminAgents.allStatuses')}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-slate-200"
            >
              <option value="ALL">{t('superadminAgents.allStatuses')}</option>
              <option value="PendingApproval">{t('kycStatus.PendingApproval')}</option>
              <option value="Approved">{t('kycStatus.Approved')}</option>
              <option value="Suspended">{t('kycStatus.Suspended')}</option>
              <option value="Rejected">{t('kycStatus.Rejected')}</option>
            </select>
          </div>

          {agentsQuery.isLoading ? (
            <div className="space-y-2">
              {[1, 2, 3].map((n) => (
                <div key={n} className="h-14 animate-pulse rounded-xl bg-slate-800" />
              ))}
            </div>
          ) : filteredAgents.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-800 p-8 text-center text-xs text-slate-400">
              {t('superadminAgents.noAgentsMatch')}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-800 bg-slate-950 text-slate-400">
                  <tr>
                    <th className="px-3 py-2.5">{t('superadminAgents.colAgentCode')}</th>
                    <th className="px-3 py-2.5">{t('superadminAgents.colLocation')}</th>
                    <th className="px-3 py-2.5">{t('superadminAgents.colContact')}</th>
                    <th className="px-3 py-2.5 text-right">{t('superadminAgents.colGpsCount')}</th>
                    <th className="px-3 py-2.5 text-right">{t('superadminAgents.colSlabs')}</th>
                    <th className="px-3 py-2.5 text-right">{t('superadminAgents.colAccrued')}</th>
                    <th className="px-3 py-2.5 text-right">{t('superadminAgents.colPayable')}</th>
                    <th className="px-3 py-2.5">{t('superadminAgents.colStatus')}</th>
                    <th className="px-3 py-2.5 text-right">{t('superadminAgents.colActions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/70">
                  {filteredAgents.map((ag) => (
                    <tr key={ag.agentId} className="hover:bg-slate-800/40">
                      <td className="px-3 py-3">
                        <p className="font-bold text-white">{ag.fullName}</p>
                        <p className="mono text-[11px] font-bold text-amber-300">{ag.agentCode}</p>
                      </td>
                      <td className="px-3 py-3 text-slate-300">
                        <p>{ag.district}</p>
                        <p className="text-[11px] text-slate-400">{ag.talukasCsv}</p>
                      </td>
                      <td className="px-3 py-3">
                        <p className="mono text-slate-200">{ag.mobileNo}</p>
                        <p className="text-[11px] text-slate-400">{ag.email}</p>
                      </td>
                      <td className="px-3 py-3 text-right mono text-slate-200">
                        {ag.activeGpCount} / {ag.onboardedGpCount}
                      </td>
                      <td className="px-3 py-3 text-right mono text-sky-300">
                        {ag.approvedNewGpRatePct}% / {ag.approvedRenewalRatePct}%
                      </td>
                      <td className="px-3 py-3 text-right mono text-emerald-400">
                        {formatInr(ag.totalAccruedCommission)}
                      </td>
                      <td className="px-3 py-3 text-right mono font-bold text-amber-300">
                        {formatInr(ag.pendingPayableBalance)}
                      </td>
                      <td className="px-3 py-3">
                        <KycStatusBadge status={ag.kycStatus} dark />
                      </td>
                      <td className="px-3 py-3 text-right">
                        <div className="inline-flex items-center gap-1.5">
                          <button
                            type="button"
                            onClick={() => setInspectAgent(ag)}
                            className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 font-bold text-slate-200 hover:bg-slate-700"
                          >
                            <ShieldCheck className="h-3.5 w-3.5 text-emerald-400" />
                            {t('superadminAgents.inspectKycBtn')}
                          </button>
                          {canMutate && ag.pendingPayableBalance > 0 && (
                            <button
                              type="button"
                              onClick={() =>
                                setSettlementState({ open: true, agentId: ag.agentId })
                              }
                              className="rounded-lg bg-emerald-600 px-2.5 py-1 font-bold text-white hover:bg-emerald-500"
                            >
                              {t('superadminAgents.settleNowBtn')}
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* Tab 2: Pending KYC Queue */}
      {activeTab === 'pendingKyc' && (
        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-3 text-xs">
          {pendingKycAgents.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-800 p-8 text-center text-slate-400">
              {t('superadminAgents.pendingKycEmpty')}
            </div>
          ) : (
            pendingKycAgents.map((ag) => (
              <div
                key={ag.agentId}
                className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-slate-800 bg-slate-950 p-4"
              >
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-extrabold text-white">{ag.fullName}</span>
                    <span className="mono rounded bg-slate-800 px-2 py-0.5 text-[11px] font-bold text-amber-300">
                      {ag.agentCode}
                    </span>
                    <KycStatusBadge status={ag.kycStatus} dark />
                  </div>
                  <p className="mono text-[11px] text-slate-400">
                    {ag.mobileNo} · {ag.district} ({ag.talukasCsv}) · {ag.aadhaarMasked} ·{' '}
                    {ag.panMasked}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => setInspectAgent(ag)}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-amber-500 px-4 py-2 font-extrabold text-slate-950 hover:bg-amber-400"
                >
                  <ShieldCheck className="h-4 w-4" />
                  {t('superadminAgents.inspectKycBtn')}
                </button>
              </div>
            ))
          )}
        </section>
      )}

      {/* Tab 3: Commission Settlement Hub */}
      {activeTab === 'settlements' && (
        <div className="space-y-6 text-xs">
          {hub && hub.pendingWithdrawalRequests.length > 0 && (
            <section className="rounded-2xl border border-amber-500/40 bg-amber-950/20 p-5 space-y-3">
              <h2 className="text-sm font-extrabold text-amber-300">
                {t('superadminAgents.pendingRequestsTitle')}
              </h2>
              <div className="space-y-2">
                {hub.pendingWithdrawalRequests.map((req) => (
                  <div
                    key={req.payoutId}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-900 p-4"
                  >
                    <div className="space-y-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="mono font-extrabold text-amber-300">{req.voucherNo}</span>
                        <span className="font-bold text-white">
                          {req.agentName} ({req.agentCode})
                        </span>
                      </div>
                      <p className="mono text-[11px] text-slate-400">
                        {formatInr(req.payoutAmount)} · {req.paymentMode} ·{' '}
                        {formatDateOnly(req.requestedAtUtc || req.createdAtUtc, i18n.language)}
                        {req.remarks ? ` · ${req.remarks}` : ''}
                      </p>
                    </div>

                    {canMutate && (
                      <button
                        type="button"
                        onClick={() =>
                          setSettlementState({
                            open: true,
                            agentId: req.agentId,
                            request: req,
                          })
                        }
                        className="rounded-xl bg-emerald-600 px-4 py-2 font-extrabold text-white hover:bg-emerald-500"
                      >
                        {t('superadminAgents.fulfillRequestBtn')}
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </section>
          )}

          <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-4">
            <h2 className="text-sm font-extrabold text-white">
              {t('superadminAgents.agentPayableCardsTitle')}
            </h2>

            {(hub?.agentPayableSummaries.filter((a) => a.pendingPayableBalance > 0) ?? []).length ===
            0 ? (
              <p className="rounded-xl border border-dashed border-slate-800 p-6 text-center text-slate-400">
                {t('superadminAgents.noPendingPayables')}
              </p>
            ) : (
              <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
                {hub?.agentPayableSummaries
                  .filter((a) => a.pendingPayableBalance > 0)
                  .map((ag) => (
                    <div
                      key={ag.agentId}
                      className="rounded-xl border border-slate-800 bg-slate-950 p-4 flex flex-col justify-between gap-3"
                    >
                      <div className="space-y-1">
                        <div className="flex items-center justify-between">
                          <span className="font-extrabold text-white">{ag.fullName}</span>
                          <span className="mono text-[11px] text-amber-300">{ag.agentCode}</span>
                        </div>
                        <p className="text-[11px] text-slate-400">
                          {ag.upiId || ag.accountNumber || ag.mobileNo}
                        </p>
                        <p className="mono text-lg font-extrabold text-rose-400 pt-1">
                          {formatInr(ag.pendingPayableBalance)}
                        </p>
                      </div>

                      {canMutate && (
                        <button
                          type="button"
                          onClick={() => setSettlementState({ open: true, agentId: ag.agentId })}
                          className="rounded-lg bg-emerald-600 px-3 py-2 font-bold text-white hover:bg-emerald-500"
                        >
                          {t('superadminAgents.settleNowBtn')}
                        </button>
                      )}
                    </div>
                  ))}
              </div>
            )}
          </section>

          {hub && hub.unsettledCommissions.length > 0 && (
            <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-3">
              <h2 className="text-sm font-extrabold text-white">
                {t('superadminAgents.unsettledCommissionsTitle')}
              </h2>
              <div className="overflow-x-auto">
                <table className="w-full text-left">
                  <thead className="border-b border-slate-800 text-slate-400">
                    <tr>
                      <th className="py-2 pr-3">{t('superadminAgents.colAgentCode')}</th>
                      <th className="py-2 pr-3">{t('agentPortal.colGpName')}</th>
                      <th className="py-2 pr-3">{t('agentPortal.colType')}</th>
                      <th className="py-2 pr-3 text-right">{t('agentPortal.colAccrued')}</th>
                      <th className="py-2 pr-3 text-right">{t('agentPortal.colRemaining')}</th>
                      <th className="py-2">{t('agentPortal.colStatus')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/70">
                    {hub.unsettledCommissions.map((c) => (
                      <tr key={c.commissionId}>
                        <td className="py-2.5 pr-3">
                          <span className="font-bold text-white">{c.agentName}</span>{' '}
                          <span className="mono text-amber-300">({c.agentCode})</span>
                        </td>
                        <td className="py-2.5 pr-3">
                          {c.panchayatNameMr} <span className="mono text-slate-400">({c.lgdCode})</span>
                        </td>
                        <td className="py-2.5 pr-3 text-slate-300">{c.commissionType}</td>
                        <td className="py-2.5 pr-3 text-right mono text-slate-200">
                          {formatInr(c.commissionAmount)}
                        </td>
                        <td className="py-2.5 pr-3 text-right mono font-bold text-rose-400">
                          {formatInr(c.remainingPayable)}
                        </td>
                        <td className="py-2.5 font-bold text-amber-300">{c.status}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </div>
      )}

      {/* Tab 4: Disbursed History & Vouchers */}
      {activeTab === 'history' && (
        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 text-xs">
          {(hub?.disbursedHistory ?? []).length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-800 p-8 text-center text-slate-400">
              {t('superadminAgents.noDisbursedHistory')}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead className="border-b border-slate-800 bg-slate-950 text-slate-400">
                  <tr>
                    <th className="px-3 py-2.5">{t('agentPortal.colVoucherNo')}</th>
                    <th className="px-3 py-2.5">{t('superadminAgents.colAgentCode')}</th>
                    <th className="px-3 py-2.5">{t('agentPortal.colPaymentDate')}</th>
                    <th className="px-3 py-2.5 text-right">{t('agentPortal.colAmount')}</th>
                    <th className="px-3 py-2.5">{t('agentPortal.colMode')}</th>
                    <th className="px-3 py-2.5">{t('agentPortal.colUtrRef')}</th>
                    <th className="px-3 py-2.5 text-right">{t('agentPortal.colReceipt')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/70">
                  {hub?.disbursedHistory.map((p) => (
                    <tr key={p.payoutId} className="hover:bg-slate-800/40">
                      <td className="px-3 py-3 mono font-bold text-amber-300">{p.voucherNo}</td>
                      <td className="px-3 py-3">
                        <span className="font-bold text-white">{p.agentName}</span>{' '}
                        <span className="mono text-slate-400">({p.agentCode})</span>
                      </td>
                      <td className="px-3 py-3 mono text-slate-300">
                        {formatDateOnly(p.paidAtUtc || p.paymentDate, i18n.language)}
                      </td>
                      <td className="px-3 py-3 text-right mono font-bold text-emerald-400">
                        {formatInr(p.payoutAmount)}
                      </td>
                      <td className="px-3 py-3 mono text-slate-300">{p.paymentMode}</td>
                      <td className="px-3 py-3 mono text-slate-300">{p.utrReference}</td>
                      <td className="px-3 py-3 text-right">
                        <div className="inline-flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() =>
                              void superAdminAgentsApi.downloadVoucherPdf(p.payoutId, p.voucherNo)
                            }
                            className="inline-flex items-center gap-1 rounded-lg border border-emerald-500/40 bg-emerald-500/15 px-2.5 py-1 font-bold text-emerald-300 hover:bg-emerald-500/25"
                          >
                            <FileDown className="h-3.5 w-3.5" />
                            {t('agentPortal.downloadVoucherBtn')}
                          </button>
                          {p.receiptAssetKey && (
                            <button
                              type="button"
                              onClick={() =>
                                void agentPortalApi.downloadProtectedAsset(
                                  p.receiptAssetKey!,
                                  `${p.voucherNo}-proof`,
                                )
                              }
                              className="inline-flex items-center gap-1 rounded-lg border border-sky-500/40 bg-sky-500/15 px-2.5 py-1 font-bold text-sky-300 hover:bg-sky-500/25"
                            >
                              <Download className="h-3.5 w-3.5" />
                              {t('agentPortal.downloadProofBtn')}
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* Modals */}
      {manualModalOpen && (
        <CreateManualAgentModal
          onClose={() => setManualModalOpen(false)}
          onCreated={refreshQueries}
        />
      )}

      {inspectAgent && (
        <KycInspectionModal
          agent={inspectAgent}
          canMutate={canMutate}
          onClose={() => setInspectAgent(null)}
          onDecided={refreshQueries}
        />
      )}

      {settlementState.open && (
        <ExecuteSettlementModal
          agents={allAgents}
          initialAgentId={settlementState.agentId}
          initialRequest={settlementState.request}
          onClose={() => setSettlementState({ open: false })}
        />
      )}
    </div>
  );
}

