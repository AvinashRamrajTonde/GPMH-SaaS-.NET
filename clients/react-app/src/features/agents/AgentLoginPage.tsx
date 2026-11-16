import { AlertTriangle, Eye, EyeOff, Mail, PhoneCall, ShieldAlert, UserCheck } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { KycStatusBadge } from '../../components/StatusBadge';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';

interface GateInfo {
  kycStatus: string;
  agentCode: string;
  reason?: string;
  helplinePhone: string;
  helplineEmail: string;
}

export function AgentLoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { loginAgent } = useAuth();

  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [gateInfo, setGateInfo] = useState<GateInfo | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setGateInfo(null);
    setSubmitting(true);
    try {
      await loginAgent(identifier.trim(), password);
      void navigate('/agent/dashboard');
    } catch (err) {
      if (err instanceof ApiProblemError) {
        if (err.problem.status === 403 && err.problem.kycStatus) {
          setGateInfo({
            kycStatus: err.problem.kycStatus,
            agentCode: err.problem.agentCode || '',
            reason: err.problem.reason,
            helplinePhone: err.problem.helplinePhone || '0721-2550101',
            helplineEmail: err.problem.helplineEmail || 'support@epanchayat-mh.gov.in',
          });
        } else {
          setErrorMsg(err.problem.detail || err.message);
        }
      } else {
        setErrorMsg(err instanceof Error ? err.message : t('login.invalidCredentials'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-md py-6 space-y-5">
      <div className="text-center space-y-2">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-tr from-emerald-500 to-teal-700 text-white shadow-lg border border-emerald-400/30">
          <UserCheck className="h-7 w-7" />
        </div>
        <h1 className="text-xl font-extrabold text-white">{t('agent.loginTitle')}</h1>
        <p className="text-xs text-slate-400">{t('agent.loginSubtitle')}</p>
      </div>

      <div className="rounded-2xl border border-slate-800 bg-slate-900 p-7 shadow-2xl space-y-5">
        {errorMsg && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-xl border border-rose-500/40 bg-rose-950/60 px-3.5 py-2.5 text-xs text-rose-200"
          >
            <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{errorMsg}</span>
          </div>
        )}

        {gateInfo && (
          <div className="rounded-xl border border-amber-500/40 bg-amber-950/40 p-4 text-xs text-amber-100 space-y-2.5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="inline-flex items-center gap-1.5 font-extrabold text-amber-300">
                <AlertTriangle className="h-4 w-4" />
                {t('agent.statusGateTitle')}
              </span>
              <KycStatusBadge status={gateInfo.kycStatus} dark />
            </div>
            <p className="mono text-[11px]">
              {t('agent.codeLabel')}: <strong className="text-white">{gateInfo.agentCode}</strong>
            </p>
            {gateInfo.reason && <p className="text-slate-200">{gateInfo.reason}</p>}
            <div className="space-y-1 border-t border-amber-500/20 pt-2 text-[11px]">
              <p className="font-bold text-amber-300">{t('agent.helplineLabel')}</p>
              <div className="flex flex-wrap gap-3">
                <a
                  href={`tel:${gateInfo.helplinePhone}`}
                  className="inline-flex items-center gap-1 text-emerald-300 hover:underline"
                >
                  <PhoneCall className="h-3 w-3" />
                  {gateInfo.helplinePhone}
                </a>
                <a
                  href={`mailto:${gateInfo.helplineEmail}`}
                  className="inline-flex items-center gap-1 text-emerald-300 hover:underline"
                >
                  <Mail className="h-3 w-3" />
                  {gateInfo.helplineEmail}
                </a>
              </div>
            </div>
          </div>
        )}

        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4 text-xs">
          <label className="block space-y-1.5 font-bold text-slate-300">
            <span>{t('agent.loginIdentifierLabel')}</span>
            <input
              type="text"
              required
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              placeholder="9822112233"
              className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3.5 py-2.5 mono text-sm font-bold text-amber-300 focus:border-emerald-500"
            />
          </label>

          <label className="block space-y-1.5 font-bold text-slate-300">
            <span>{t('login.password')}</span>
            <div className="relative">
              <input
                type={showPassword ? 'text' : 'password'}
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3.5 py-2.5 pr-10 text-sm text-white focus:border-emerald-500"
              />
              <button
                type="button"
                aria-label={showPassword ? t('login.hidePassword') : t('login.showPassword')}
                onClick={() => setShowPassword((v) => !v)}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-1 text-slate-400 hover:text-white"
              >
                {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>
          </label>

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-4 py-3 text-sm font-bold text-white shadow-lg hover:from-emerald-500 hover:to-teal-500 disabled:opacity-60 transition"
          >
            {submitting ? t('onboarding.loading') : t('agent.loginBtn')}
          </button>
        </form>

        <div className="border-t border-slate-800 pt-4 text-center text-xs">
          <Link to="/agent/register" className="font-bold text-emerald-400 hover:underline">
            {t('agent.registerCta')}
          </Link>
        </div>
      </div>
    </div>
  );
}
