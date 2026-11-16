import { CheckCircle2, Landmark, QrCode, Upload } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { state, updateField, updateBankAccount } = useOnboardingWizard();
  const [uploadingIdx, setUploadingIdx] = useState<number | null>(null);

  const upiUri = useMemo(() => {
    const vpa = encodeURIComponent(state.primaryUpiId.trim() || 'gp@sbi');
    const payee = encodeURIComponent(state.upiPayeeName.trim() || state.panchayatNameEn || 'Gram Panchayat');
    return `upi://pay?pa=${vpa}&pn=${payee}&cu=INR&tn=Grampanchayat+Tax`;
  }, [state.primaryUpiId, state.upiPayeeName, state.panchayatNameEn]);

  async function handleQrUpload(index: number, file: File | null) {
    if (!file) return;
    if (!accessToken) {
      updateBankAccount(index, { qrImageKey: `pending-qr-${file.name}` });
      return;
    }
    setUploadingIdx(index);
    try {
      const uploaded = await sprint2Api.uploadProtectedAsset('QR', file);
      updateBankAccount(index, { qrImageKey: uploaded.assetKey });
    } finally {
      setUploadingIdx(null);
    }
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-4 lg:grid-cols-3">
        {state.bankAccounts.map((acc, idx) => (
          <div key={acc.fundType} className="rounded-xl border border-slate-200 bg-white p-4 space-y-2.5 shadow-xs">
            <div className="flex items-center gap-2 border-b border-slate-100 pb-2 text-emerald-800">
              <Landmark className="h-4 w-4" />
              <span className="text-xs font-extrabold">{t(FUND_LABEL_KEYS[acc.fundType] || acc.fundType)}</span>
            </div>

            <input
              type="text"
              placeholder={t('onboarding.bankName')}
              value={acc.bankName}
              onChange={(e) => updateBankAccount(idx, { bankName: e.target.value })}
              className="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs"
            />
            <input
              type="text"
              placeholder={t('onboarding.branchName')}
              value={acc.branchName}
              onChange={(e) => updateBankAccount(idx, { branchName: e.target.value })}
              className="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs"
            />
            <input
              type="text"
              placeholder={t('onboarding.accountNo')}
              value={acc.accountNumber}
              onChange={(e) => updateBankAccount(idx, { accountNumber: e.target.value })}
              className="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 font-mono text-xs"
            />
            <input
              type="text"
              placeholder={t('onboarding.ifscCode')}
              value={acc.ifscCode}
              onChange={(e) => updateBankAccount(idx, { ifscCode: e.target.value.toUpperCase() })}
              className="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 font-mono text-xs uppercase"
            />

            <label className="flex cursor-pointer items-center justify-between rounded-lg border border-dashed border-slate-300 bg-slate-50 px-2.5 py-1.5 text-[11px]">
              <span className="inline-flex items-center gap-1 font-medium text-slate-700">
                <Upload className="h-3 w-3" />
                {uploadingIdx === idx ? t('onboarding.loading') : t('onboarding.uploadQrImage')}
              </span>
              {acc.qrImageKey && <CheckCircle2 className="h-3.5 w-3.5 text-emerald-700" />}
              <input
                type="file"
                accept="image/png,image/jpeg,image/webp"
                className="hidden"
                onChange={(e) => void handleQrUpload(idx, e.target.files?.[0] ?? null)}
              />
            </label>
          </div>
        ))}
      </div>

      <div className="rounded-xl border border-indigo-200 bg-indigo-50/60 p-4 grid gap-4 md:grid-cols-[1fr_auto] items-center">
        <div className="space-y-3">
          <div className="flex items-center gap-2 text-indigo-900">
            <QrCode className="h-4 w-4" />
            <span className="text-xs font-bold">{t('onboarding.upiConfigTitle')}</span>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <input
              type="text"
              placeholder="e.g. gpchincholi@sbi"
              value={state.primaryUpiId}
              onChange={(e) => updateField('primaryUpiId', e.target.value)}
              className="rounded-lg border border-indigo-200 bg-white px-3 py-2 font-mono text-xs font-bold"
            />
            <input
              type="text"
              placeholder={t('onboarding.upiPayee')}
              value={state.upiPayeeName}
              onChange={(e) => updateField('upiPayeeName', e.target.value)}
              className="rounded-lg border border-indigo-200 bg-white px-3 py-2 text-xs"
            />
          </div>
          <code className="block break-all rounded bg-white/80 px-2.5 py-1.5 text-[11px] font-mono text-indigo-950 border border-indigo-100">
            {upiUri}
          </code>
        </div>

        <div className="flex flex-col items-center justify-center rounded-xl border border-indigo-200 bg-white p-3">
          <svg viewBox="0 0 21 21" className="h-24 w-24 text-slate-900" fill="currentColor" aria-label="Dynamic UPI QR Preview">
            <rect x="0" y="0" width="7" height="7" />
            <rect x="1" y="1" width="5" height="5" fill="#fff" />
            <rect x="2" y="2" width="3" height="3" />
            <rect x="14" y="0" width="7" height="7" />
            <rect x="15" y="1" width="5" height="5" fill="#fff" />
            <rect x="16" y="2" width="3" height="3" />
            <rect x="0" y="14" width="7" height="7" />
            <rect x="1" y="15" width="5" height="5" fill="#fff" />
            <rect x="2" y="16" width="3" height="3" />
            <rect x="9" y="9" width="3" height="3" />
            <rect x="9" y="3" width="3" height="3" />
            <rect x="13" y="13" width="4" height="4" />
            <rect x="9" y="15" width="2" height="4" />
          </svg>
          <span className="mt-1 text-[10px] font-bold text-emerald-700">{t('onboarding.qrDynamicActive')}</span>
        </div>
      </div>
    </div>
  );
}
