import { AlertTriangle, PhoneCall, ShieldCheck } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
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
        const ext = err.problem as unknown as Record<string, string | undefined>;
        if (err.problem.status === 403 && ext.kycStatus) {
          setGateInfo({
            kycStatus: ext.kycStatus,
            agentCode: ext.agentCode || '',
            reason: ext.reason,
            helplinePhone: ext.helplinePhone || '0721-2550101',
            helplineEmail: ext.helplineEmail || 'support@epanchayat-mh.gov.in',
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
    <div className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
      <div className="flex items-center gap-2 text-emerald-700">
        <ShieldCheck className="h-5 w-5" />
        <h1 className="text-lg font-bold text-slate-900">{t('agent.loginTitle')}</h1>
      </div>

      {errorMsg && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-800">
          {errorMsg}
        </div>
      )}

      {gateInfo && (
        <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-xs text-amber-950 space-y-2">
          <div className="flex items-center gap-2 font-bold">
            <AlertTriangle className="h-4 w-4 text-amber-700" />
            <span>
              {t('agent.statusGateTitle')} ({gateInfo.kycStatus})
            </span>
          </div>
          <p className="font-mono">
            {t('agent.codeLabel')}: <strong>{gateInfo.agentCode}</strong>
          </p>
          {gateInfo.reason && <p>{gateInfo.reason}</p>}
          <div className="flex items-center gap-2 pt-1 font-semibold text-amber-900">
            <PhoneCall className="h-3.5 w-3.5" />
            <span>
              Helpline: {gateInfo.helplinePhone} · {gateInfo.helplineEmail}
            </span>
          </div>
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4 text-xs">
        <label className="block font-semibold text-slate-700">
          {t('agent.mobile')}
          <input
            type="text"
            required
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            placeholder="9822112233"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
          />
        </label>

        <label className="block font-semibold text-slate-700">
          {t('login.password')}
          <input
            type="password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
        </label>

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-xl bg-emerald-700 px-4 py-2.5 text-sm font-bold text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? t('onboarding.loading') : t('agent.loginBtn')}
        </button>
      </form>

      <div className="border-t border-slate-100 pt-3 text-center text-xs">
        <Link to="/agent/register" className="font-bold text-emerald-700 hover:underline">
          {t('agent.registerCta')}
        </Link>
      </div>
    </div>
  );
}
