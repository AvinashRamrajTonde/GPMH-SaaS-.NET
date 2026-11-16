import { ArrowLeft, ArrowRight, CheckCircle2, ShieldAlert } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
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
  const { accessToken, login } = useAuth();
  const { state, setStep, buildCompletePayload } = useOnboardingWizard();
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  async function handleCompleteSubmit() {
    setErrorMsg(null);
    setSubmitting(true);
    try {
      const payload = buildCompletePayload();
      if (accessToken) {
        await sprint2Api.completeOnboarding(payload);
      } else {
        const created = await sprint2Api.registerSelfServiceGp(
          state.adminUsername,
          state.adminPassword,
          state.adminEmail.trim() || null,
          payload,
        );
        await login(state.adminUsername.trim(), state.adminPassword, created.tenantId);
      }
      void navigate('/dashboard');
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

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-5">
        <div>
          <h1 className="text-xl font-bold text-slate-900">{t('onboarding.title')}</h1>
          <p className="mt-1 text-sm text-slate-600">{t('onboarding.subtitle')}</p>
        </div>

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
                onClick={() => setStep(s.num)}
                className={`flex flex-col items-center rounded-xl border-2 p-2.5 text-center transition ${
                  active
                    ? 'border-emerald-600 bg-emerald-50 text-emerald-900 shadow-xs'
                    : completed
                      ? 'border-emerald-200 bg-white text-emerald-700'
                      : 'border-slate-200 bg-slate-50 text-slate-600 hover:bg-white'
                }`}
              >
                <span className="font-mono text-xs font-bold">
                  {completed ? '✓' : s.num}
                </span>
                <span className="mt-0.5 text-[11px] font-semibold leading-tight">{t(s.key)}</span>
              </button>
            );
          })}
        </div>

        {errorMsg && (
          <div className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-800">
            <ShieldAlert className="h-4 w-4 shrink-0" />
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

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4">
          <button
            type="button"
            disabled={state.step <= 1}
            onClick={() => setStep(state.step - 1)}
            className="inline-flex items-center gap-1.5 rounded-xl border border-slate-300 px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-100 disabled:opacity-40"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('onboarding.prevBtn')}
          </button>

          <span className="font-mono text-xs font-semibold text-slate-500">
            {state.step} / 5
          </span>

          {state.step < 5 ? (
            <button
              type="button"
              onClick={() => setStep(state.step + 1)}
              className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-700 px-5 py-2 text-xs font-bold text-white hover:bg-emerald-800"
            >
              {t('onboarding.nextBtn')}
              <ArrowRight className="h-4 w-4" />
            </button>
          ) : (
            <button
              type="button"
              disabled={submitting}
              onClick={() => void handleCompleteSubmit()}
              className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-700 px-5 py-2.5 text-xs font-bold text-white hover:bg-emerald-800 disabled:opacity-60"
            >
              <CheckCircle2 className="h-4 w-4" />
              {submitting ? t('onboarding.loading') : t('onboarding.activateBtn')}
            </button>
          )}
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
