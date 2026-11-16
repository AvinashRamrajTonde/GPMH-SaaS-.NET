import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Eye, EyeOff, Lock, UserPlus } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { getDistricts } from '../../services/lgdApi';
import { type AgentRegistrationResult, sprint2Api } from '../../services/sprint2Api';
import {
  evaluatePasswordStrength,
  extractProblemValidation,
  type ParsedValidationItem,
} from '../../utils/validation';

export function AgentRegisterPage() {
  const { t } = useTranslation();

  const [fullName, setFullName] = useState('');
  const [mobileNo, setMobileNo] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);

  const [idProofType, setIdProofType] = useState('AADHAAR');
  const [aadhaarNumber, setAadhaarNumber] = useState('');
  const [panNumber, setPanNumber] = useState('');
  const [aadhaarFile, setAadhaarFile] = useState<File | null>(null);
  const [panFile, setPanFile] = useState<File | null>(null);

  const [address, setAddress] = useState('');
  const [villageOrCity, setVillageOrCity] = useState('');
  const [district, setDistrict] = useState('Amravati');
  const [talukasCsv, setTalukasCsv] = useState('');

  const [bankName, setBankName] = useState('');
  const [bankBranch, setBankBranch] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [ifscCode, setIfscCode] = useState('');
  const [upiId, setUpiId] = useState('');
  const [requestedRate, setRequestedRate] = useState(20);

  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [errorItems, setErrorItems] = useState<ParsedValidationItem[]>([]);
  const [result, setResult] = useState<AgentRegistrationResult | null>(null);

  const districtsQuery = useQuery({
    queryKey: ['lgd', 'districts'],
    queryFn: getDistricts,
    staleTime: 1000 * 60 * 30,
  });

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setErrorItems([]);

    const items: ParsedValidationItem[] = [];
    if (!/^\d{10}$/.test(mobileNo.trim())) {
      items.push({ key: 'mobileNo', rawField: 'MobileNo', message: t('validation.mobileDigits') });
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      items.push({ key: 'email', rawField: 'Email', message: t('validation.emailFormat') });
    }
    if (!evaluatePasswordStrength(password).isValid) {
      items.push({ key: 'password', rawField: 'Password', message: t('validation.passwordRules') });
    }
    if (password !== confirmPassword) {
      items.push({
        key: 'confirmPassword',
        rawField: 'ConfirmPassword',
        message: t('onboarding.passwordMismatch'),
      });
    }
    if (!/^\d{12}$/.test(aadhaarNumber.trim())) {
      items.push({
        key: 'aadhaarNumber',
        rawField: 'AadhaarNumber',
        message: t('validation.aadhaarDigits'),
      });
    }
    if (!/^[A-Z]{5}\d{4}[A-Z]$/.test(panNumber.trim().toUpperCase())) {
      items.push({ key: 'panNumber', rawField: 'PanNumber', message: t('validation.panFormat') });
    }
    if (ifscCode.trim() && !/^[A-Z]{4}0[A-Z0-9]{6}$/.test(ifscCode.trim().toUpperCase())) {
      items.push({ key: 'ifscCode', rawField: 'IfscCode', message: t('validation.ifscFormat') });
    }

    if (items.length > 0) {
      setErrorMsg(t('validation.summaryTitle'));
      setErrorItems(items);
      return;
    }

    setSubmitting(true);
    try {
      const form = new FormData();
      form.append('fullName', fullName.trim());
      form.append('mobileNo', mobileNo.trim());
      form.append('email', email.trim());
      form.append('password', password);
      form.append('address', address.trim());
      form.append('villageOrCity', villageOrCity.trim());
      form.append('district', district.trim());
      form.append('talukasCsv', talukasCsv.trim());
      form.append('idProofType', idProofType);
      form.append('aadhaarNumber', aadhaarNumber.trim());
      form.append('panNumber', panNumber.trim().toUpperCase());
      form.append('bankName', bankName.trim());
      form.append('bankBranch', bankBranch.trim());
      form.append('accountNumber', accountNumber.trim());
      form.append('ifscCode', ifscCode.trim().toUpperCase());
      form.append('upiId', upiId.trim());
      form.append('requestedCommissionRatePct', String(requestedRate));
      if (aadhaarFile) form.append('aadhaarDocument', aadhaarFile);
      if (panFile) form.append('panDocument', panFile);

      const res = await sprint2Api.registerAgent(form);
      setResult(res);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('agent.registerFailed'));
      setErrorMsg(parsed.summary);
      setErrorItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-emerald-500/30 bg-slate-900 p-7 text-center space-y-4 shadow-2xl">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
          <CheckCircle2 className="h-8 w-8" />
        </div>
        <h1 className="text-xl font-extrabold text-white">{t('agent.registerSuccessTitle')}</h1>
        <p className="text-xs text-slate-300">{t('agent.pendingApprovalDesc')}</p>
        <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 text-left mono text-xs space-y-1.5 text-slate-200">
          <p>
            {t('agent.codeLabel')}: <strong className="text-amber-300">{result.agentCode}</strong>
          </p>
          <p>
            {t('agent.aadhaarLabel')}: <strong>{result.aadhaarMasked}</strong>
          </p>
          <p>
            {t('agent.panLabel')}: <strong>{result.panMasked}</strong>
          </p>
        </div>
        <Link
          to="/agent/login"
          className="inline-block rounded-xl bg-emerald-600 px-5 py-2.5 text-xs font-bold text-white hover:bg-emerald-500"
        >
          {t('agent.goToLogin')}
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl rounded-2xl border border-slate-800 bg-slate-900 p-6 sm:p-8 shadow-2xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-800 pb-4">
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-emerald-400">
            <UserPlus className="h-5 w-5" />
            <h1 className="text-xl font-extrabold text-white">{t('agent.registerTitle')}</h1>
          </div>
          <p className="text-xs text-slate-400">{t('agent.registerSubtitle')}</p>
        </div>
        <Link to="/agent/login" className="text-xs font-bold text-emerald-400 hover:underline">
          {t('agent.goToLogin')} →
        </Link>
      </div>

      <div className="flex items-center gap-2 rounded-xl border border-emerald-500/30 bg-emerald-950/40 px-4 py-2.5 text-xs text-emerald-200">
        <Lock className="h-4 w-4 shrink-0 text-emerald-400" />
        <span>{t('agent.dpdpNotice')}</span>
      </div>

      <ValidationSummary summary={errorMsg} items={errorItems} dark />

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-6 text-xs">
        {/* Section 1: Personal & Login */}
        <section className="space-y-3">
          <h2 className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
            {t('agent.sec1Title')}
          </h2>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.fullName')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="text"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.mobile')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="tel"
                maxLength={10}
                value={mobileNo}
                onChange={(e) => setMobileNo(e.target.value)}
                placeholder="9822112233"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-amber-300 font-bold"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.email')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('login.password')} <span className="text-rose-400">*</span>
              </span>
              <div className="relative">
                <input
                  required
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 pr-9 mono text-white"
                />
                <button
                  type="button"
                  aria-label={showPassword ? t('login.hidePassword') : t('login.showPassword')}
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-1 text-slate-400 hover:text-white"
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.confirmPassword')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type={showPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className={`w-full rounded-lg border px-3 py-2 mono text-white ${
                  confirmPassword && confirmPassword !== password
                    ? 'border-rose-500 bg-rose-950/30'
                    : 'border-slate-700 bg-slate-950'
                }`}
              />
            </label>
          </div>
        </section>

        {/* Section 2: Identity & KYC Proofs */}
        <section className="space-y-3 border-t border-slate-800 pt-4">
          <h2 className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
            {t('agent.sec2Title')}
          </h2>
          <div className="grid gap-3 sm:grid-cols-3">
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.idProofTypeLabel')}</span>
              <select
                value={idProofType}
                onChange={(e) => setIdProofType(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              >
                <option value="AADHAAR">{t('agent.idProofAadhaar')}</option>
                <option value="PAN">{t('agent.idProofPan')}</option>
              </select>
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.aadhaarInput')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="text"
                maxLength={12}
                value={aadhaarNumber}
                onChange={(e) => setAadhaarNumber(e.target.value)}
                placeholder="123456789012"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.panInput')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="text"
                maxLength={10}
                value={panNumber}
                onChange={(e) => setPanNumber(e.target.value.toUpperCase())}
                placeholder="ABCDE1234F"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono uppercase text-white"
              />
            </label>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block rounded-xl border border-dashed border-slate-700 bg-slate-950 p-3 space-y-1.5 cursor-pointer hover:border-emerald-500">
              <span className="block font-bold text-slate-200">{t('agent.aadhaarDocUpload')}</span>
              <input
                type="file"
                accept="image/png,image/jpeg,application/pdf"
                onChange={(e) => setAadhaarFile(e.target.files?.[0] ?? null)}
                className="w-full text-[11px] text-slate-400 file:mr-3 file:rounded-lg file:border-0 file:bg-slate-800 file:px-3 file:py-1 file:text-xs file:font-bold file:text-emerald-300"
              />
            </label>
            <label className="block rounded-xl border border-dashed border-slate-700 bg-slate-950 p-3 space-y-1.5 cursor-pointer hover:border-emerald-500">
              <span className="block font-bold text-slate-200">{t('agent.panDocUpload')}</span>
              <input
                type="file"
                accept="image/png,image/jpeg,application/pdf"
                onChange={(e) => setPanFile(e.target.files?.[0] ?? null)}
                className="w-full text-[11px] text-slate-400 file:mr-3 file:rounded-lg file:border-0 file:bg-slate-800 file:px-3 file:py-1 file:text-xs file:font-bold file:text-emerald-300"
              />
            </label>
          </div>
        </section>

        {/* Section 3: Jurisdiction */}
        <section className="space-y-3 border-t border-slate-800 pt-4">
          <h2 className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
            {t('agent.sec3Title')}
          </h2>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <label className="sm:col-span-2 space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.address')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="text"
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.villageCity')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="text"
                value={villageOrCity}
                onChange={(e) => setVillageOrCity(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.district')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                list="mh-districts-list"
                type="text"
                value={district}
                onChange={(e) => setDistrict(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
              <datalist id="mh-districts-list">
                {districtsQuery.data?.map((d) => (
                  <option key={d.district} value={d.district} />
                ))}
              </datalist>
            </label>

            <label className="sm:col-span-2 space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.talukasCsv')} <span className="text-rose-400">*</span>
              </span>
              <input
                required
                type="text"
                value={talukasCsv}
                onChange={(e) => setTalukasCsv(e.target.value)}
                placeholder="Dharur, Kaij, Majalgaon"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>
          </div>
        </section>

        {/* Section 4: Payout Bank & Commission */}
        <section className="space-y-3 border-t border-slate-800 pt-4">
          <h2 className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
            {t('agent.sec4Title')}
          </h2>
          <div className="grid gap-3 sm:grid-cols-3">
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.requestedRateLabel')}</span>
              <input
                type="number"
                min={1}
                max={50}
                value={requestedRate}
                onChange={(e) => setRequestedRate(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300 font-bold"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.bankName')}</span>
              <input
                type="text"
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.branchName')}</span>
              <input
                type="text"
                value={bankBranch}
                onChange={(e) => setBankBranch(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.accountNo')}</span>
              <input
                type="text"
                value={accountNumber}
                onChange={(e) => setAccountNumber(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.ifscCode')}</span>
              <input
                type="text"
                maxLength={11}
                value={ifscCode}
                onChange={(e) => setIfscCode(e.target.value.toUpperCase())}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono uppercase text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.upiIdLabel')}</span>
              <input
                type="text"
                placeholder={t('agent.upiIdPlaceholder')}
                value={upiId}
                onChange={(e) => setUpiId(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>
          </div>
        </section>

        <button
          type="submit"
          disabled={submitting}
          className="rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-6 py-3 text-xs font-bold text-white shadow-lg hover:from-emerald-500 hover:to-teal-500 disabled:opacity-60 transition"
        >
          {submitting ? t('onboarding.loading') : t('agent.submitApplication')}
        </button>
      </form>
    </div>
  );
}
