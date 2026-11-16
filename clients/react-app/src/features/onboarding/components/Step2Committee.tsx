import { CheckCircle2, Stamp, Upload } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../context/useAuth';
import { sprint2Api } from '../../../services/sprint2Api';
import { useOnboardingWizard } from '../context/useOnboardingWizard';

type AssetFieldKey =
  | 'gramsevakSignatureKey'
  | 'sarpanchSignatureKey'
  | 'operatorSignatureKey'
  | 'officialStampKey';

export function Step2Committee() {
  const { t } = useTranslation();
  const { accessToken } = useAuth();
  const { state, updateField } = useOnboardingWizard();
  const [uploadingField, setUploadingField] = useState<AssetFieldKey | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);

  async function handleFileUpload(
    fieldKey: AssetFieldKey,
    category: 'SIGNATURE' | 'STAMP',
    file: File | null,
  ) {
    if (!file) return;
    setUploadError(null);

    if (!accessToken) {
      updateField(fieldKey, `pending-local-${file.name}`);
      return;
    }

    setUploadingField(fieldKey);
    try {
      const uploaded = await sprint2Api.uploadProtectedAsset(category, file);
      updateField(fieldKey, uploaded.assetKey);
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : t('onboarding.uploadFailed'));
    } finally {
      setUploadingField(null);
    }
  }

  return (
    <div className="space-y-5">
      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs text-slate-600">
        {t('onboarding.committeeHint')}
      </div>

      {uploadError && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-800">
          {uploadError}
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
          <h3 className="text-xs font-bold uppercase tracking-wide text-emerald-800">
            {t('onboarding.gramsevakSection')}
          </h3>
          <input
            type="text"
            placeholder={t('onboarding.fullName')}
            value={state.gramsevakName}
            onChange={(e) => updateField('gramsevakName', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            type="tel"
            maxLength={10}
            placeholder={t('onboarding.mobile')}
            value={state.gramsevakMobile}
            onChange={(e) => updateField('gramsevakMobile', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
          />
          <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-slate-50 px-3 py-2 text-xs">
            <span className="inline-flex items-center gap-1.5 font-medium text-slate-700">
              <Upload className="h-3.5 w-3.5" />
              {uploadingField === 'gramsevakSignatureKey'
                ? t('onboarding.loading')
                : t('onboarding.uploadSignature')}
            </span>
            {state.gramsevakSignatureKey && (
              <CheckCircle2 className="h-4 w-4 text-emerald-700" />
            )}
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
              onChange={(e) =>
                void handleFileUpload('gramsevakSignatureKey', 'SIGNATURE', e.target.files?.[0] ?? null)
              }
            />
          </label>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
          <h3 className="text-xs font-bold uppercase tracking-wide text-emerald-800">
            {t('onboarding.sarpanchSection')}
          </h3>
          <input
            type="text"
            placeholder={t('onboarding.sarpanchName')}
            value={state.sarpanchName}
            onChange={(e) => updateField('sarpanchName', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            type="tel"
            maxLength={10}
            placeholder={t('onboarding.mobile')}
            value={state.sarpanchMobile}
            onChange={(e) => updateField('sarpanchMobile', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
          />
          <input
            type="text"
            placeholder={t('onboarding.upSarpanchName')}
            value={state.upSarpanchName}
            onChange={(e) => updateField('upSarpanchName', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
          <h3 className="text-xs font-bold uppercase tracking-wide text-emerald-800">
            {t('onboarding.operatorSection')}
          </h3>
          <input
            type="text"
            placeholder={t('onboarding.fullName')}
            value={state.operatorName}
            onChange={(e) => updateField('operatorName', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
          <input
            type="tel"
            maxLength={10}
            placeholder={t('onboarding.mobile')}
            value={state.operatorMobile}
            onChange={(e) => updateField('operatorMobile', e.target.value)}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
          />
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-3">
          <h3 className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wide text-emerald-800">
            <Stamp className="h-4 w-4" />
            {t('onboarding.officialStamp')}
          </h3>
          <p className="text-xs text-slate-500">{t('onboarding.stampHint')}</p>
          <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-slate-50 px-3 py-3 text-xs">
            <span className="inline-flex items-center gap-1.5 font-medium text-slate-700">
              <Upload className="h-4 w-4" />
              {uploadingField === 'officialStampKey'
                ? t('onboarding.loading')
                : t('onboarding.uploadStamp')}
            </span>
            {state.officialStampKey && <CheckCircle2 className="h-4 w-4 text-emerald-700" />}
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
              onChange={(e) =>
                void handleFileUpload('officialStampKey', 'STAMP', e.target.files?.[0] ?? null)
              }
            />
          </label>
        </div>
      </div>
    </div>
  );
}
