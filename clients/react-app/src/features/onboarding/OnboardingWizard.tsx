import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Check, CheckCircle2, ShieldAlert } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { sprint2Api } from '../../services/sprint2Api';
import { OnboardingProvider } from './context/OnboardingContext';
import { useOnboardingWizard } from './context/useOnboardingWizard';
import { Step1GeoProfile } from './components/Step1GeoProfile';
import { Step2Committee } from './components/Step2Committee';
import { Step3AssessmentYear } from './components/Step3AssessmentYear';
import { Step4BankQr } from './components/Step4BankQr';
import { Step5FormulaActivation } from './components/Step5FormulaActivation';

const STEP_KEYS = [
  { num: 1, key: 'onboarding.step1Nav' },
  { num: 2, key: 'onboarding.step2Nav' },
  { num: 3, key: 'onboarding.step3Nav' },
  { num: 4, key: 'onboarding.step4Nav' },
  { num: 5, key: 'onboarding.step5Nav' },
];

function OnboardingWizardInner() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { user, accessToken, login } = useAuth();
  const { state, setStep, hydrateFromStatus, buildCompletePayload } = useOnboardingWizard();
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [savedNotice, setSavedNotice] = useState(false);

  const canHydrate = Boolean(accessToken && user?.activeTenantId);
  const statusQuery = useQuery({
    queryKey: ['onboarding', 'status', user?.activeTenantId],
    queryFn: sprint2Api.getOnboardingStatus,
    enabled: canHydrate,
  });

  useEffect(() => {
    const rawStep = Number(searchParams.get('step'));
    if (rawStep >= 1 && rawStep <= 5 && rawStep !== state.step) {
      setStep(rawStep);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  useEffect(() => {
    if (statusQuery.data) {
      hydrateFromStatus(statusQuery.data);
    }
  }, [statusQuery.data, hydrateFromStatus]);

  function goToStep(next: number) {
    const clamped = Math.min(5, Math.max(1, next));
    setStep(clamped);
    setSearchParams({ step: String(clamped) }, { replace: true });
  }

  async function handleCompleteSubmit(redirectToDashboard: boolean) {
    setErrorMsg(null);
    setSavedNotice(false);

    if (!accessToken && state.adminPassword !== state.adminConfirmPassword) {
      setStep(5);
      setErrorMsg(t('onboarding.passwordMismatch'));
      return;
    }

    setSubmitting(true);
    try {
      const payload = buildCompletePayload();
      if (accessToken) {
        await sprint2Api.completeOnboarding(payload);
        await queryClient.invalidateQueries({ queryKey: ['onboarding', 'status', user?.activeTenantId] });
        await queryClient.invalidateQueries({ queryKey: ['dashboard', 'summary', user?.activeTenantId] });
      } else {
        const created = await sprint2Api.registerSelfServiceGp(
          state.adminUsername,
          state.adminPassword,
          state.adminEmail.trim() || null,
          payload,
        );
        await login(state.adminUsername.trim(), state.adminPassword, created.tenantId);
      }
      if (redirectToDashboard) {
        void navigate('/dashboard');
      } else {
        setSavedNotice(true);
      }
    } catch (err) {
      if (err instanceof ApiProblemError) {
        const firstFieldErr = err.problem.errors ? Object.values(err.problem.errors).flat()[0] : null;
        setErrorMsg(firstFieldErr || err.problem.detail || err.message);
      } else {
        setErrorMsg(err instanceof Error ? err.message : t('onboarding.submitFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  const progressPct = Math.round((state.step / 5) * 100);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="space-y-1.5">
        <div className="inline-flex items-center gap-2 rounded-full border border-brand-200 bg-brand-50 px-3 py-1 text-[11px] font-bold text-brand-800">
          {t('login.heroBadge')}
        </div>
        <h1 className="text-2xl font-extrabold text-ink">{t('onboarding.title')}</h1>
        <p className="text-xs sm:text-sm text-muted">{t('onboarding.subtitle')}</p>
      </div>

      {/* Stepper Card matching canonical 21_gp_onboarding_wizard */}
      <section className="rounded-2xl border border-line bg-card p-4 sm:p-5 shadow-xs space-y-4">
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-5" role="tablist">
          {STEP_KEYS.map((s) => {
            const active = state.step === s.num;
            const completed = state.step > s.num;
            return (
              <button
                key={s.num}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => goToStep(s.num)}
                className={`flex flex-col items-center rounded-xl border-2 px-2 py-2.5 text-center transition ${
                  active
                    ? 'border-brand-700 bg-brand-50 text-brand-900 shadow-xs'
                    : completed
                      ? 'border-emerald-300 bg-white text-emerald-800 hover:bg-emerald-50/40'
                      : 'border-slate-200 bg-surface text-slate-600 hover:bg-white'
                }`}
              >
                <span
                  className={`mb-1.5 flex h-7 w-7 items-center justify-center rounded-full border-2 mono text-xs font-extrabold ${
                    active
                      ? 'border-brand-700 bg-brand-700 text-white'
                      : completed
                        ? 'border-emerald-600 bg-emerald-600 text-white'
                        : 'border-slate-300 bg-white text-slate-600'
                  }`}
                >
                  {completed ? <Check className="h-3.5 w-3.5" /> : s.num}
                </span>
                <span className="text-[11px] font-bold leading-snug">{t(s.key)}</span>
              </button>
            );
          })}
        </div>

        <div className="h-2 w-full overflow-hidden rounded-full bg-slate-200">
          <div
            className="h-full rounded-full bg-gradient-to-r from-brand-700 to-brand-500 transition-all duration-300"
            style={{ width: `${progressPct}%` }}
          />
        </div>
      </section>

      {/* Main Step Card */}
      <section className="rounded-2xl border border-line bg-card p-6 sm:p-8 shadow-xs space-y-6">
        {statusQuery.isFetching && (
          <div className="rounded-xl border border-brand-200 bg-brand-50 px-4 py-2.5 text-xs font-semibold text-brand-800 animate-pulse">
            {t('onboarding.hydrating')}
          </div>
        )}

        {savedNotice && (
          <div className="flex items-center gap-2 rounded-xl border border-emerald-300 bg-emerald-50 px-4 py-3 text-xs font-bold text-emerald-900">
            <CheckCircle2 className="h-4 w-4 text-emerald-700" />
            <span>{t('onboarding.savedSuccess')}</span>
          </div>
        )}

        {errorMsg && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-800"
          >
            <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{errorMsg}</span>
          </div>
        )}

        <div>
          {state.step === 1 && <Step1GeoProfile />}
          {state.step === 2 && <Step2Committee />}
          {state.step === 3 && <Step3AssessmentYear />}
          {state.step === 4 && <Step4BankQr />}
          {state.step === 5 && <Step5FormulaActivation />}
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line pt-5">
          <button
            type="button"
            disabled={state.step <= 1}
            onClick={() => goToStep(state.step - 1)}
            className="inline-flex items-center gap-1.5 rounded-xl border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-100 disabled:opacity-40 transition"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('onboarding.prevBtn')}
          </button>

          <span className="mono text-xs font-bold text-muted">
            {t('onboarding.stepCounter', { current: state.step })}
          </span>

          <div className="flex flex-wrap items-center gap-2">
            {accessToken && state.step < 5 && (
              <button
                type="button"
                disabled={submitting}
                onClick={() => void handleCompleteSubmit(false)}
                className="inline-flex items-center gap-1.5 rounded-xl border border-brand-600 bg-brand-50 px-4 py-2 text-xs font-bold text-brand-800 hover:bg-brand-100 disabled:opacity-60 transition"
              >
                {submitting ? t('onboarding.loading') : t('onboarding.saveBtn')}
              </button>
            )}

            {state.step < 5 ? (
              <button
                type="button"
                onClick={() => goToStep(state.step + 1)}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-5 py-2 text-xs font-bold text-white hover:bg-brand-800 transition"
              >
                {t('onboarding.nextBtn')}
                <ArrowRight className="h-4 w-4" />
              </button>
            ) : (
              <button
                type="button"
                disabled={submitting}
                onClick={() => void handleCompleteSubmit(true)}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-5 py-2.5 text-xs font-bold text-white hover:bg-brand-800 disabled:opacity-60 transition shadow-xs"
              >
                <CheckCircle2 className="h-4 w-4" />
                {submitting ? t('onboarding.loading') : t('onboarding.activateBtn')}
              </button>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}

export function OnboardingWizard() {
  return (
    <OnboardingProvider>
      <OnboardingWizardInner />
    </OnboardingProvider>
  );
}
