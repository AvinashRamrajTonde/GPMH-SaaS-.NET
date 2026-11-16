import { useQuery } from '@tanstack/react-query';
import { Award, Building2, MapPin, RefreshCw, ShieldAlert, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { KycStatusBadge } from '../../components/StatusBadge';
import { useAuth } from '../../context/useAuth';
import { sprint2Api } from '../../services/sprint2Api';

export function AgentWorkspacePage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();

  const isFieldAgent = Boolean(accessToken && user?.role === 'Field_Agent');

  const profileQuery = useQuery({
    queryKey: ['agent', 'me', user?.userId],
    queryFn: sprint2Api.getAgentProfile,
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

  const profile = profileQuery.data;

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6 shadow-xl space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-4">
          <div className="flex items-center gap-2.5 text-emerald-400">
            <ShieldCheck className="h-5 w-5" />
            <h1 className="text-xl font-extrabold text-white">{t('agent.workspaceTitle')}</h1>
          </div>
          {profile && (
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full border border-emerald-500/30 bg-emerald-500/15 px-3 py-1 mono text-xs font-bold text-emerald-300">
                {profile.agentCode}
              </span>
              <KycStatusBadge status={profile.kycStatus} dark />
            </div>
          )}
        </div>

        {profileQuery.isLoading && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[1, 2, 3, 4].map((n) => (
              <div key={n} className="h-28 animate-pulse rounded-xl bg-slate-800" />
            ))}
          </div>
        )}

        {profileQuery.isError && (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-rose-500/40 bg-rose-950/60 p-4 text-xs text-rose-200">
            <span className="inline-flex items-center gap-2 font-semibold">
              <ShieldAlert className="h-4 w-4 shrink-0" />
              {t('agent.loadError')}
            </span>
            <button
              type="button"
              onClick={() => void profileQuery.refetch()}
              className="inline-flex items-center gap-1 rounded-lg bg-rose-700 px-3 py-1.5 font-bold text-white hover:bg-rose-600"
            >
              <RefreshCw className="h-3.5 w-3.5" />
              Retry
            </button>
          </div>
        )}

        {profile && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 text-xs">
            <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-1.5">
              <p className="font-bold text-emerald-400 flex items-center gap-1.5">
                <Award className="h-4 w-4" />
                {t('agent.approvedRatesTitle')}
              </p>
              <div className="grid grid-cols-2 gap-2 pt-1">
                <div>
                  <p className="text-[11px] text-slate-400">{t('agent.newGpLabel')}</p>
                  <p className="mono text-xl font-extrabold text-emerald-300">
                    {profile.approvedNewGpRatePct}%
                  </p>
                </div>
                <div>
                  <p className="text-[11px] text-slate-400">{t('agent.renewalLabel')}</p>
                  <p className="mono text-xl font-extrabold text-sky-300">
                    {profile.approvedRenewalRatePct}%
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-1.5">
              <p className="font-bold text-slate-400 flex items-center gap-1.5">
                <MapPin className="h-4 w-4 text-amber-400" />
                {t('agent.territoryTitle')}
              </p>
              <p className="text-sm font-extrabold text-white">{profile.district}</p>
              <p className="text-slate-400">{profile.talukasCsv}</p>
            </div>

            <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-1.5">
              <p className="font-bold text-slate-400">{t('agent.kycIdentityTitle')}</p>
              <p className="text-sm font-bold text-white">{profile.fullName}</p>
              <p className="mono text-[11px] text-slate-300">
                {profile.aadhaarMasked} · {profile.panMasked}
              </p>
              <p className="mono text-[11px] text-slate-400">{profile.mobileNo}</p>
            </div>

            <div className="rounded-xl border border-emerald-500/30 bg-emerald-950/30 p-4 flex flex-col justify-between">
              <p className="font-semibold text-emerald-200">{t('agent.onboardReferralPrompt')}</p>
              <Link
                to="/onboarding"
                className="mt-3 inline-flex items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-4 py-2.5 font-bold text-white hover:from-emerald-500 hover:to-teal-500 transition"
              >
                <Building2 className="h-4 w-4" />
                {t('agent.launchOnboardingBtn')}
              </Link>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
