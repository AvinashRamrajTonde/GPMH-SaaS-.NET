import { useQuery } from '@tanstack/react-query';
import {
  Award,
  Building2,
  CalendarClock,
  Check,
  Copy,
  FileSpreadsheet,
  KeyRound,
  LayoutDashboard,
  MapPin,
  Plus,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  User,
  Wallet,
} from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { KycStatusBadge } from '../../components/StatusBadge';
import { useAuth } from '../../context/useAuth';
import { agentPortalApi } from '../../services/agentPortalApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import {
  AgentAddGpTab,
  AgentRenewalsTab,
  AgentVillagesTab,
} from './AgentGpAndVillagesTabs';
import {
  AgentChangePasswordTab,
  AgentCommissionsTab,
  AgentPayoutsTab,
  AgentProfileTab,
} from './AgentPayoutsAndAccountTabs';

type AgentTab =
  | 'overview'
  | 'addGp'
  | 'villages'
  | 'renewals'
  | 'commissions'
  | 'payouts'
  | 'profile'
  | 'password';

export function AgentWorkspacePage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const [activeTab, setActiveTab] = useState<AgentTab>('overview');
  const [copiedType, setCopiedType] = useState<'onboarding' | 'register' | 'code' | null>(null);

  const isFieldAgent = Boolean(accessToken && user?.role === 'Field_Agent');

  const dashboardQuery = useQuery({
    queryKey: ['agent', 'dashboard', user?.userId],
    queryFn: agentPortalApi.getDashboard,
    enabled: isFieldAgent,
  });

  const profileQuery = useQuery({
    queryKey: ['agent', 'me', user?.userId],
    queryFn: agentPortalApi.getProfile,
    enabled: isFieldAgent,
  });

  const villagesQuery = useQuery({
    queryKey: ['agent', 'villages', user?.userId],
    queryFn: agentPortalApi.getVillages,
    enabled: isFieldAgent,
  });

  if (!isFieldAgent) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-amber-500/30 bg-slate-900 p-6 text-xs text-amber-100 space-y-3 shadow-xl">
        <p className="font-bold text-sm text-amber-300">{t('agent.workspaceTitle')}</p>
        <p className="text-slate-300">{t('agent.workspaceLoginRequired')}</p>
        <Link
          to="/agent/login"
          className="inline-block rounded-xl bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500"
        >
          {t('agent.goToLogin')}
        </Link>
      </div>
    );
  }

  const dash = dashboardQuery.data;
  const profile = profileQuery.data;
  const agentCode = dash?.agentCode || profile?.agentCode || '';
  const origin = typeof window !== 'undefined' ? window.location.origin : '';
  const onboardingReferralUrl = `${origin}/onboarding?ref=${encodeURIComponent(agentCode)}`;
  const registerReferralUrl = `${origin}/register?ref=${encodeURIComponent(agentCode)}`;

  async function copyText(type: 'onboarding' | 'register' | 'code', value: string) {
    if (!value) return;
    try {
      await navigator.clipboard.writeText(value);
      setCopiedType(type);
      window.setTimeout(() => setCopiedType(null), 2000);
    } catch {
      // Fallback: clipboard unavailable
    }
  }

  const tabs: {
    id: AgentTab;
    label: string;
    icon: typeof LayoutDashboard;
    badge?: number;
  }[] = [
    { id: 'overview', label: t('agentPortal.tabOverview'), icon: LayoutDashboard },
    { id: 'addGp', label: t('agentPortal.tabAddGp'), icon: Plus },
    {
      id: 'villages',
      label: t('agentPortal.tabMyVillages'),
      icon: Building2,
      badge: dash?.villagesOnboarded,
    },
    {
      id: 'renewals',
      label: t('agentPortal.tabRenewals'),
      icon: CalendarClock,
      badge: dash?.upcomingRenewals90dCount,
    },
    { id: 'commissions', label: t('agentPortal.tabCommissions'), icon: FileSpreadsheet },
    { id: 'payouts', label: t('agentPortal.tabPayouts'), icon: Wallet },
    { id: 'profile', label: t('agentPortal.tabProfile'), icon: User },
    { id: 'password', label: t('agentPortal.tabChangePassword'), icon: KeyRound },
  ];

  const isLoadingCore = dashboardQuery.isLoading || profileQuery.isLoading;

  return (
    <div className="space-y-6">
      {/* Header & Verified Referral Strip */}
      <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-xl space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2.5">
              <ShieldCheck className="h-5 w-5 text-emerald-400" />
              <h1 className="text-xl font-extrabold text-white">{t('agent.workspaceTitle')}</h1>
              {agentCode && (
                <span className="rounded-full border border-emerald-500/40 bg-emerald-500/15 px-3 py-0.5 mono text-xs font-bold text-emerald-300">
                  {agentCode}
                </span>
              )}
              {profile && <KycStatusBadge status={profile.kycStatus} dark />}
            </div>
            <p className="text-xs text-slate-400">{t('agentPortal.headerSubtitle')}</p>
          </div>

          <button
            type="button"
            onClick={() => setActiveTab('addGp')}
            className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-600 px-4 py-2 text-xs font-extrabold text-white hover:bg-emerald-500 transition shadow"
          >
            <Plus className="h-4 w-4" />
            {t('agentPortal.tabAddGp')}
          </button>
        </div>

        {agentCode && (
          <div className="rounded-xl border border-emerald-500/30 bg-emerald-950/30 p-3.5 flex flex-wrap items-center justify-between gap-3 text-xs">
            <div className="space-y-1 min-w-[240px]">
              <p className="font-bold text-emerald-300">{t('agentPortal.referralStripLabel')}</p>
              <p className="mono text-[11px] text-slate-300 break-all">{onboardingReferralUrl}</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => void copyText('onboarding', onboardingReferralUrl)}
                className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-500/40 bg-emerald-500/15 px-3 py-1.5 font-bold text-emerald-200 hover:bg-emerald-500/25"
              >
                {copiedType === 'onboarding' ? (
                  <Check className="h-3.5 w-3.5" />
                ) : (
                  <Copy className="h-3.5 w-3.5" />
                )}
                {copiedType === 'onboarding'
                  ? t('agentPortal.copiedToast')
                  : t('agentPortal.copyOnboardingLinkBtn')}
              </button>
              <button
                type="button"
                onClick={() => void copyText('register', registerReferralUrl)}
                className="inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-3 py-1.5 font-bold text-slate-200 hover:bg-slate-700"
              >
                {copiedType === 'register' ? (
                  <Check className="h-3.5 w-3.5" />
                ) : (
                  <Copy className="h-3.5 w-3.5" />
                )}
                {copiedType === 'register'
                  ? t('agentPortal.copiedToast')
                  : t('agentPortal.copyRegisterLinkBtn')}
              </button>
              <button
                type="button"
                onClick={() => void copyText('code', agentCode)}
                className="inline-flex items-center gap-1.5 rounded-lg border border-amber-500/40 bg-amber-500/15 px-3 py-1.5 mono font-bold text-amber-300 hover:bg-amber-500/25"
              >
                {copiedType === 'code' ? (
                  <Check className="h-3.5 w-3.5" />
                ) : (
                  <Copy className="h-3.5 w-3.5" />
                )}
                {copiedType === 'code' ? t('agentPortal.copiedToast') : t('agentPortal.copyCodeBtn')}
              </button>
            </div>
          </div>
        )}
      </section>

      {(dashboardQuery.isError || profileQuery.isError) && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-rose-500/40 bg-rose-950/60 p-4 text-xs text-rose-200">
          <span className="inline-flex items-center gap-2 font-semibold">
            <ShieldAlert className="h-4 w-4 shrink-0" />
            {t('agent.loadError')}
          </span>
          <button
            type="button"
            onClick={() => {
              void dashboardQuery.refetch();
              void profileQuery.refetch();
            }}
            className="inline-flex items-center gap-1 rounded-lg bg-rose-700 px-3 py-1.5 font-bold text-white hover:bg-rose-600"
          >
            <RefreshCw className="h-3.5 w-3.5" />
            Retry
          </button>
        </div>
      )}

      {/* 8 KPI Cards */}
      {isLoadingCore ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {[1, 2, 3, 4, 5, 6, 7, 8].map((n) => (
            <div key={n} className="h-24 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      ) : (
        dash && (
          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 text-xs">
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">
                {t('agentPortal.kpiVillagesOnboarded')}
              </p>
              <p className="mono text-2xl font-extrabold text-white mt-1">
                {dash.villagesOnboarded}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">{t('agentPortal.kpiActiveGps')}</p>
              <p className="mono text-2xl font-extrabold text-emerald-400 mt-1">
                {dash.activeGps}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">{t('agentPortal.kpiPendingGps')}</p>
              <p className="mono text-2xl font-extrabold text-amber-300 mt-1">
                {dash.pendingVerificationGps}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">{t('agentPortal.kpiTotalSales')}</p>
              <p className="mono text-2xl font-extrabold text-sky-400 mt-1">
                {formatInr(dash.totalSalesValue)}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">{t('agentPortal.kpiTotalCommission')}</p>
              <p className="mono text-2xl font-extrabold text-emerald-400 mt-1">
                {formatInr(dash.totalCommissionEarned)}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">
                {t('agentPortal.kpiPayableCommission')}
              </p>
              <p className="mono text-2xl font-extrabold text-amber-400 mt-1">
                {formatInr(dash.pendingPayableCommission)}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">{t('agentPortal.kpiPaidCommission')}</p>
              <p className="mono text-2xl font-extrabold text-teal-300 mt-1">
                {formatInr(dash.paidCommission)}
              </p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <p className="font-semibold text-slate-400">
                {t('agentPortal.kpiUpcomingRenewals')}
              </p>
              <p className="mono text-2xl font-extrabold text-indigo-300 mt-1">
                {dash.upcomingRenewals90dCount}
              </p>
            </div>
          </section>
        )
      )}

      {/* 8 Navigation Tabs */}
      <nav className="flex flex-wrap gap-2 border-b border-slate-800 pb-3 no-print" role="tablist">
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
              className={`inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-bold transition ${
                active
                  ? 'border-emerald-500/50 bg-emerald-500/15 text-emerald-300'
                  : 'border-slate-800 bg-slate-900 text-slate-400 hover:border-slate-700 hover:text-slate-200'
              }`}
            >
              <Icon className="h-3.5 w-3.5" />
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

      {/* Active Tab Body */}
      {activeTab === 'overview' && dash && profile && (
        <div className="space-y-6 text-xs">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="rounded-xl border border-slate-800 bg-slate-900 p-5 space-y-2">
              <p className="font-bold text-emerald-400 flex items-center gap-1.5">
                <Award className="h-4 w-4" />
                {t('agentPortal.commissionSlabsTitle')}
              </p>
              <div className="flex flex-wrap gap-2 pt-1">
                <span className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-1.5 mono font-bold text-emerald-300">
                  {t('agentPortal.newGpRatePill', { rate: dash.approvedNewGpRatePct.toFixed(1) })}
                </span>
                <span className="rounded-lg border border-sky-500/30 bg-sky-500/10 px-3 py-1.5 mono font-bold text-sky-300">
                  {t('agentPortal.renewalRatePill', {
                    rate: dash.approvedRenewalRatePct.toFixed(1),
                  })}
                </span>
              </div>
            </div>

            <div className="rounded-xl border border-slate-800 bg-slate-900 p-5 space-y-1.5">
              <p className="font-bold text-amber-300 flex items-center gap-1.5">
                <MapPin className="h-4 w-4" />
                {t('agent.territoryTitle')}
              </p>
              <p className="text-sm font-extrabold text-white">{profile.district}</p>
              <p className="text-slate-400">{profile.talukasCsv}</p>
            </div>

            <div className="rounded-xl border border-slate-800 bg-slate-900 p-5 flex flex-col justify-between gap-2">
              <p className="font-bold text-slate-300">{t('agentPortal.availableBalanceLabel')}</p>
              <p className="mono text-xl font-extrabold text-amber-300">
                {formatInr(dash.pendingPayableCommission)}
              </p>
              <button
                type="button"
                onClick={() => setActiveTab('payouts')}
                className="self-start rounded-lg bg-emerald-600 px-3 py-1.5 font-bold text-white hover:bg-emerald-500"
              >
                {t('agentPortal.tabPayouts')}
              </button>
            </div>
          </div>

          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 className="text-sm font-extrabold text-white">
                {t('agentPortal.recentCommissionsTitle')}
              </h2>
              <button
                type="button"
                onClick={() => setActiveTab('commissions')}
                className="text-xs font-bold text-emerald-400 hover:underline"
              >
                {t('agentPortal.tabCommissions')} →
              </button>
            </div>

            {dash.recentCommissions.length === 0 ? (
              <p className="rounded-xl border border-dashed border-slate-800 p-6 text-center text-slate-400">
                {t('agentPortal.noCommissionsYet')}
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left">
                  <thead className="border-b border-slate-800 text-slate-400">
                    <tr>
                      <th className="py-2 pr-4">{t('agentPortal.colDate')}</th>
                      <th className="py-2 pr-4">{t('agentPortal.colGpName')}</th>
                      <th className="py-2 pr-4">{t('agentPortal.colType')}</th>
                      <th className="py-2 pr-4 text-right">{t('agentPortal.colRateSnapshot')}</th>
                      <th className="py-2 pr-4 text-right">{t('agentPortal.colAccrued')}</th>
                      <th className="py-2 text-right">{t('agentPortal.colStatus')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/70">
                    {dash.recentCommissions.map((c) => (
                      <tr key={c.commissionId}>
                        <td className="py-2.5 pr-4 mono text-slate-400">
                          {formatDateOnly(c.createdAtUtc, i18n.language)}
                        </td>
                        <td className="py-2.5 pr-4 font-bold text-white">
                          {c.panchayatNameMr} <span className="mono text-amber-300">({c.lgdCode})</span>
                        </td>
                        <td className="py-2.5 pr-4 text-slate-300">{c.commissionType}</td>
                        <td className="py-2.5 pr-4 text-right mono text-sky-300">
                          {c.ratePctSnapshot}%
                        </td>
                        <td className="py-2.5 pr-4 text-right mono font-bold text-emerald-400">
                          {formatInr(c.commissionAmount)}
                        </td>
                        <td className="py-2.5 text-right font-bold text-slate-300">{c.status}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {activeTab === 'addGp' && (
        <AgentAddGpTab
          approvedNewGpRatePct={dash?.approvedNewGpRatePct ?? profile?.approvedNewGpRatePct ?? 15}
          onSuccessJumpToVillages={() => setActiveTab('villages')}
        />
      )}

      {activeTab === 'villages' && (
        <AgentVillagesTab
          villages={villagesQuery.data?.villages ?? []}
          isLoading={villagesQuery.isLoading}
          onJumpToAddGp={() => setActiveTab('addGp')}
        />
      )}

      {activeTab === 'renewals' && (
        <AgentRenewalsTab
          upcomingRenewals={villagesQuery.data?.upcomingRenewals ?? []}
          approvedRenewalRatePct={
            dash?.approvedRenewalRatePct ?? profile?.approvedRenewalRatePct ?? 10
          }
          isLoading={villagesQuery.isLoading}
        />
      )}

      {activeTab === 'commissions' && <AgentCommissionsTab />}

      {activeTab === 'payouts' && (
        <AgentPayoutsTab
          pendingPayableCommission={dash?.pendingPayableCommission ?? 0}
          paidCommission={dash?.paidCommission ?? 0}
        />
      )}

      {activeTab === 'profile' && profile && <AgentProfileTab profile={profile} />}

      {activeTab === 'password' && <AgentChangePasswordTab />}
    </div>
  );
}
