import { LogOut, Shield, ShieldAlert } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/useAuth';
import { ApiProblemError } from '../services/apiClient';

export function LoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user, login, logout } = useAuth();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [tenantId, setTenantId] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setSubmitting(true);
    try {
      const auth = await login(username.trim(), password, tenantId.trim() || null);
      if (auth.user.role === 'SuperAdmin') {
        void navigate('/superadmin');
      } else if (auth.user.role === 'Field_Agent') {
        void navigate('/agent/dashboard');
      } else {
        void navigate('/dashboard');
      }
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setErrorMsg(err.problem.detail || err.message);
      } else {
        setErrorMsg(err instanceof Error ? err.message : t('login.invalidCredentials'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (user) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-emerald-200 bg-white p-6 shadow-sm space-y-4">
        <div className="flex items-center gap-2 text-emerald-700">
          <Shield className="h-5 w-5" />
          <h1 className="text-lg font-bold text-slate-900">{user.fullNameEn}</h1>
        </div>
        <p className="font-mono text-xs text-slate-600">
          {user.username} · <strong>{user.role}</strong>
        </p>
        <button
          type="button"
          onClick={() => void logout()}
          className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-4 py-2 text-xs font-bold text-slate-800 hover:bg-slate-100"
        >
          <LogOut className="h-4 w-4" />
          {t('login.logoutBtn')}
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
      <div className="flex items-center gap-2 text-emerald-700">
        <Shield className="h-5 w-5" />
        <h1 className="text-lg font-bold text-slate-900">{t('login.title')}</h1>
      </div>

      {errorMsg && (
        <div className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-800">
          <ShieldAlert className="h-4 w-4 shrink-0" />
          <span>{errorMsg}</span>
        </div>
      )}

      <form className="space-y-4" onSubmit={(e) => void handleSubmit(e)}>
        <label className="block text-xs font-semibold text-slate-700">
          {t('login.email')}
          <input
            type="text"
            required
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="gramsevak.gp"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
        </label>

        <label className="block text-xs font-semibold text-slate-700">
          {t('login.password')}
          <input
            type="password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="••••••••"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
        </label>

        <label className="block text-xs font-semibold text-slate-700">
          {t('login.tenantSelector')}
          <input
            type="text"
            value={tenantId}
            onChange={(e) => setTenantId(e.target.value)}
            placeholder="UUID (Optional)"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-xs"
          />
        </label>

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-lg bg-emerald-700 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? t('onboarding.loading') : t('login.submit')}
        </button>
      </form>

      <div className="flex items-center justify-between border-t border-slate-100 pt-3 text-xs">
        <Link to="/agent/login" className="font-bold text-emerald-700 hover:underline">
          {t('nav.agentPortal')}
        </Link>
        <Link to="/onboarding" className="font-bold text-slate-600 hover:underline">
          {t('nav.onboarding')}
        </Link>
      </div>
    </div>
  );
}
