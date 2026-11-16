import { ShieldCheck, UserPlus, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ValidationSummary } from '../../components/ValidationSummary';
import {
  type CitizenDto,
  EXEMPTION_CATEGORIES,
  namuna8Api,
  SOCIAL_CATEGORIES,
} from '../../services/namuna8Api';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';

interface QuickAddCitizenModalProps {
  existing?: CitizenDto | null;
  defaultWardNo?: string;
  onClose: () => void;
  onSaved: (citizen: CitizenDto) => void;
}

export function QuickAddCitizenModal({
  existing = null,
  defaultWardNo = '',
  onClose,
  onSaved,
}: QuickAddCitizenModalProps) {
  const { t } = useTranslation();

  const [fullNameMr, setFullNameMr] = useState(existing?.fullNameMr ?? '');
  const [fullNameEn, setFullNameEn] = useState(existing?.fullNameEn ?? '');
  const [wardNo, setWardNo] = useState(existing?.wardNo ?? defaultWardNo);
  const [gender, setGender] = useState(existing?.gender ?? 'Male');
  const [mobileNo, setMobileNo] = useState(existing?.mobileNo ?? '');
  const [aadhaarNumber, setAadhaarNumber] = useState('');
  const [socialCategory, setSocialCategory] = useState(existing?.socialCategory ?? 'General');
  const [exemptionCategory, setExemptionCategory] = useState(existing?.exemptionCategory ?? 'NotExempted');
  const [isActive, setIsActive] = useState(existing?.isActive ?? true);
  const [dpdpConsentAccepted, setDpdpConsentAccepted] = useState(Boolean(existing));

  const [saving, setSaving] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrSummary(null);
    setErrItems([]);

    const errs: ParsedValidationItem[] = [];
    if (!fullNameMr.trim()) {
      errs.push({ key: 'fullNameMr', rawField: 'FullNameMr', message: t('validation.required') });
    }
    if (!fullNameEn.trim()) {
      errs.push({ key: 'fullNameEn', rawField: 'FullNameEn', message: t('validation.required') });
    }
    if (mobileNo.trim() && !/^[6-9]\d{9}$/.test(mobileNo.trim())) {
      errs.push({ key: 'mobileNo', rawField: 'MobileNo', message: t('validation.mobileDigits') });
    }
    const digits = aadhaarNumber.replace(/[\s-]+/g, '');
    if (digits && !/^\d{12}$/.test(digits)) {
      errs.push({ key: 'aadhaarNumber', rawField: 'AadhaarNumber', message: t('validation.aadhaarDigits') });
    }
    if (!existing && !dpdpConsentAccepted) {
      errs.push({
        key: 'dpdpConsentAccepted',
        rawField: 'DpdpConsentAccepted',
        message: t('citizens.errConsentRequired'),
      });
    }

    if (errs.length > 0) {
      setErrSummary(t('validation.summaryTitle'));
      setErrItems(errs);
      return;
    }

    setSaving(true);
    try {
      const saved = existing
        ? await namuna8Api.updateCitizen(existing.id, {
            wardNo: wardNo.trim() || null,
            fullNameMr: fullNameMr.trim(),
            fullNameEn: fullNameEn.trim(),
            gender,
            mobileNo: mobileNo.trim() || null,
            aadhaarNumber: digits || null,
            socialCategory,
            exemptionCategory,
            isActive,
          })
        : await namuna8Api.createCitizen({
            wardNo: wardNo.trim() || null,
            fullNameMr: fullNameMr.trim(),
            fullNameEn: fullNameEn.trim(),
            gender,
            mobileNo: mobileNo.trim() || null,
            aadhaarNumber: digits || null,
            socialCategory,
            exemptionCategory,
            dpdpConsentAccepted,
          });
      onSaved(saved);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('citizens.saveFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 overflow-y-auto"
    >
      <div className="w-full max-w-2xl rounded-2xl border border-line bg-card p-6 shadow-xl space-y-4 text-xs">
        <div className="flex items-center justify-between border-b border-line pb-3">
          <div className="flex items-center gap-2">
            <UserPlus className="h-5 w-5 text-brand-700" />
            <div>
              <h2 className="text-base font-extrabold text-ink">
                {existing ? t('citizens.editModalTitle') : t('citizens.addModalTitle')}
              </h2>
              <p className="text-[11px] text-muted">{t('citizens.dpdpSubtitle')}</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100"
            aria-label={t('properties.cancel')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <ValidationSummary summary={errSummary} items={errItems} />

        <form onSubmit={(e) => void handleSubmit(e)} noValidate className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="space-y-1 font-bold text-slate-700">
              <span>
                {t('citizens.fullNameMr')} <span className="text-rose-600">*</span>
              </span>
              <input
                type="text"
                value={fullNameMr}
                onChange={(e) => setFullNameMr(e.target.value)}
                placeholder="उदा. रामेश्वर पाटील"
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-700">
              <span>
                {t('citizens.fullNameEn')} <span className="text-rose-600">*</span>
              </span>
              <input
                type="text"
                value={fullNameEn}
                onChange={(e) => setFullNameEn(e.target.value)}
                placeholder="e.g. Rameshwar Patil"
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </label>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <label className="space-y-1 font-bold text-slate-700">
              <span>{t('citizens.gender')}</span>
              <select
                value={gender}
                onChange={(e) => setGender(e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              >
                <option value="Male">{t('citizens.genderMale')}</option>
                <option value="Female">{t('citizens.genderFemale')}</option>
                <option value="Other">{t('citizens.genderOther')}</option>
              </select>
            </label>
            <label className="space-y-1 font-bold text-slate-700">
              <span>{t('properties.wardNo')}</span>
              <input
                type="text"
                value={wardNo}
                onChange={(e) => setWardNo(e.target.value)}
                placeholder="W-01"
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-700">
              <span>{t('citizens.mobileNo')}</span>
              <input
                type="tel"
                maxLength={10}
                value={mobileNo}
                onChange={(e) => setMobileNo(e.target.value)}
                placeholder="9822001122"
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
              />
            </label>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <label className="space-y-1 font-bold text-slate-700">
              <span>{t('citizens.aadhaarOptional')}</span>
              <input
                type="password"
                autoComplete="off"
                inputMode="numeric"
                maxLength={14}
                value={aadhaarNumber}
                onChange={(e) => setAadhaarNumber(e.target.value)}
                placeholder={existing?.aadhaarMasked ?? '1234 5678 9012'}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-700">
              <span>{t('citizens.socialCategory')}</span>
              <select
                value={socialCategory}
                onChange={(e) => setSocialCategory(e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              >
                {SOCIAL_CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat}
                  </option>
                ))}
              </select>
            </label>
            <label className="space-y-1 font-bold text-slate-700">
              <span>{t('citizens.exemptionCategory')}</span>
              <select
                value={exemptionCategory}
                onChange={(e) => setExemptionCategory(e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              >
                {EXEMPTION_CATEGORIES.map((ex) => (
                  <option key={ex} value={ex}>
                    {t(`citizens.exemption.${ex}`)}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {existing && (
            <label className="inline-flex items-center gap-2 font-bold text-slate-700 cursor-pointer">
              <input
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                className="h-4 w-4 rounded border-slate-300 text-brand-700"
              />
              <span>{t('formula.active')}</span>
            </label>
          )}

          {!existing && (
            <label className="flex items-start gap-2 rounded-xl border border-brand-200 bg-brand-50/50 p-3 font-medium text-slate-800 cursor-pointer">
              <input
                type="checkbox"
                checked={dpdpConsentAccepted}
                onChange={(e) => setDpdpConsentAccepted(e.target.checked)}
                className="mt-0.5 h-4 w-4 rounded border-slate-300 text-brand-700 shrink-0"
              />
              <span className="flex items-center gap-1.5">
                <ShieldCheck className="h-4 w-4 shrink-0 text-brand-700" />
                {t('citizens.dpdpConsentText')}
              </span>
            </label>
          )}

          <div className="flex justify-end gap-2 border-t border-line pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-slate-300 px-4 py-2 font-bold text-slate-700 hover:bg-slate-50"
            >
              {t('properties.cancel')}
            </button>
            <button
              type="submit"
              disabled={saving}
              className="rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
            >
              {saving ? t('onboarding.submitting') : t('citizens.saveBtn')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
