import { CheckCircle2, ShieldAlert, UserPlus } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ApiProblemError } from '../../services/apiClient';
import { sprint2Api, type AgentRegistrationResult } from '../../services/sprint2Api';

export function AgentRegisterPage() {
  const { t } = useTranslation();
  const [fullName, setFullName] = useState('');
  const [mobileNo, setMobileNo] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [address, setAddress] = useState('');
  const [villageOrCity, setVillageOrCity] = useState('');
  const [district, setDistrict] = useState('Amravati');
  const [talukasCsv, setTalukasCsv] = useState('');
  const [idProofType, setIdProofType] = useState('AADHAAR');
  const [aadhaarNumber, setAadhaarNumber] = useState('');
  const [panNumber, setPanNumber] = useState('');
  const [bankName, setBankName] = useState('');
  const [bankBranch, setBankBranch] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [ifscCode, setIfscCode] = useState('');
  const [upiId, setUpiId] = useState('');
  const [requestedRate, setRequestedRate] = useState(20);
  const [aadhaarFile, setAadhaarFile] = useState<File | null>(null);
  const [panFile, setPanFile] = useState<File | null>(null);

  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [result, setResult] = useState<AgentRegistrationResult | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setSubmitting(true);
    try {
      const form = new FormData();
      form.append('fullName', fullName);
      form.append('mobileNo', mobileNo);
      form.append('email', email);
      form.append('password', password);
      form.append('address', address);
      form.append('villageOrCity', villageOrCity);
      form.append('district', district);
      form.append('talukasCsv', talukasCsv);
      form.append('idProofType', idProofType);
      form.append('aadhaarNumber', aadhaarNumber);
      form.append('panNumber', panNumber.toUpperCase());
      form.append('bankName', bankName);
      form.append('bankBranch', bankBranch);
      form.append('accountNumber', accountNumber);
      form.append('ifscCode', ifscCode.toUpperCase());
      form.append('upiId', upiId);
      form.append('requestedCommissionRatePct', String(requestedRate));
      if (aadhaarFile) form.append('aadhaarDocument', aadhaarFile);
      if (panFile) form.append('panDocument', panFile);

      const res = await sprint2Api.registerAgent(form);
      setResult(res);
    } catch (err) {
      if (err instanceof ApiProblemError) {
        const firstErr = err.problem.errors ? Object.values(err.problem.errors).flat()[0] : null;
        setErrorMsg(firstErr || err.problem.detail || err.message);
      } else {
        setErrorMsg(err instanceof Error ? err.message : t('agent.registerFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-emerald-200 bg-white p-6 shadow-sm text-center space-y-4">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100 text-emerald-700">
          <CheckCircle2 className="h-8 w-8" />
        </div>
        <h1 className="text-xl font-bold text-slate-900">{t('agent.registerSuccessTitle')}</h1>
        <p className="text-xs text-slate-600">{t('agent.pendingApprovalDesc')}</p>
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-left font-mono text-xs space-y-1.5">
          <p>{t('agent.codeLabel')}: <strong>{result.agentCode}</strong></p>
          <p>{t('agent.aadhaarLabel')}: <strong>{result.aadhaarMasked}</strong></p>
          <p>{t('agent.panLabel')}: <strong>{result.panMasked}</strong></p>
        </div>
        <Link
          to="/agent/login"
          className="inline-block rounded-xl bg-emerald-700 px-5 py-2.5 text-xs font-bold text-white hover:bg-emerald-800"
        >
          {t('agent.goToLogin')}
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-5">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-emerald-800">
          <UserPlus className="h-5 w-5" />
          <h1 className="text-xl font-bold text-slate-900">{t('agent.registerTitle')}</h1>
        </div>
        <Link to="/agent/login" className="text-xs font-bold text-emerald-700 hover:underline">
          {t('agent.goToLogin')}
        </Link>
      </div>

      {errorMsg && (
        <div className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs text-rose-800">
          <ShieldAlert className="h-4 w-4 shrink-0" />
          <span>{errorMsg}</span>
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4 text-xs">
        <div className="grid gap-3 sm:grid-cols-3">
          <input required type="text" placeholder={t('agent.fullName')} value={fullName} onChange={(e) => setFullName(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input required type="tel" maxLength={10} placeholder={t('agent.mobile')} value={mobileNo} onChange={(e) => setMobileNo(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2 font-mono" />
          <input required type="email" placeholder={t('agent.email')} value={email} onChange={(e) => setEmail(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input required type="password" placeholder={t('login.password')} value={password} onChange={(e) => setPassword(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2 font-mono" />
          <select value={idProofType} onChange={(e) => setIdProofType(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2">
            <option value="AADHAAR">Aadhaar Card</option>
            <option value="PAN">PAN Card</option>
          </select>
          <input required type="text" placeholder={t('agent.aadhaarInput')} value={aadhaarNumber} onChange={(e) => setAadhaarNumber(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2 font-mono" />
          <input required type="text" placeholder={t('agent.panInput')} value={panNumber} onChange={(e) => setPanNumber(e.target.value.toUpperCase())} className="rounded-lg border border-slate-300 px-3 py-2 font-mono uppercase" />
          <input required type="text" placeholder={t('agent.address')} value={address} onChange={(e) => setAddress(e.target.value)} className="sm:col-span-2 rounded-lg border border-slate-300 px-3 py-2" />
          <input required type="text" placeholder={t('agent.villageCity')} value={villageOrCity} onChange={(e) => setVillageOrCity(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input required type="text" placeholder={t('onboarding.district')} value={district} onChange={(e) => setDistrict(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input required type="text" placeholder={t('agent.talukasCsv')} value={talukasCsv} onChange={(e) => setTalukasCsv(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input type="text" placeholder={t('onboarding.bankName')} value={bankName} onChange={(e) => setBankName(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input type="text" placeholder={t('onboarding.branchName')} value={bankBranch} onChange={(e) => setBankBranch(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2" />
          <input type="text" placeholder={t('onboarding.accountNo')} value={accountNumber} onChange={(e) => setAccountNumber(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2 font-mono" />
          <input type="text" placeholder={t('onboarding.ifscCode')} value={ifscCode} onChange={(e) => setIfscCode(e.target.value.toUpperCase())} className="rounded-lg border border-slate-300 px-3 py-2 font-mono uppercase" />
          <input type="text" placeholder="UPI ID" value={upiId} onChange={(e) => setUpiId(e.target.value)} className="rounded-lg border border-slate-300 px-3 py-2 font-mono" />
          <input type="number" min={1} max={50} value={requestedRate} onChange={(e) => setRequestedRate(Number(e.target.value))} className="rounded-lg border border-slate-300 px-3 py-2 font-mono" />
        </div>

        <div className="grid gap-3 sm:grid-cols-2 border-t border-slate-100 pt-3">
          <label className="block space-y-1">
            <span className="font-bold text-slate-700">{t('agent.aadhaarDocUpload')}</span>
            <input type="file" accept="image/png,image/jpeg,application/pdf" onChange={(e) => setAadhaarFile(e.target.files?.[0] ?? null)} className="w-full text-[11px]" />
          </label>
          <label className="block space-y-1">
            <span className="font-bold text-slate-700">{t('agent.panDocUpload')}</span>
            <input type="file" accept="image/png,image/jpeg,application/pdf" onChange={(e) => setPanFile(e.target.files?.[0] ?? null)} className="w-full text-[11px]" />
          </label>
        </div>

        <button
          type="submit"
          disabled={submitting}
          className="rounded-xl bg-emerald-700 px-6 py-2.5 text-xs font-bold text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? t('onboarding.loading') : t('agent.submitApplication')}
        </button>
      </form>
    </div>
  );
}

