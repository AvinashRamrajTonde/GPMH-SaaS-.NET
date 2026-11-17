import {
  ArrowRight,
  CheckCircle2,
  ChevronDown,
  Eye,
  EyeOff,
  Landmark,
  LogOut,
  Shield,
  ShieldAlert,
} from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { RoleBadge } from '../components/StatusBadge';
import { useAuth } from '../context/useAuth';
import { ApiProblemError } from '../services/apiClient';

export function LoginPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { user, login, logout, switchTenant } = useAuth();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showTenantInput, setShowTenantInput] = useState(false);
  const [tenantId, setTenantId] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setSubmitting(true);
    try {
      const auth = await login(username.trim(), password, tenantId.trim() || null);
      if (auth.user.role === 'SuperAdmin' || auth.user.role === 'Support_Admin') {
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
      <div className="flex flex-1 items-center justify-center p-6">
        <div className="w-full max-w-md rounded-2xl border border-brand-200 bg-white p-6 shadow-sm space-y-5">
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-700 text-white">
                <Shield className="h-5 w-5" />
              </div>
              <div>
                <p className="text-[11px] font-bold uppercase tracking-wider text-brand-700">
                  {t('login.activeSessionTitle')}
                </p>
                <h1 className="text-lg font-extrabold text-ink">
                  {i18n.language === 'mr' && user.fullNameMr ? user.fullNameMr : user.fullNameEn}
                </h1>
              </div>
            </div>
            <RoleBadge role={user.role} />
          </div>

          <div className="rounded-xl border border-line bg-surface p-3 text-xs space-y-1">
            <p>
              <span className="text-muted">ID:</span>{' '}
              <span className="mono font-bold text-ink">{user.username}</span>
            </p>
            {user.activeTenantId && (
              <p>
                <span className="text-muted">Tenant:</span>{' '}
                <span className="mono text-ink">{user.activeTenantId}</span>
              </p>
            )}
          </div>

          {user.memberships.length > 1 && (
            <label className="block text-xs font-bold text-slate-700 space-y-1">
              <span>{t('nav.switchTenant')}</span>
              <select
                value={user.activeTenantId ?? ''}
                onChange={(e) => void switchTenant(e.target.value)}
                className="w-full rounded-lg border border-line bg-white px-3 py-2 mono text-xs"
              >
                {user.memberships.map((m) => (
                  <option key={m.tenantId} value={m.tenantId}>
                    {m.tenantId} ({t(`roles.${m.role}`)})
                  </option>
                ))}
              </select>
            </label>
          )}

          <div className="flex flex-wrap items-center gap-2 pt-1">
            <Link
              to={
                user.role === 'SuperAdmin' || user.role === 'Support_Admin'
                  ? '/superadmin'
                  : user.role === 'Field_Agent'
                    ? '/agent/dashboard'
                    : '/dashboard'
              }
              className="flex-1 inline-flex items-center justify-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2.5 text-xs font-bold text-white hover:bg-brand-800 transition"
            >
              <span>{t('nav.dashboard')}</span>
              <ArrowRight className="h-4 w-4" />
            </Link>
            <button
              type="button"
              onClick={() => void logout()}
              className="inline-flex items-center gap-1.5 rounded-xl border border-slate-300 px-4 py-2.5 text-xs font-bold text-slate-700 hover:bg-slate-100 transition"
            >
              <LogOut className="h-4 w-4" />
              {t('login.logoutBtn')}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 grid lg:grid-cols-2">
      {/* Left Statutory Hero Panel */}
      <section className="hidden lg:flex flex-col justify-between bg-gradient-to-br from-brand-900 via-brand-800 to-brand-700 p-12 text-white">
        <div className="space-y-6">
          <div className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3.5 py-1.5 text-xs font-bold text-brand-100">
            <Landmark className="h-4 w-4" />
            <span>{t('login.heroBadge')}</span>
          </div>
          <h1 className="text-3xl font-extrabold leading-snug text-white">
            {t('login.heroTitle')}
          </h1>
          <p className="max-w-lg text-sm text-brand-100 leading-relaxed">
            {t('login.heroSubtitle')}
          </p>

          <ul className="space-y-3 pt-4 text-sm text-brand-50">
            <li className="flex items-start gap-2.5">
              <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-brand-300" />
              <span>{t('login.heroPoint1')}</span>
            </li>
            <li className="flex items-start gap-2.5">
              <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-brand-300" />
              <span>{t('login.heroPoint2')}</span>
            </li>
            <li className="flex items-start gap-2.5">
              <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-brand-300" />
              <span>{t('login.heroPoint3')}</span>
            </li>
          </ul>
        </div>

        <div className="border-t border-white/15 pt-5 text-xs text-brand-200 space-y-1">
          <p>{t('footer.standards')}</p>
          <p className="mono">{t('footer.helpline')}</p>
        </div>
      </section>

      {/* Right Sign-In Form Panel */}
      <section className="flex items-center justify-center p-6 sm:p-10 bg-surface">
        <div className="w-full max-w-md rounded-2xl border border-line bg-white p-7 shadow-sm space-y-6">
          <div className="space-y-1.5">
            <div className="inline-flex h-10 w-10 items-center justify-center rounded-xl bg-brand-50 text-brand-700 border border-brand-200">
              <Shield className="h-5 w-5" />
            </div>
            <h2 className="text-xl font-extrabold text-ink">{t('login.title')}</h2>
            <p className="text-xs text-muted">{t('login.cardSubtitle')}</p>
          </div>

          {errorMsg && (
            <div
              role="alert"
              className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-xs font-medium text-rose-800"
            >
              <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{errorMsg}</span>
            </div>
          )}

          <form className="space-y-4" onSubmit={(e) => void handleSubmit(e)}>
            <label className="block space-y-1.5 text-xs font-bold text-slate-700">
              <span>
                {t('login.email')} <span className="text-rose-600">*</span>
              </span>
              <input
                type="text"
                required
                autoComplete="username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="gramsevak"
                className="w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm font-medium text-ink focus:border-brand-600"
              />
            </label>

            <label className="block space-y-1.5 text-xs font-bold text-slate-700">
              <span>
                {t('login.password')} <span className="text-rose-600">*</span>
              </span>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  required
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 pr-10 text-sm text-ink focus:border-brand-600"
                />
                <button
                  type="button"
                  aria-label={showPassword ? t('login.hidePassword') : t('login.showPassword')}
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-500 hover:bg-slate-100 hover:text-ink"
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </label>

            <div>
              <button
                type="button"
                onClick={() => setShowTenantInput((v) => !v)}
                className="inline-flex items-center gap-1 text-[11px] font-semibold text-brand-700 hover:underline"
              >
                <ChevronDown
                  className={`h-3.5 w-3.5 transition-transform ${showTenantInput ? 'rotate-180' : ''}`}
                />
                <span>{t('login.advancedTenantToggle')}</span>
              </button>
              {showTenantInput && (
                <label className="mt-2 block space-y-1 text-xs font-semibold text-slate-700">
                  <span>{t('login.tenantSelector')}</span>
                  <input
                    type="text"
                    value={tenantId}
                    onChange={(e) => setTenantId(e.target.value)}
                    placeholder={t('login.tenantPlaceholder')}
                    className="w-full rounded-xl border border-slate-300 bg-surface px-3 py-2 mono text-xs"
                  />
                </label>
              )}
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="w-full rounded-xl bg-brand-700 px-4 py-2.5 text-sm font-bold text-white shadow-xs hover:bg-brand-800 disabled:opacity-60 transition"
            >
              {submitting ? t('onboarding.loading') : t('login.submit')}
            </button>
          </form>

          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 pt-4 text-xs">
            <Link to="/agent/login" className="font-bold text-brand-700 hover:underline">
              {t('nav.agentPortal')}
            </Link>
            <Link to="/onboarding" className="font-bold text-slate-600 hover:text-brand-700 hover:underline">
              {t('nav.onboarding')}
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
