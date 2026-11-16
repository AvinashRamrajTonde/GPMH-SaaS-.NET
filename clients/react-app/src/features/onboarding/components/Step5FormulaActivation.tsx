import { CheckCircle2, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../context/useAuth';
import { useOnboardingWizard } from '../context/useOnboardingWizard';

export function Step5FormulaActivation() {
  const { t } = useTranslation();
  const { accessToken } = useAuth();
  const { state, editedFields, updateField } = useOnboardingWizard();

  const seedStats = [
    { label: t('onboarding.seedConstructions'), value: '12' },
    { label: t('onboarding.seedDepreciation'), value: '9' },
    { label: t('onboarding.seedWeights'), value: '1.0 / 1.2 / 1.25' },
    { label: t('onboarding.seedAreaSlabs'), value: '₹40 / ₹50 / ₹100' },
    { label: t('onboarding.seedDivisor'), value: '1000' },
  ];

  return (
    <div className="space-y-5">
      <div className="rounded-xl border border-emerald-200 bg-emerald-50/70 p-4 space-y-3">
        <div className="flex items-center gap-2 text-emerald-900">
          <Sparkles className="h-5 w-5" />
          <h3 className="text-sm font-bold">{t('onboarding.step5Title')}</h3>
        </div>
        <p className="text-xs text-emerald-800">{t('onboarding.step5Desc')}</p>

        <div className="grid gap-3 sm:grid-cols-5">
          {seedStats.map((item) => (
            <div key={item.label} className="rounded-lg border border-emerald-200 bg-white p-2.5 text-center">
              <p className="text-[11px] text-slate-500">{item.label}</p>
              <p className="mt-1 font-mono text-sm font-extrabold text-emerald-800">{item.value}</p>
            </div>
          ))}
        </div>

        <label className="inline-flex items-center gap-2 text-xs font-bold text-emerald-950 pt-1 cursor-pointer">
          <input
            type="checkbox"
            checked={state.seedDefaultFormulas}
            onChange={(e) => updateField('seedDefaultFormulas', e.target.checked)}
            className="h-4 w-4 rounded border-emerald-400 text-emerald-700"
          />
          {t('onboarding.seedCheckbox')}
        </label>
      </div>

      {!accessToken && (
        <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
          <p className="text-xs font-bold text-slate-800">{t('onboarding.adminAccountSection')}</p>
          <div className="grid gap-3 sm:grid-cols-3">
            <input
              type="text"
              placeholder={t('onboarding.adminUsername')}
              value={state.adminUsername}
              onChange={(e) => updateField('adminUsername', e.target.value)}
              className="rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
            />
            <input
              type="email"
              placeholder={t('onboarding.adminEmail')}
              value={state.adminEmail}
              onChange={(e) => updateField('adminEmail', e.target.value)}
              className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
            <input
              type="password"
              placeholder={t('onboarding.adminPassword')}
              value={state.adminPassword}
              onChange={(e) => updateField('adminPassword', e.target.value)}
              className="rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
            />
          </div>
        </div>
      )}

      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs space-y-2">
        <div className="flex items-center gap-2 font-bold text-slate-800">
          <CheckCircle2 className="h-4 w-4 text-emerald-700" />
          <span>{t('onboarding.provenanceSummary')}</span>
        </div>
        <p className="font-mono text-[11px] text-slate-600">
          LGD: {state.lgdCode || 'N/A'} | {state.panchayatNameMr || state.panchayatNameEn || '—'} | Edited Fields:{' '}
          {editedFields.length > 0 ? editedFields.join(', ') : 'None (Pure LGD Auto-Fill)'}
        </p>
      </div>
    </div>
  );
}
