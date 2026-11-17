import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Check, CheckCircle2, UserCheck, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import { agentPortalApi } from '../../services/agentPortalApi';
import { sprint2Api } from '../../services/sprint2Api';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { OnboardingProvider } from './context/OnboardingContext';
import { useOnboardingWizard } from './context/useOnboardingWizard';
import { Step1GeoProfile } from './components/Step1GeoProfile';
import { Step2Committee } from './components/Step2Committee';
import { Step3AssessmentYear } from './components/Step3AssessmentYear';
import { Step4BankQr } from './components/Step4BankQr';
import { Step5FormulaActivation } from './components/Step5FormulaActivation';
import {
  resolveStepForFieldKey,
  validateAllOnboardingSteps,
  validateOnboardingStep,
} from './validation';

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
  const {
    state,
    fieldErrors,
    setFieldErrors,
    setStep,
    hydrateFromStatus,
    buildCompletePayload,
  } = useOnboardingWizard();
  const [submitting, setSubmitting] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [validationItems, setValidationItems] = useState<ParsedValidationItem[]>([]);
  const [savedNotice, setSavedNotice] = useState(false);

  const isSelfService = !accessToken;
  const urlRefCode = (searchParams.get('ref') || '').trim();
  const [manualRefCode, setManualRefCode] = useState<string | null>(null);
  const referralInputValue = manualRefCode ?? urlRefCode;
  const [activeVerifyCode, setActiveVerifyCode] = useState<string>(urlRefCode);

  const referralQuery = useQuery({
    queryKey: ['agent', 'validate-referral', activeVerifyCode.trim().toUpperCase()],
    queryFn: () => agentPortalApi.validateReferralCode(activeVerifyCode),
    enabled: Boolean(isSelfService && activeVerifyCode.trim().length >= 3),
    staleTime: 60_000,
    retry: false,
  });

  const verifiedReferralCode =
    referralQuery.data?.valid && referralQuery.data.agentCode
      ? referralQuery.data.agentCode
      : null;

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

  function goToStep(next: number, validateBeforeAdvance = false) {
    const clamped = Math.min(5, Math.max(1, next));
    setSavedNotice(false);

    if (validateBeforeAdvance && clamped > state.step) {
      const stepErrs = validateOnboardingStep(state.step, state, isSelfService, t);
      const entries = Object.entries(stepErrs);
      if (entries.length > 0) {
        setFieldErrors(stepErrs);
        setSummaryError(t('validation.summaryTitle'));
        setValidationItems(
          entries.map(([k, message]) => ({ key: k, rawField: k, message })),
        );
        return;
      }
      setFieldErrors({});
      setSummaryError(null);
      setValidationItems([]);
    }

    setStep(clamped);
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('step', String(clamped));
    setSearchParams(nextParams, { replace: true });
  }

  async function handleCompleteSubmit(redirectToDashboard: boolean) {
    setSummaryError(null);
    setValidationItems([]);
    setSavedNotice(false);

    const check = validateAllOnboardingSteps(state, isSelfService, t);
    if (check.firstInvalidStep !== null) {
      setFieldErrors(check.fieldErrors);
      setSummaryError(t('validation.summaryTitle'));
      setValidationItems(
        Object.entries(check.fieldErrors).map(([k, message]) => ({
          key: k,
          rawField: k,
          message,
        })),
      );
      goToStep(check.firstInvalidStep, false);
      return;
    }

    setFieldErrors({});
    setSubmitting(true);
    try {
      const payload = buildCompletePayload();
      if (accessToken) {
        await sprint2Api.completeOnboarding(payload);
        await queryClient.invalidateQueries({ queryKey: ['onboarding', 'status', user?.activeTenantId] });
        await queryClient.invalidateQueries({ queryKey: ['dashboard', 'summary', user?.activeTenantId] });
      } else {
        const created = await sprint2Api.registerSelfServiceGp(
          state.adminUsername.trim(),
          state.adminPassword,
          state.adminEmail.trim() || null,
          payload,
          verifiedReferralCode,
        );
        await login(state.adminUsername.trim(), state.adminPassword, created.tenantId);
      }
      if (redirectToDashboard) {
        void navigate('/dashboard');
      } else {
        setSavedNotice(true);
      }
    } catch (err) {
      const parsed = extractProblemValidation(err, t('onboarding.submitFailed'));
      setSummaryError(parsed.summary);
      setValidationItems(parsed.items);
      if (Object.keys(parsed.fieldErrors).length > 0) {
        setFieldErrors(parsed.fieldErrors);
        const targetStep = Math.min(
          ...Object.keys(parsed.fieldErrors).map((k) => resolveStepForFieldKey(k)),
        );
        if (targetStep >= 1 && targetStep <= 5) {
          goToStep(targetStep, false);
        }
      }
    } finally {
      setSubmitting(false);
    }
  }

  const stepHasErrors = useMemo(() => {
    const flags: Record<number, boolean> = {};
    for (const key of Object.keys(fieldErrors)) {
      flags[resolveStepForFieldKey(key)] = true;
    }
    return flags;
  }, [fieldErrors]);

  const showDuplicateHint = useMemo(() => {
    if (!isSelfService) return false;
    return validationItems.some(
      (i) =>
        i.message.toLowerCase().includes('already registered') ||
        i.message.toLowerCase().includes('already taken'),
    );
  }, [isSelfService, validationItems]);

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

      {isSelfService && (
        <section className="rounded-2xl border border-line bg-card p-4 shadow-xs space-y-2.5 text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="inline-flex items-center gap-1.5 font-extrabold text-ink">
              <UserCheck className="h-4 w-4 text-emerald-700" />
              {t('onboarding.referralBannerTitle')}
            </span>

            <div className="flex flex-wrap items-center gap-2">
              <input
                type="text"
                aria-label={t('onboarding.referralCodeInputLabel')}
                value={referralInputValue}
                onChange={(e) => setManualRefCode(e.target.value.toUpperCase())}
                placeholder={t('onboarding.referralCodePlaceholder')}
                className="w-40 rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 mono text-xs font-bold uppercase text-ink focus:border-brand-600 focus:outline-none"
              />
              <button
                type="button"
                disabled={referralQuery.isFetching || referralInputValue.trim().length < 3}
                onClick={() => {
                  const code = referralInputValue.trim();
                  setActiveVerifyCode(code);
                  const nextParams = new URLSearchParams(searchParams);
                  if (code) nextParams.set('ref', code);
                  setSearchParams(nextParams, { replace: true });
                }}
                className="rounded-lg bg-brand-700 px-3 py-1.5 font-bold text-white hover:bg-brand-800 disabled:opacity-50"
              >
                {referralQuery.isFetching
                  ? t('onboarding.referralVerifying')
                  : t('onboarding.referralVerifyBtn')}
              </button>
              {(referralInputValue || activeVerifyCode) && (
                <button
                  type="button"
                  onClick={() => {
                    setManualRefCode('');
                    setActiveVerifyCode('');
                    const nextParams = new URLSearchParams(searchParams);
                    nextParams.delete('ref');
                    setSearchParams(nextParams, { replace: true });
                  }}
                  className="inline-flex items-center gap-1 rounded-lg border border-slate-300 bg-surface px-2.5 py-1.5 font-semibold text-slate-700 hover:bg-slate-100"
                >
                  <X className="h-3.5 w-3.5" />
                  {t('onboarding.referralClearBtn')}
                </button>
              )}
            </div>
          </div>

          {verifiedReferralCode && referralQuery.data && (
            <div className="rounded-xl border border-emerald-300 bg-emerald-50 px-3.5 py-2 text-emerald-900 font-bold flex items-center gap-2">
              <CheckCircle2 className="h-4 w-4 text-emerald-700 shrink-0" />
              <span>
                {t('onboarding.referralAttributedTo', {
                  fullName: referralQuery.data.fullName ?? verifiedReferralCode,
                  code: referralQuery.data.agentCode,
                  district: referralQuery.data.district ?? 'Maharashtra',
                })}
              </span>
            </div>
          )}

          {activeVerifyCode &&
            !referralQuery.isFetching &&
            referralQuery.data &&
            !referralQuery.data.valid && (
              <div className="rounded-xl border border-rose-300 bg-rose-50 px-3.5 py-2 font-semibold text-rose-800">
                {t('onboarding.referralInvalidCode')}
              </div>
            )}
        </section>
      )}

      {/* Stepper Header Card */}
      <section className="rounded-2xl border border-line bg-card p-4 sm:p-5 shadow-xs space-y-4">
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-5" role="tablist">
          {STEP_KEYS.map((s) => {
            const active = state.step === s.num;
            const completed = state.step > s.num && !stepHasErrors[s.num];
            const hasErr = Boolean(stepHasErrors[s.num]);
            return (
              <button
                key={s.num}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => goToStep(s.num, false)}
                className={`relative flex flex-col items-center rounded-xl border-2 px-2 py-2.5 text-center transition ${
                  hasErr
                    ? 'border-rose-400 bg-rose-50/70 text-rose-900'
                    : active
                      ? 'border-brand-700 bg-brand-50 text-brand-900 shadow-xs'
                      : completed
                        ? 'border-emerald-300 bg-white text-emerald-800 hover:bg-emerald-50/40'
                        : 'border-slate-200 bg-surface text-slate-600 hover:bg-white'
                }`}
              >
                {hasErr && (
                  <span
                    className="absolute right-1.5 top-1.5 h-2 w-2 rounded-full bg-rose-600"
                    aria-hidden="true"
                  />
                )}
                <span
                  className={`mb-1.5 flex h-7 w-7 items-center justify-center rounded-full border-2 mono text-xs font-extrabold ${
                    hasErr
                      ? 'border-rose-600 bg-rose-600 text-white'
                      : active
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

      {/* Main Step Form Card */}
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

        <ValidationSummary
          summary={summaryError}
          items={validationItems}
          resolveStep={resolveStepForFieldKey}
          currentStep={state.step}
          onJumpToStep={(target) => goToStep(target, false)}
          showLoginHint={showDuplicateHint}
        />

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
            onClick={() => goToStep(state.step - 1, false)}
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
                onClick={() => goToStep(state.step + 1, true)}
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
