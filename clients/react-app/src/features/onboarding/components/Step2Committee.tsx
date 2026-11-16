import { ShieldAlert, Stamp, Upload } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ProtectedAssetPreview } from '../../../components/ProtectedAssetPreview';
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
  const { state, fieldErrors, updateField } = useOnboardingWizard();
  const [uploadingField, setUploadingField] = useState<AssetFieldKey | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [localPreviews, setLocalPreviews] = useState<Partial<Record<AssetFieldKey, string>>>({});

  async function handleFileUpload(
    fieldKey: AssetFieldKey,
    category: 'SIGNATURE' | 'STAMP',
    file: File | null,
  ) {
    if (!file) return;
    setUploadError(null);

    if (file.size > 2 * 1024 * 1024) {
      setUploadError(t('onboarding.uploadFailed'));
      return;
    }

    const previewUrl = URL.createObjectURL(file);
    setLocalPreviews((prev) => ({ ...prev, [fieldKey]: previewUrl }));

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
      <div className="rounded-xl border border-brand-200 bg-brand-50/70 p-4 text-xs text-brand-900">
        {t('onboarding.committeeHint')}
      </div>

      {uploadError && (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-xs font-medium text-rose-800"
        >
          <ShieldAlert className="h-4 w-4 shrink-0" />
          <span>{uploadError}</span>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        {/* Gramsevak / DDO */}
        <div className="rounded-xl border border-line bg-white p-4 space-y-3 shadow-xs">
          <h3 className="text-xs font-extrabold uppercase tracking-wide text-brand-800">
            {t('onboarding.gramsevakSection')}
          </h3>
          <label className="block space-y-1 text-xs font-semibold text-slate-700">
            <span>{t('onboarding.fullName')}</span>
            <input
              type="text"
              value={state.gramsevakName}
              onChange={(e) => updateField('gramsevakName', e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="block space-y-1 text-xs font-semibold text-slate-700">
            <span>{t('onboarding.mobile')}</span>
            <input
              type="tel"
              maxLength={10}
              value={state.gramsevakMobile}
              onChange={(e) => updateField('gramsevakMobile', e.target.value)}
              className={`w-full rounded-lg border px-3 py-2 mono text-sm ${
                fieldErrors.gramsevakMobile ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
              }`}
            />
            {fieldErrors.gramsevakMobile && (
              <span className="block text-[11px] font-semibold text-rose-700">
                {fieldErrors.gramsevakMobile}
              </span>
            )}
          </label>
          <div>
            <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-surface px-3 py-2.5 text-xs hover:border-brand-600">
              <span className="inline-flex items-center gap-1.5 font-semibold text-slate-700">
                <Upload className="h-3.5 w-3.5 text-brand-700" />
                {uploadingField === 'gramsevakSignatureKey'
                  ? t('onboarding.loading')
                  : t('onboarding.uploadSignature')}
              </span>
              <input
                type="file"
                accept="image/png,image/jpeg,image/webp"
                className="hidden"
                onChange={(e) =>
                  void handleFileUpload('gramsevakSignatureKey', 'SIGNATURE', e.target.files?.[0] ?? null)
                }
              />
            </label>
            <ProtectedAssetPreview
              assetKey={state.gramsevakSignatureKey}
              localPreviewUrl={localPreviews.gramsevakSignatureKey}
              alt={t('onboarding.uploadSignature')}
            />
          </div>
        </div>

        {/* Sarpanch & Up-Sarpanch */}
        <div className="rounded-xl border border-line bg-white p-4 space-y-3 shadow-xs">
          <h3 className="text-xs font-extrabold uppercase tracking-wide text-brand-800">
            {t('onboarding.sarpanchSection')}
          </h3>
          <label className="block space-y-1 text-xs font-semibold text-slate-700">
            <span>{t('onboarding.sarpanchName')}</span>
            <input
              type="text"
              value={state.sarpanchName}
              onChange={(e) => updateField('sarpanchName', e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="block space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.mobile')}</span>
              <input
                type="tel"
                maxLength={10}
                value={state.sarpanchMobile}
                onChange={(e) => updateField('sarpanchMobile', e.target.value)}
                className={`w-full rounded-lg border px-3 py-2 mono text-sm ${
                  fieldErrors.sarpanchMobile ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                }`}
              />
              {fieldErrors.sarpanchMobile && (
                <span className="block text-[11px] font-semibold text-rose-700">
                  {fieldErrors.sarpanchMobile}
                </span>
              )}
            </label>
            <label className="block space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.upSarpanchName')}</span>
              <input
                type="text"
                value={state.upSarpanchName}
                onChange={(e) => updateField('upSarpanchName', e.target.value)}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
              />
            </label>
          </div>
          <div>
            <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-surface px-3 py-2.5 text-xs hover:border-brand-600">
              <span className="inline-flex items-center gap-1.5 font-semibold text-slate-700">
                <Upload className="h-3.5 w-3.5 text-brand-700" />
                {uploadingField === 'sarpanchSignatureKey'
                  ? t('onboarding.loading')
                  : t('onboarding.uploadSarpanchSignature')}
              </span>
              <input
                type="file"
                accept="image/png,image/jpeg,image/webp"
                className="hidden"
                onChange={(e) =>
                  void handleFileUpload('sarpanchSignatureKey', 'SIGNATURE', e.target.files?.[0] ?? null)
                }
              />
            </label>
            <ProtectedAssetPreview
              assetKey={state.sarpanchSignatureKey}
              localPreviewUrl={localPreviews.sarpanchSignatureKey}
              alt={t('onboarding.uploadSarpanchSignature')}
            />
          </div>
        </div>

        {/* Clerk / DEO */}
        <div className="rounded-xl border border-line bg-white p-4 space-y-3 shadow-xs">
          <h3 className="text-xs font-extrabold uppercase tracking-wide text-brand-800">
            {t('onboarding.operatorSection')}
          </h3>
          <label className="block space-y-1 text-xs font-semibold text-slate-700">
            <span>{t('onboarding.fullName')}</span>
            <input
              type="text"
              value={state.operatorName}
              onChange={(e) => updateField('operatorName', e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="block space-y-1 text-xs font-semibold text-slate-700">
            <span>{t('onboarding.mobile')}</span>
            <input
              type="tel"
              maxLength={10}
              value={state.operatorMobile}
              onChange={(e) => updateField('operatorMobile', e.target.value)}
              className={`w-full rounded-lg border px-3 py-2 mono text-sm ${
                fieldErrors.operatorMobile ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
              }`}
            />
            {fieldErrors.operatorMobile && (
              <span className="block text-[11px] font-semibold text-rose-700">
                {fieldErrors.operatorMobile}
              </span>
            )}
          </label>
          <div>
            <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-surface px-3 py-2.5 text-xs hover:border-brand-600">
              <span className="inline-flex items-center gap-1.5 font-semibold text-slate-700">
                <Upload className="h-3.5 w-3.5 text-brand-700" />
                {uploadingField === 'operatorSignatureKey'
                  ? t('onboarding.loading')
                  : t('onboarding.uploadOperatorSignature')}
              </span>
              <input
                type="file"
                accept="image/png,image/jpeg,image/webp"
                className="hidden"
                onChange={(e) =>
                  void handleFileUpload('operatorSignatureKey', 'SIGNATURE', e.target.files?.[0] ?? null)
                }
              />
            </label>
            <ProtectedAssetPreview
              assetKey={state.operatorSignatureKey}
              localPreviewUrl={localPreviews.operatorSignatureKey}
              alt={t('onboarding.uploadOperatorSignature')}
            />
          </div>
        </div>

        {/* Official GP Stamp */}
        <div className="rounded-xl border border-line bg-white p-4 space-y-3 shadow-xs">
          <h3 className="flex items-center gap-1.5 text-xs font-extrabold uppercase tracking-wide text-brand-800">
            <Stamp className="h-4 w-4" />
            {t('onboarding.officialStamp')}
          </h3>
          <p className="text-xs text-muted">{t('onboarding.stampHint')}</p>
          <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-surface px-3 py-3 text-xs hover:border-brand-600">
            <span className="inline-flex items-center gap-1.5 font-semibold text-slate-700">
              <Upload className="h-4 w-4 text-brand-700" />
              {uploadingField === 'officialStampKey'
                ? t('onboarding.loading')
                : t('onboarding.uploadStamp')}
            </span>
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
              onChange={(e) =>
                void handleFileUpload('officialStampKey', 'STAMP', e.target.files?.[0] ?? null)
              }
            />
          </label>
          <ProtectedAssetPreview
            assetKey={state.officialStampKey}
            localPreviewUrl={localPreviews.officialStampKey}
            alt={t('onboarding.officialStamp')}
          />
        </div>
      </div>
    </div>
  );
}
