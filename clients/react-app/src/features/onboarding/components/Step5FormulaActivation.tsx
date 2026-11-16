import { Check, CheckCircle2, Eye, EyeOff, Sparkles, X } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../context/useAuth';
import { evaluatePasswordStrength } from '../../../utils/validation';
import { useOnboardingWizard } from '../context/useOnboardingWizard';

export function Step5FormulaActivation() {
  const { t } = useTranslation();
  const { accessToken } = useAuth();
  const { state, fieldErrors, editedFields, updateField } = useOnboardingWizard();
  const [showPass, setShowPass] = useState(false);

  const seedStats = [
    { label: t('onboarding.seedConstructions'), value: '12' },
    { label: t('onboarding.seedDepreciation'), value: '9' },
    { label: t('onboarding.seedWeights'), value: '1.0 / 1.2 / 1.25' },
    { label: t('onboarding.seedAreaSlabs'), value: '₹40 / ₹50 / ₹100' },
    { label: t('onboarding.seedDivisor'), value: '1000' },
  ];

  const pwdStrength = evaluatePasswordStrength(state.adminPassword);
  const passwordMismatch =
    !accessToken &&
    state.adminConfirmPassword.length > 0 &&
    state.adminPassword !== state.adminConfirmPassword;

  const strengthRules = [
    { ok: pwdStrength.hasMinLen, label: t('validation.passwordRuleLength') },
    { ok: pwdStrength.hasUpper, label: t('validation.passwordRuleUpper') },
    { ok: pwdStrength.hasLower, label: t('validation.passwordRuleLower') },
    { ok: pwdStrength.hasDigit, label: t('validation.passwordRuleDigit') },
  ];

  return (
    <div className="space-y-5">
      <div className="rounded-xl border border-brand-200 bg-brand-50/70 p-4 space-y-3">
        <div className="flex items-center gap-2 text-brand-900">
          <Sparkles className="h-5 w-5" />
          <h3 className="text-sm font-extrabold">{t('onboarding.step5Title')}</h3>
        </div>
        <p className="text-xs text-brand-800">{t('onboarding.step5Desc')}</p>

        <div className="grid gap-3 sm:grid-cols-5">
          {seedStats.map((item) => (
            <div
              key={item.label}
              className="rounded-lg border border-brand-200 bg-white p-2.5 text-center"
            >
              <p className="text-[11px] text-muted">{item.label}</p>
              <p className="mt-1 mono text-sm font-extrabold text-brand-800">{item.value}</p>
            </div>
          ))}
        </div>

        <label className="inline-flex items-center gap-2 text-xs font-bold text-brand-900 pt-1 cursor-pointer">
          <input
            type="checkbox"
            checked={state.seedDefaultFormulas}
            onChange={(e) => updateField('seedDefaultFormulas', e.target.checked)}
            className="h-4 w-4 rounded border-brand-400 text-brand-700"
          />
          {t('onboarding.seedCheckbox')}
        </label>
      </div>

      {!accessToken && (
        <div className="rounded-xl border border-line bg-white p-4 space-y-4 shadow-xs">
          <p className="text-xs font-extrabold text-ink">{t('onboarding.adminAccountSection')}</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>
                {t('onboarding.adminUsername')} <span className="text-rose-600">*</span>
              </span>
              <input
                type="text"
                required
                aria-invalid={Boolean(fieldErrors.adminUsername)}
                value={state.adminUsername}
                onChange={(e) => updateField('adminUsername', e.target.value)}
                className={`w-full rounded-lg border px-3 py-2 mono text-sm ${
                  fieldErrors.adminUsername ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                }`}
              />
              {fieldErrors.adminUsername && (
                <span className="block text-[11px] font-semibold text-rose-700">
                  {fieldErrors.adminUsername}
                </span>
              )}
            </label>

            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.adminEmail')}</span>
              <input
                type="email"
                aria-invalid={Boolean(fieldErrors.adminEmail)}
                value={state.adminEmail}
                onChange={(e) => updateField('adminEmail', e.target.value)}
                className={`w-full rounded-lg border px-3 py-2 text-sm ${
                  fieldErrors.adminEmail ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                }`}
              />
              {fieldErrors.adminEmail && (
                <span className="block text-[11px] font-semibold text-rose-700">
                  {fieldErrors.adminEmail}
                </span>
              )}
            </label>

            <div className="space-y-1.5 text-xs font-semibold text-slate-700">
              <label htmlFor="onb-admin-pwd" className="block">
                {t('onboarding.adminPassword')} <span className="text-rose-600">*</span>
              </label>
              <div className="relative">
                <input
                  id="onb-admin-pwd"
                  type={showPass ? 'text' : 'password'}
                  required
                  aria-invalid={Boolean(fieldErrors.adminPassword)}
                  value={state.adminPassword}
                  onChange={(e) => updateField('adminPassword', e.target.value)}
                  className={`w-full rounded-lg border px-3 py-2 pr-9 mono text-sm ${
                    fieldErrors.adminPassword ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                  }`}
                />
                <button
                  type="button"
                  aria-label={showPass ? t('login.hidePassword') : t('login.showPassword')}
                  onClick={() => setShowPass((v) => !v)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-1 text-slate-500 hover:bg-slate-100"
                >
                  {showPass ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              <div className="flex flex-wrap gap-1.5 pt-1">
                {strengthRules.map((rule) => (
                  <span
                    key={rule.label}
                    className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10.5px] font-semibold ${
                      rule.ok
                        ? 'bg-emerald-100 text-emerald-800'
                        : 'bg-slate-100 text-slate-600'
                    }`}
                  >
                    {rule.ok ? <Check className="h-3 w-3" /> : <X className="h-3 w-3" />}
                    {rule.label}
                  </span>
                ))}
              </div>
              {fieldErrors.adminPassword && (
                <p className="text-[11px] font-semibold text-rose-700">
                  {fieldErrors.adminPassword}
                </p>
              )}
            </div>

            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>
                {t('onboarding.confirmPassword')} <span className="text-rose-600">*</span>
              </span>
              <input
                type={showPass ? 'text' : 'password'}
                required
                aria-invalid={passwordMismatch || Boolean(fieldErrors.adminConfirmPassword)}
                value={state.adminConfirmPassword}
                onChange={(e) => updateField('adminConfirmPassword', e.target.value)}
                className={`w-full rounded-lg border px-3 py-2 mono text-sm ${
                  passwordMismatch || fieldErrors.adminConfirmPassword
                    ? 'border-rose-500 bg-rose-50/40'
                    : 'border-slate-300'
                }`}
              />
              {(passwordMismatch || fieldErrors.adminConfirmPassword) && (
                <p className="text-[11px] font-semibold text-rose-700">
                  {fieldErrors.adminConfirmPassword || t('onboarding.passwordMismatch')}
                </p>
              )}
            </label>
          </div>
        </div>
      )}

      <div className="rounded-xl border border-line bg-surface p-4 text-xs space-y-1.5">
        <div className="flex items-center gap-2 font-bold text-ink">
          <CheckCircle2 className="h-4 w-4 text-brand-700" />
          <span>{t('onboarding.provenanceSummary')}</span>
        </div>
        <p className="mono text-[11px] text-slate-700">
          {t('onboarding.provenanceLgdLabel')}: {state.lgdCode || '—'} ·{' '}
          {state.panchayatNameMr || state.panchayatNameEn || '—'} ·{' '}
          {t('onboarding.provenanceEditedLabel')}:{' '}
          {editedFields.length > 0 ? editedFields.join(', ') : t('onboarding.provenancePureAuto')}
        </p>
      </div>
    </div>
  );
}
