import { CalendarCheck2, Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useOnboardingWizard } from '../context/useOnboardingWizard';

const YEAR_OPTIONS = [
  { label: '2025-26', start: 2025, end: 2026 },
  { label: '2026-27', start: 2026, end: 2027 },
  { label: '2027-28', start: 2027, end: 2028 },
];

export function Step3AssessmentYear() {
  const { t } = useTranslation();
  const { state, updateField } = useOnboardingWizard();

  return (
    <div className="space-y-5">
      <div className="rounded-xl border border-brand-200 bg-brand-50/70 p-4 flex items-start gap-3">
        <CalendarCheck2 className="h-5 w-5 text-brand-700 shrink-0 mt-0.5" />
        <div>
          <p className="text-sm font-bold text-brand-900">{t('onboarding.ayTitle')}</p>
          <p className="text-xs text-brand-800 mt-0.5">{t('onboarding.ayHint')}</p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block space-y-1">
          <span className="text-xs font-bold text-slate-700">{t('onboarding.ayCode')}</span>
          <select
            value={state.assessmentYearLabel}
            onChange={(e) => {
              const found = YEAR_OPTIONS.find((y) => y.label === e.target.value);
              if (found) {
                updateField('assessmentYearLabel', found.label);
                updateField('assessmentStartYear', found.start);
                updateField('assessmentEndYear', found.end);
              }
            }}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 mono text-sm font-semibold"
          >
            {YEAR_OPTIONS.map((y) => (
              <option key={y.label} value={y.label}>
                {t('onboarding.ayRange', { label: y.label, start: y.start, end: y.end })}
              </option>
            ))}
          </select>
        </label>

        <div className="rounded-xl border border-line bg-surface p-3.5 text-xs space-y-1">
          <p className="font-bold text-slate-700">{t('onboarding.fyCycle')}</p>
          <p className="mono text-sm font-bold text-ink">
            01-04-{state.assessmentStartYear} → 31-03-{state.assessmentEndYear}
          </p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex items-start gap-3 rounded-xl border border-line bg-white p-4 cursor-pointer hover:border-brand-300 transition">
          <input
            type="checkbox"
            checked={state.lockAssessmentYear}
            onChange={(e) => updateField('lockAssessmentYear', e.target.checked)}
            className="mt-1 h-4 w-4 rounded border-slate-300 text-brand-700"
          />
          <div>
            <p className="inline-flex items-center gap-1.5 text-sm font-bold text-ink">
              <Lock className="h-3.5 w-3.5 text-brand-700" />
              {t('onboarding.lockAy')}
            </p>
            <p className="text-xs text-muted mt-0.5">{t('onboarding.lockAyDesc')}</p>
          </div>
        </label>

        <label className="flex items-start gap-3 rounded-xl border border-line bg-white p-4 cursor-pointer hover:border-brand-300 transition">
          <input
            type="checkbox"
            checked={state.initializeArrearsRollover}
            onChange={(e) => updateField('initializeArrearsRollover', e.target.checked)}
            className="mt-1 h-4 w-4 rounded border-slate-300 text-brand-700"
          />
          <div>
            <p className="text-sm font-bold text-ink">{t('onboarding.arrearsRollover')}</p>
            <p className="text-xs text-muted mt-0.5">{t('onboarding.arrearsRolloverDesc')}</p>
          </div>
        </label>
      </div>
    </div>
  );
}
