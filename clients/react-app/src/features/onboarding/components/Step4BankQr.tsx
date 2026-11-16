import { Landmark, QrCode, ShieldAlert, Upload } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ProtectedAssetPreview } from '../../../components/ProtectedAssetPreview';
import { QrMatrixSvg } from '../../../components/QrMatrixSvg';
import { useAuth } from '../../../context/useAuth';
import { sprint2Api } from '../../../services/sprint2Api';
import { useOnboardingWizard } from '../context/useOnboardingWizard';

const FUND_LABEL_KEYS: Record<string, string> = {
  GRAM_NIDHI: 'onboarding.fundGramNidhi',
  XV_FINANCE: 'onboarding.fundXvFinance',
  WATER_SUPPLY: 'onboarding.fundWaterSupply',
};

export function Step4BankQr() {
  const { t } = useTranslation();
  const { accessToken } = useAuth();
  const { state, fieldErrors, updateField, updateBankAccount } = useOnboardingWizard();
  const [uploadingIdx, setUploadingIdx] = useState<number | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [localQrUrls, setLocalQrUrls] = useState<Record<number, string>>({});

  const upiUri = useMemo(() => {
    const vpa = encodeURIComponent(state.primaryUpiId.trim() || 'gp@sbi');
    const payee = encodeURIComponent(
      state.upiPayeeName.trim() || state.panchayatNameEn || 'Gram Panchayat',
    );
    return `upi://pay?pa=${vpa}&pn=${payee}&cu=INR&tn=GP-Tax`;
  }, [state.primaryUpiId, state.upiPayeeName, state.panchayatNameEn]);

  async function handleQrUpload(index: number, file: File | null) {
    if (!file) return;
    setUploadError(null);
    if (file.size > 2 * 1024 * 1024) {
      setUploadError(t('onboarding.uploadFailed'));
      return;
    }

    const preview = URL.createObjectURL(file);
    setLocalQrUrls((prev) => ({ ...prev, [index]: preview }));

    if (!accessToken) {
      updateBankAccount(index, { qrImageKey: `pending-qr-${file.name}` });
      return;
    }

    setUploadingIdx(index);
    try {
      const uploaded = await sprint2Api.uploadProtectedAsset('QR', file);
      updateBankAccount(index, { qrImageKey: uploaded.assetKey });
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : t('onboarding.uploadFailed'));
    } finally {
      setUploadingIdx(null);
    }
  }

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-slate-200 bg-surface px-4 py-2.5 text-xs text-muted">
        {t('validation.bankPartialHint')}
      </div>

      {uploadError && (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-xs font-medium text-rose-800"
        >
          <ShieldAlert className="h-4 w-4 shrink-0" />
          <span>{uploadError}</span>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        {state.bankAccounts.map((acc, idx) => {
          const bankErr = fieldErrors[`bankAccounts.${idx}.bankName`];
          const branchErr = fieldErrors[`bankAccounts.${idx}.branchName`];
          const accountErr = fieldErrors[`bankAccounts.${idx}.accountNumber`];
          const ifscErr = fieldErrors[`bankAccounts.${idx}.ifscCode`];
          return (
            <div
              key={acc.fundType}
              className="rounded-xl border border-line bg-white p-4 space-y-3 shadow-xs"
            >
              <div className="flex items-center gap-2 border-b border-slate-100 pb-2 text-brand-800">
                <Landmark className="h-4 w-4" />
                <span className="text-xs font-extrabold">
                  {t(FUND_LABEL_KEYS[acc.fundType] || acc.fundType)}
                </span>
              </div>

              <label className="block space-y-1 text-[11px] font-bold text-slate-700">
                <span>{t('onboarding.bankName')}</span>
                <input
                  type="text"
                  value={acc.bankName}
                  onChange={(e) => updateBankAccount(idx, { bankName: e.target.value })}
                  className={`w-full rounded-lg border px-2.5 py-1.5 text-xs font-normal ${
                    bankErr ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                  }`}
                />
                {bankErr && <span className="block text-[10.5px] text-rose-700">{bankErr}</span>}
              </label>

              <label className="block space-y-1 text-[11px] font-bold text-slate-700">
                <span>{t('onboarding.branchName')}</span>
                <input
                  type="text"
                  value={acc.branchName}
                  onChange={(e) => updateBankAccount(idx, { branchName: e.target.value })}
                  className={`w-full rounded-lg border px-2.5 py-1.5 text-xs font-normal ${
                    branchErr ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                  }`}
                />
                {branchErr && (
                  <span className="block text-[10.5px] text-rose-700">{branchErr}</span>
                )}
              </label>

              <label className="block space-y-1 text-[11px] font-bold text-slate-700">
                <span>{t('onboarding.accountNo')}</span>
                <input
                  type="text"
                  value={acc.accountNumber}
                  onChange={(e) => updateBankAccount(idx, { accountNumber: e.target.value })}
                  className={`w-full rounded-lg border px-2.5 py-1.5 mono text-xs font-normal ${
                    accountErr ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                  }`}
                />
                {accountErr && (
                  <span className="block text-[10.5px] text-rose-700">{accountErr}</span>
                )}
              </label>

              <div className="grid grid-cols-2 gap-2">
                <label className="block space-y-1 text-[11px] font-bold text-slate-700">
                  <span>IFSC</span>
                  <input
                    type="text"
                    maxLength={11}
                    placeholder="SBIN0001234"
                    value={acc.ifscCode}
                    onChange={(e) =>
                      updateBankAccount(idx, { ifscCode: e.target.value.toUpperCase() })
                    }
                    className={`w-full rounded-lg border px-2.5 py-1.5 mono text-xs font-normal uppercase ${
                      ifscErr ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
                    }`}
                  />
                  {ifscErr && (
                    <span className="block text-[10.5px] text-rose-700">{ifscErr}</span>
                  )}
                </label>

                <label className="block space-y-1 text-[11px] font-bold text-slate-700">
                  <span>{t('onboarding.openingBalance')}</span>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={acc.openingBalance}
                    onChange={(e) =>
                      updateBankAccount(idx, { openingBalance: Number(e.target.value) })
                    }
                    className="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 mono text-xs font-normal"
                  />
                </label>
              </div>

              <div>
                <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-surface px-2.5 py-2 text-[11px] hover:border-brand-600">
                  <span className="inline-flex items-center gap-1 font-semibold text-slate-700">
                    <Upload className="h-3.5 w-3.5 text-brand-700" />
                    {uploadingIdx === idx ? t('onboarding.loading') : t('onboarding.uploadQrImage')}
                  </span>
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    className="hidden"
                    onChange={(e) => void handleQrUpload(idx, e.target.files?.[0] ?? null)}
                  />
                </label>
                <ProtectedAssetPreview
                  assetKey={acc.qrImageKey ?? null}
                  localPreviewUrl={localQrUrls[idx]}
                  alt={t('onboarding.uploadQrImage')}
                />
              </div>
            </div>
          );
        })}
      </div>

      {/* Live NPCI Dynamic UPI QR Builder */}
      <div className="rounded-2xl border border-indigo-200 bg-indigo-50/60 p-5 grid gap-5 md:grid-cols-[1fr_auto] items-center">
        <div className="space-y-3">
          <div className="flex items-center gap-2 text-indigo-900">
            <QrCode className="h-4 w-4" />
            <span className="text-xs font-extrabold">{t('onboarding.upiConfigTitle')}</span>
          </div>
          <p className="text-[11px] text-indigo-800">{t('onboarding.upiValidHint')}</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="space-y-1 text-xs font-bold text-indigo-950">
              <span>{t('onboarding.upiIdLabel')}</span>
              <input
                type="text"
                placeholder={t('onboarding.upiIdPlaceholder')}
                value={state.primaryUpiId}
                onChange={(e) => updateField('primaryUpiId', e.target.value)}
                className={`w-full rounded-lg border bg-white px-3 py-2 mono text-xs font-bold ${
                  fieldErrors.primaryUpiId ? 'border-rose-500 ring-1 ring-rose-300' : 'border-indigo-200'
                }`}
              />
              {fieldErrors.primaryUpiId && (
                <span className="block text-[11px] font-semibold text-rose-700">
                  {fieldErrors.primaryUpiId}
                </span>
              )}
            </label>
            <label className="space-y-1 text-xs font-bold text-indigo-950">
              <span>{t('onboarding.upiPayee')}</span>
              <input
                type="text"
                placeholder={t('onboarding.upiPayee')}
                value={state.upiPayeeName}
                onChange={(e) => updateField('upiPayeeName', e.target.value)}
                className="w-full rounded-lg border border-indigo-200 bg-white px-3 py-2 text-xs font-medium"
              />
            </label>
          </div>
          <code className="block break-all rounded-lg border border-indigo-100 bg-white/90 px-3 py-2 mono text-[11px] text-indigo-950">
            {upiUri}
          </code>
        </div>

        <div className="flex flex-col items-center justify-center rounded-2xl border border-indigo-200 bg-white p-3.5 shadow-xs">
          <QrMatrixSvg value={upiUri} sizePx={124} label={t('onboarding.qrDynamicActive')} />
          <span className="mt-2 text-[10.5px] font-bold text-brand-700">
            {t('onboarding.qrDynamicActive')}
          </span>
        </div>
      </div>
    </div>
  );
}
