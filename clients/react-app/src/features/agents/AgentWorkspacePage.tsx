import { useQuery } from '@tanstack/react-query';
import { Award, Building2, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
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
      <div className="mx-auto max-w-md rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <p className="font-bold">{t('agent.workspaceLoginRequired')}</p>
        <Link to="/agent/login" className="inline-block rounded-lg bg-emerald-700 px-4 py-2 font-bold text-white">
          {t('agent.goToLogin')}
        </Link>
      </div>
    );
  }

  const profile = profileQuery.data;

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2 text-emerald-800">
            <ShieldCheck className="h-5 w-5" />
            <h1 className="text-xl font-bold text-slate-900">{t('agent.workspaceTitle')}</h1>
          </div>
          {profile && (
            <span className="rounded-full bg-emerald-100 px-3 py-1 font-mono text-xs font-bold text-emerald-800">
              {profile.agentCode} · {String(profile.kycStatus)}
            </span>
          )}
        </div>

        {profileQuery.isLoading && <div className="h-24 animate-pulse rounded-xl bg-slate-200" />}

        {profile && (
          <div className="grid gap-4 sm:grid-cols-3 text-xs">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 space-y-1">
              <p className="font-semibold text-slate-500">{t('agent.fullName')}</p>
              <p className="text-sm font-bold text-slate-900">{profile.fullName}</p>
              <p className="font-mono text-slate-600">{profile.district} · {profile.talukasCsv}</p>
            </div>
            <div className="rounded-xl border border-emerald-200 bg-emerald-50/70 p-4 space-y-1">
              <p className="flex items-center gap-1 font-semibold text-emerald-800">
                <Award className="h-4 w-4" />
                {t('agent.approvedRatesTitle')}
              </p>
              <p className="font-mono text-lg font-extrabold text-emerald-900 tabular-nums">
                {profile.approvedNewGpRatePct}% New / {profile.approvedRenewalRatePct}% Renewal
              </p>
            </div>
            <div className="rounded-xl border border-slate-200 bg-white p-4 flex flex-col justify-between">
              <p className="font-semibold text-slate-600">{t('agent.onboardReferralPrompt')}</p>
              <Link
                to="/onboarding"
                className="mt-2 inline-flex items-center justify-center gap-1.5 rounded-lg bg-emerald-700 px-4 py-2 font-bold text-white hover:bg-emerald-800"
              >
                <Building2 className="h-4 w-4" />
                {t('nav.onboarding')}
              </Link>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
