import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Building2,
  CheckCircle2,
  Copy,
  Crown,
  KeyRound,
  Plus,
  Printer,
  Search,
  ShieldAlert,
  ShieldCheck,
  UserCheck,
  X,
} from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { KycStatusBadge, TenantStatusBadge } from '../../components/StatusBadge';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { getPanchayatByLgd } from '../../services/lgdApi';
import {
  type AgentProfile,
  type ProvisionedTenantResult,
  type ProvisionTenantPayload,
  sprint2Api,
} from '../../services/sprint2Api';
import { formatDateOnly, formatInr } from '../../utils/formatters';

type HQTab = 'directory' | 'provision' | 'agents';

function generateStrongPassword(): string {
  const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
  const lower = 'abcdefghijkmnopqrstuvwxyz';
  const digits = '23456789';
  const symbols = '@#$!';
  const all = upper + lower + digits;
  const pick = (pool: string) => pool[Math.floor(Math.random() * pool.length)];
  let pwd = pick(upper) + pick(lower) + pick(digits) + pick(symbols);
  for (let i = 0; i < 8; i++) {
    pwd += pick(all);
  }
  return pwd;
}

export function SuperAdminProvisionPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState<HQTab>('directory');
  const [tenantSearch, setTenantSearch] = useState('');
  const [districtFilter, setDistrictFilter] = useState('ALL');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [agentKycFilter, setAgentKycFilter] = useState('ALL');

  const [form, setForm] = useState<ProvisionTenantPayload>({
    lgdCode: '',
    panchayatNameEn: '',
    panchayatNameMr: '',
    districtName: '',
    tehsilName: '',
    districtCode: '27',
    tehsilCode: '000',
    pincode: '',
    censusCode: '',
    officeAddress: '',
    adminFullName: '',
    adminUsername: '',
    adminEmail: '',
    adminPassword: '',
    planName: 'Standard',
    paymentMode: 'UPI',
    paymentAmount: 6000,
    durationDays: 365,
    paymentReference: '',
    totalWards: 5,
    populationCensus: null,
  });

  const [lgdLoading, setLgdLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [provisioned, setProvisioned] = useState<ProvisionedTenantResult | null>(null);
  const [copiedSlip, setCopiedSlip] = useState(false);
  const [lastProvisionedPassword, setLastProvisionedPassword] = useState('');

  // Agent KYC Review modal state
  const [reviewAgent, setReviewAgent] = useState<AgentProfile | null>(null);
  const [newGpRate, setNewGpRate] = useState(20);
  const [renewalRate, setRenewalRate] = useState(15);
  const [reviewReason, setReviewReason] = useState('');
  const [decisionBusy, setDecisionBusy] = useState(false);
  const [agentError, setAgentError] = useState<string | null>(null);

  const isSuperAdmin = Boolean(accessToken && user?.role === 'SuperAdmin');

  const tenantsQuery = useQuery({
    queryKey: ['superadmin', 'tenants'],
    queryFn: sprint2Api.listSuperAdminTenants,
    enabled: isSuperAdmin,
  });

  const agentsQuery = useQuery({
    queryKey: ['superadmin', 'agents'],
    queryFn: sprint2Api.listSuperAdminAgents,
    enabled: isSuperAdmin,
  });

  const districts = useMemo(() => {
    const set = new Set<string>();
    for (const item of tenantsQuery.data ?? []) {
      if (item.districtName) set.add(item.districtName);
    }
    return Array.from(set).sort();
  }, [tenantsQuery.data]);

  const filteredTenants = useMemo(() => {
    const items = tenantsQuery.data ?? [];
    const q = tenantSearch.trim().toLowerCase();
    return items.filter((item) => {
      if (districtFilter !== 'ALL' && item.districtName !== districtFilter) return false;
      if (statusFilter !== 'ALL' && item.status !== statusFilter) return false;
      if (!q) return true;
      return (
        item.panchayatNameEn.toLowerCase().includes(q) ||
        item.panchayatNameMr.toLowerCase().includes(q) ||
        item.lgdCode.includes(q) ||
        item.districtName.toLowerCase().includes(q) ||
        item.tehsilName.toLowerCase().includes(q)
      );
    });
  }, [tenantsQuery.data, tenantSearch, districtFilter, statusFilter]);

  const filteredAgents = useMemo(() => {
    const list = agentsQuery.data ?? [];
    if (agentKycFilter === 'ALL') return list;
    return list.filter((ag) => ag.kycStatus === agentKycFilter);
  }, [agentsQuery.data, agentKycFilter]);

  const stats = useMemo(() => {
    const tenants = tenantsQuery.data ?? [];
    const agents = agentsQuery.data ?? [];
    const activeCount = tenants.filter((x) => x.status === 'Active').length;
    const pendingTenants = tenants.filter((x) => x.status === 'PendingApproval').length;
    const avgSetup =
      tenants.length > 0
        ? Math.round(tenants.reduce((acc, x) => acc + x.setupProgressPct, 0) / tenants.length)
        : 0;
    const pendingAgents = agents.filter((a) => a.kycStatus === 'PendingApproval').length;
    return {
      totalTenants: tenants.length,
      activeCount,
      pendingTenants,
      avgSetup,
      totalAgents: agents.length,
      pendingAgents,
    };
  }, [tenantsQuery.data, agentsQuery.data]);

  async function handleLgdAutoFill() {
    if (!/^\d{5,8}$/.test(form.lgdCode.trim())) return;
    setErrorMsg(null);
    setLgdLoading(true);
    try {
      const gp = await getPanchayatByLgd(form.lgdCode);
      setForm((prev) => ({
        ...prev,
        panchayatNameEn: gp.gp_name_en,
        panchayatNameMr: gp.gp_name_mr,
        districtName: gp.district,
        districtCode: gp.district_code || '27',
        tehsilName: gp.tehsil,
        tehsilCode: gp.tehsil_code || '000',
        pincode: gp.pincode || '',
      }));
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : t('onboarding.notFound'));
    } finally {
      setLgdLoading(false);
    }
  }

  async function handleSubmitProvision(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setSubmitting(true);
    try {
      const passwordSnapshot = form.adminPassword;
      const result = await sprint2Api.provisionTenantDirect(form);
      setLastProvisionedPassword(passwordSnapshot);
      setProvisioned(result);
      await queryClient.invalidateQueries({ queryKey: ['superadmin', 'tenants'] });
    } catch (err) {
      if (err instanceof ApiProblemError) {
        const firstFieldErr = err.problem.errors ? Object.values(err.problem.errors).flat()[0] : null;
        setErrorMsg(firstFieldErr || err.problem.detail || err.message);
      } else {
        setErrorMsg(err instanceof Error ? err.message : t('superadmin.provisionFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  function openReviewModal(agent: AgentProfile) {
    setAgentError(null);
    setReviewAgent(agent);
    setNewGpRate(agent.requestedCommissionRatePct);
    setRenewalRate(Math.max(5, agent.requestedCommissionRatePct - 5));
    setReviewReason(agent.statusReason || '');
  }

  async function submitAgentDecision(status: number) {
    if (!reviewAgent) return;
    setAgentError(null);
    setDecisionBusy(true);
    try {
      await sprint2Api.updateAgentStatus(
        reviewAgent.agentId,
        status,
        newGpRate,
        renewalRate,
        reviewReason.trim() || undefined,
      );
      await queryClient.invalidateQueries({ queryKey: ['superadmin', 'agents'] });
      setReviewAgent(null);
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setAgentError(err.problem.detail || err.message);
      } else {
        setAgentError(err instanceof Error ? err.message : 'Update failed');
      }
    } finally {
      setDecisionBusy(false);
    }
  }

  async function copyCredentialsSlip() {
    if (!provisioned) return;
    const text = [
      `GP: ${provisioned.panchayatNameMr} (${provisioned.panchayatNameEn})`,
      `LGD Code: ${provisioned.lgdCode}`,
      `Tenant ID: ${provisioned.tenantId}`,
      `Username: ${provisioned.adminUsername}`,
      `Password: ${lastProvisionedPassword}`,
      `Receipt Ref: ${provisioned.paymentReference}`,
      `Valid Until: ${formatDateOnly(provisioned.subscriptionExpiresAtUtc, i18n.language)}`,
    ].join('\n');
    await navigator.clipboard.writeText(text);
    setCopiedSlip(true);
    setTimeout(() => setCopiedSlip(false), 2500);
  }

  if (!isSuperAdmin) {
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

  return (
    <div className="space-y-6">
      {/* Statewide KPI Summary Cards */}
      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 no-print">
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <p className="text-xs font-semibold text-slate-400">{t('superadmin.kpiTotalGps')}</p>
          <p className="mt-1 mono text-2xl font-extrabold text-white">{stats.totalTenants}</p>
          <p className="mt-2 flex items-center gap-3 text-[11px] text-slate-400">
            <span>
              <span className="inline-block h-2 w-2 rounded-full bg-emerald-400 mr-1" />
              {t('superadmin.kpiActive')}: <strong className="text-white">{stats.activeCount}</strong>
            </span>
            <span>
              <span className="inline-block h-2 w-2 rounded-full bg-amber-400 mr-1" />
              {t('superadmin.kpiPending')}: <strong className="text-white">{stats.pendingTenants}</strong>
            </span>
          </p>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <p className="text-xs font-semibold text-slate-400">{t('superadmin.kpiAvgSetup')}</p>
          <p className="mt-1 mono text-2xl font-extrabold text-emerald-400">{stats.avgSetup}%</p>
          <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-slate-800">
            <div className="h-full rounded-full bg-emerald-500" style={{ width: `${stats.avgSetup}%` }} />
          </div>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-sm">
          <p className="text-xs font-semibold text-slate-400">{t('superadmin.kpiAgentsTotal')}</p>
          <p className="mt-1 mono text-2xl font-extrabold text-sky-400">{stats.totalAgents}</p>
          <p className="mt-2 text-[11px] text-slate-400">
            {t('superadmin.kpiPendingKyc')}:{' '}
            <strong className="text-amber-300">{stats.pendingAgents}</strong>
          </p>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 flex flex-col justify-between shadow-sm">
          <div className="flex items-center justify-between text-xs">
            <span className="text-slate-400 font-semibold">{t('superadmin.consoleBadge')}</span>
            <span className="inline-flex items-center gap-1 text-emerald-400 font-bold">
              <span className="h-2 w-2 rounded-full bg-emerald-400 animate-pulse" />
              {t('superadmin.systemOnline')}
            </span>
          </div>
          <button
            type="button"
            onClick={() => setActiveTab('provision')}
            className="mt-3 inline-flex items-center justify-center gap-1.5 rounded-lg bg-gradient-to-r from-emerald-600 to-teal-600 px-3 py-2 text-xs font-bold text-white hover:from-emerald-500 hover:to-teal-500"
          >
            <Plus className="h-4 w-4" />
            {t('superadmin.tabProvision')}
          </button>
        </div>
      </section>

      {/* HQ Navigation Tabs */}
      <nav className="flex flex-wrap gap-2 border-b border-slate-800 pb-3 no-print" role="tablist">
        {(
          [
            { id: 'directory', label: t('superadmin.tabDirectory'), icon: Building2 },
            { id: 'provision', label: t('superadmin.tabProvision'), icon: Plus },
            { id: 'agents', label: t('superadmin.tabAgents'), icon: UserCheck },
          ] as const
        ).map((tab) => {
          const Icon = tab.icon;
          const active = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setActiveTab(tab.id)}
              className={`inline-flex items-center gap-2 rounded-xl border px-4 py-2 text-xs font-bold transition ${
                active
                  ? 'border-amber-500/50 bg-amber-500/15 text-amber-300'
                  : 'border-slate-800 bg-slate-900 text-slate-400 hover:border-slate-700 hover:text-slate-200'
              }`}
            >
              <Icon className="h-4 w-4" />
              <span>{tab.label}</span>
              {tab.id === 'agents' && stats.pendingAgents > 0 && (
                <span className="rounded-full bg-amber-500 px-1.5 py-0.5 mono text-[10px] font-extrabold text-slate-950">
                  {stats.pendingAgents}
                </span>
              )}
            </button>
          );
        })}
      </nav>

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
                  aria-label={t('superadmin.searchTenantsPlaceholder')}
                  value={tenantSearch}
                  onChange={(e) => setTenantSearch(e.target.value)}
                  placeholder={t('superadmin.searchTenantsPlaceholder')}
                  className="rounded-lg border border-slate-700 bg-slate-950 pl-8 pr-3 py-1.5 text-slate-100 placeholder:text-slate-500 focus:border-amber-400"
                />
              </div>
              <select
                aria-label={t('superadmin.allDistricts')}
                value={districtFilter}
                onChange={(e) => setDistrictFilter(e.target.value)}
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
                aria-label={t('superadmin.allStatuses')}
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-slate-200"
              >
                <option value="ALL">{t('superadmin.allStatuses')}</option>
                <option value="Active">{t('tenantStatus.Active')}</option>
                <option value="PendingApproval">{t('tenantStatus.PendingApproval')}</option>
                <option value="Trial">{t('tenantStatus.Trial')}</option>
                <option value="Suspended">{t('tenantStatus.Suspended')}</option>
              </select>
            </div>
          </div>

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
                    <th className="px-4 py-3">{t('onboarding.panchayat')}</th>
                    <th className="px-4 py-3">LGD</th>
                    <th className="px-4 py-3">{t('onboarding.district')}</th>
                    <th className="px-4 py-3">{t('superadmin.kpiAvgSetup')}</th>
                    <th className="px-4 py-3">{t('superadmin.durationDays')}</th>
                    <th className="px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {filteredTenants.map((item) => (
                    <tr key={item.tenantId} className="hover:bg-slate-800/40">
                      <td className="px-4 py-3">
                        <p className="font-bold text-white">{item.panchayatNameMr}</p>
                        <p className="text-[11px] text-slate-400">{item.panchayatNameEn}</p>
                      </td>
                      <td className="px-4 py-3 mono font-bold text-amber-300">{item.lgdCode}</td>
                      <td className="px-4 py-3 text-slate-300">
                        {item.districtName} · {item.tehsilName}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <div className="h-1.5 w-20 overflow-hidden rounded-full bg-slate-800">
                            <div
                              className="h-full rounded-full bg-emerald-500"
                              style={{ width: `${item.setupProgressPct}%` }}
                            />
                          </div>
                          <span className="mono text-[11px] text-slate-300">
                            {item.setupProgressPct}%
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 mono text-[11px] text-slate-400">
                        {formatDateOnly(item.subscriptionExpiresAtUtc, i18n.language)}
                      </td>
                      <td className="px-4 py-3">
                        <TenantStatusBadge status={item.status} dark />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* TAB 2: Direct Tenant Provisioning */}
      {activeTab === 'provision' && (
        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6 shadow-sm space-y-5">
          <div className="flex items-center gap-2 text-emerald-400">
            <Building2 className="h-5 w-5" />
            <h2 className="text-lg font-extrabold text-white">{t('superadmin.tabProvision')}</h2>
          </div>

          {errorMsg && (
            <div className="flex items-center gap-2 rounded-xl border border-rose-500/40 bg-rose-950/60 px-4 py-3 text-xs text-rose-200">
              <ShieldAlert className="h-4 w-4 shrink-0" />
              <span>{errorMsg}</span>
            </div>
          )}

          {provisioned && (
            <div className="rounded-2xl border border-emerald-500/40 bg-emerald-950/40 p-5 space-y-3 text-xs">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2 font-extrabold text-emerald-300 text-sm">
                  <CheckCircle2 className="h-5 w-5" />
                  <span>{t('superadmin.successTitle')}</span>
                </div>
                <div className="flex gap-2 no-print">
                  <button
                    type="button"
                    onClick={() => void copyCredentialsSlip()}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-3 py-1.5 font-bold text-white hover:bg-emerald-500"
                  >
                    <Copy className="h-3.5 w-3.5" />
                    {copiedSlip ? t('superadmin.copiedNotice') : t('superadmin.copyCredentialsBtn')}
                  </button>
                  <button
                    type="button"
                    onClick={() => window.print()}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-500/40 bg-slate-900 px-3 py-1.5 font-bold text-emerald-200 hover:bg-slate-800"
                  >
                    <Printer className="h-3.5 w-3.5" />
                    {t('superadmin.printSlipBtn')}
                  </button>
                </div>
              </div>
              <div className="grid gap-2 sm:grid-cols-3 rounded-xl border border-emerald-500/20 bg-slate-950/70 p-3 mono text-[11px] text-slate-200">
                <p>
                  GP: <strong>{provisioned.panchayatNameMr}</strong> ({provisioned.lgdCode})
                </p>
                <p>
                  Username: <strong className="text-amber-300">{provisioned.adminUsername}</strong>
                </p>
                <p>
                  Ref: <strong>{provisioned.paymentReference}</strong> · {formatInr(provisioned.amountPaid)}
                </p>
              </div>
            </div>
          )}

          <form onSubmit={(e) => void handleSubmitProvision(e)} className="space-y-5 text-xs">
            <div className="space-y-3">
              <p className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
                {t('superadmin.sectionGeo')}
              </p>
              <div className="grid gap-3 sm:grid-cols-3">
                <div>
                  <label htmlFor="sup-lgd" className="block font-bold text-slate-300 mb-1">
                    {t('onboarding.byCode')} <span className="text-rose-400">*</span>
                  </label>
                  <div className="flex gap-2">
                    <input
                      id="sup-lgd"
                      type="text"
                      required
                      value={form.lgdCode}
                      onChange={(e) => setForm((p) => ({ ...p, lgdCode: e.target.value }))}
                      placeholder="171391"
                      className="flex-1 rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-amber-300 font-bold"
                    />
                    <button
                      type="button"
                      disabled={lgdLoading}
                      onClick={() => void handleLgdAutoFill()}
                      className="rounded-lg bg-slate-800 border border-slate-700 px-3 py-2 font-bold text-white hover:bg-slate-700 disabled:opacity-50"
                    >
                      {lgdLoading ? '...' : t('onboarding.verifyBtn')}
                    </button>
                  </div>
                </div>

                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.gpNameMr')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.panchayatNameMr}
                    onChange={(e) => setForm((p) => ({ ...p, panchayatNameMr: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  />
                </label>

                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.gpNameEn')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.panchayatNameEn}
                    onChange={(e) => setForm((p) => ({ ...p, panchayatNameEn: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  />
                </label>

                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.district')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.districtName}
                    onChange={(e) => setForm((p) => ({ ...p, districtName: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  />
                </label>

                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.tehsil')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.tehsilName}
                    onChange={(e) => setForm((p) => ({ ...p, tehsilName: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  />
                </label>

                <label className="space-y-1 font-bold text-slate-300">
                  <span>{t('onboarding.pincode')}</span>
                  <input
                    type="text"
                    value={form.pincode || ''}
                    onChange={(e) => setForm((p) => ({ ...p, pincode: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
                  />
                </label>
              </div>
            </div>

            <div className="space-y-3 border-t border-slate-800 pt-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
                  {t('superadmin.sectionAdmin')}
                </p>
                <button
                  type="button"
                  onClick={() =>
                    setForm((p) => ({ ...p, adminPassword: generateStrongPassword() }))
                  }
                  className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-[11px] font-bold text-amber-300 hover:bg-slate-700"
                >
                  <KeyRound className="h-3 w-3" />
                  {t('superadmin.generatePassBtn')}
                </button>
              </div>
              <div className="grid gap-3 sm:grid-cols-4">
                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('superadmin.adminFullName')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.adminFullName}
                    onChange={(e) => setForm((p) => ({ ...p, adminFullName: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.adminUsername')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.adminUsername}
                    onChange={(e) => setForm((p) => ({ ...p, adminUsername: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.adminEmail')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="email"
                    required
                    value={form.adminEmail}
                    onChange={(e) => setForm((p) => ({ ...p, adminEmail: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>
                    {t('onboarding.adminPassword')} <span className="text-rose-400">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.adminPassword}
                    onChange={(e) => setForm((p) => ({ ...p, adminPassword: e.target.value }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300 font-bold"
                  />
                </label>
              </div>
            </div>

            <div className="space-y-3 border-t border-slate-800 pt-4">
              <p className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
                {t('superadmin.sectionSubscription')}
              </p>
              <div className="grid gap-3 sm:grid-cols-5">
                <label className="space-y-1 font-bold text-slate-300">
                  <span>{t('superadmin.planLabel')}</span>
                  <select
                    value={form.planName}
                    onChange={(e) =>
                      setForm((p) => ({
                        ...p,
                        planName: e.target.value as 'Standard' | 'Enterprise',
                      }))
                    }
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  >
                    <option value="Standard">{t('superadmin.planStandard')}</option>
                    <option value="Enterprise">{t('superadmin.planEnterprise')}</option>
                  </select>
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>{t('superadmin.paymentMode')}</span>
                  <select
                    value={form.paymentMode}
                    onChange={(e) =>
                      setForm((p) => ({
                        ...p,
                        paymentMode: e.target.value as ProvisionTenantPayload['paymentMode'],
                      }))
                    }
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  >
                    <option value="UPI">{t('superadmin.modeUpi')}</option>
                    <option value="NEFT_RTGS">{t('superadmin.modeNeft')}</option>
                    <option value="CASH">{t('superadmin.modeCash')}</option>
                    <option value="CHEQUE">{t('superadmin.modeCheque')}</option>
                    <option value="TREASURY">{t('superadmin.modeTreasury')}</option>
                  </select>
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>{t('superadmin.amount')}</span>
                  <input
                    type="number"
                    min={1}
                    value={form.paymentAmount}
                    onChange={(e) => setForm((p) => ({ ...p, paymentAmount: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>{t('superadmin.durationDays')}</span>
                  <select
                    value={form.durationDays}
                    onChange={(e) => setForm((p) => ({ ...p, durationDays: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                  >
                    <option value={365}>{t('superadmin.daysOption', { days: 365 })}</option>
                    <option value={730}>{t('superadmin.daysOption', { days: 730 })}</option>
                    <option value={1095}>{t('superadmin.daysOption', { days: 1095 })}</option>
                  </select>
                </label>
                <label className="space-y-1 font-bold text-slate-300">
                  <span>{t('superadmin.paymentRef')}</span>
                  <input
                    type="text"
                    value={form.paymentReference || ''}
                    onChange={(e) => setForm((p) => ({ ...p, paymentReference: e.target.value }))}
                    placeholder={t('superadmin.paymentRefPlaceholder')}
                    className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
                  />
                </label>
              </div>
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-6 py-2.5 text-xs font-bold text-white hover:from-emerald-500 hover:to-teal-500 disabled:opacity-60 transition shadow-md"
            >
              {submitting ? t('onboarding.loading') : t('superadmin.provisionBtn')}
            </button>
          </form>
        </section>
      )}

      {/* TAB 3: Field Agent KYC Queue */}
      {activeTab === 'agents' && (
        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-sm space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <UserCheck className="h-4 w-4 text-emerald-400" />
              <h2 className="text-sm font-extrabold text-white">
                {t('superadmin.agentKycQueueTitle')}
              </h2>
            </div>

            <select
              aria-label={t('superadmin.allKycStatuses')}
              value={agentKycFilter}
              onChange={(e) => setAgentKycFilter(e.target.value)}
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-xs text-slate-200"
            >
              <option value="ALL">{t('superadmin.allKycStatuses')}</option>
              <option value="PendingApproval">{t('kycStatus.PendingApproval')}</option>
              <option value="Approved">{t('kycStatus.Approved')}</option>
              <option value="Suspended">{t('kycStatus.Suspended')}</option>
              <option value="Rejected">{t('kycStatus.Rejected')}</option>
            </select>
          </div>

          {agentsQuery.isLoading && (
            <div className="space-y-2">
              {[1, 2].map((n) => (
                <div key={n} className="h-16 animate-pulse rounded-xl bg-slate-800" />
              ))}
            </div>
          )}

          {!agentsQuery.isLoading && filteredAgents.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/60 p-8 text-center text-xs text-slate-400">
              {t('superadmin.noAgentsMatch')}
            </div>
          ) : (
            <div className="space-y-2.5 text-xs">
              {filteredAgents.map((ag) => (
                <div
                  key={ag.agentId}
                  className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-950/80 p-4"
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-extrabold text-white text-sm">{ag.fullName}</span>
                      <span className="mono rounded bg-slate-800 px-2 py-0.5 text-[11px] font-bold text-amber-300">
                        {ag.agentCode}
                      </span>
                      <KycStatusBadge status={ag.kycStatus} dark />
                    </div>
                    <p className="mono text-[11px] text-slate-400">
                      {ag.mobileNo} · {ag.aadhaarMasked} · {ag.panMasked} · {ag.district} ({ag.talukasCsv})
                    </p>
                    <p className="text-[11px] text-slate-400">
                      Requested: <strong className="text-white">{ag.requestedCommissionRatePct}%</strong>{' '}
                      {ag.kycStatus === 'Approved' && (
                        <span>
                          · Approved: <strong className="text-emerald-400">{ag.approvedNewGpRatePct}% / {ag.approvedRenewalRatePct}%</strong>
                        </span>
                      )}
                    </p>
                  </div>

                  <button
                    type="button"
                    onClick={() => openReviewModal(ag)}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-amber-500/40 bg-amber-500/15 px-3.5 py-2 text-xs font-bold text-amber-300 hover:bg-amber-500/25 transition"
                  >
                    <ShieldCheck className="h-4 w-4" />
                    {t('superadmin.reviewActionBtn')}
                  </button>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {/* Agent KYC Decision Modal */}
      {reviewAgent && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-xs"
          role="dialog"
          aria-modal="true"
          aria-labelledby="review-modal-title"
        >
          <div className="w-full max-w-lg rounded-2xl border border-slate-700 bg-slate-900 p-6 shadow-2xl space-y-4 text-xs text-slate-200">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 id="review-modal-title" className="text-sm font-extrabold text-white">
                {t('superadmin.modalReviewTitle')} — {reviewAgent.fullName}
              </h3>
              <button
                type="button"
                aria-label={t('superadmin.cancelBtn')}
                onClick={() => setReviewAgent(null)}
                className="rounded p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {agentError && (
              <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
                {agentError}
              </div>
            )}

            <div className="rounded-xl border border-slate-800 bg-slate-950 p-3 mono text-[11px] space-y-1">
              <p>Code: {reviewAgent.agentCode}</p>
              <p>Aadhaar: {reviewAgent.aadhaarMasked} · PAN: {reviewAgent.panMasked}</p>
              <p>District: {reviewAgent.district} · {reviewAgent.talukasCsv}</p>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="space-y-1 font-bold text-slate-300">
                <span>{t('superadmin.newGpRateLabel')}</span>
                <input
                  type="number"
                  min={1}
                  max={50}
                  value={newGpRate}
                  onChange={(e) => setNewGpRate(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300 font-bold"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-300">
                <span>{t('superadmin.renewalRateLabel')}</span>
                <input
                  type="number"
                  min={0}
                  max={50}
                  value={renewalRate}
                  onChange={(e) => setRenewalRate(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-sky-300 font-bold"
                />
              </label>
            </div>

            <label className="block space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.reviewReasonLabel')}</span>
              <textarea
                rows={3}
                value={reviewReason}
                onChange={(e) => setReviewReason(e.target.value)}
                placeholder={t('superadmin.reviewReasonPlaceholder')}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <div className="flex flex-wrap justify-end gap-2 border-t border-slate-800 pt-3">
              <button
                type="button"
                disabled={decisionBusy}
                onClick={() => void submitAgentDecision(4)}
                className="rounded-lg bg-rose-700 px-3.5 py-2 font-bold text-white hover:bg-rose-600 disabled:opacity-50"
              >
                {t('superadmin.rejectBtn')}
              </button>
              <button
                type="button"
                disabled={decisionBusy}
                onClick={() => void submitAgentDecision(3)}
                className="rounded-lg bg-amber-600 px-3.5 py-2 font-bold text-white hover:bg-amber-500 disabled:opacity-50"
              >
                {t('superadmin.suspendBtn')}
              </button>
              <button
                type="button"
                disabled={decisionBusy}
                onClick={() => void submitAgentDecision(2)}
                className="rounded-lg bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500 disabled:opacity-50"
              >
                {t('superadmin.approveBtn')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
