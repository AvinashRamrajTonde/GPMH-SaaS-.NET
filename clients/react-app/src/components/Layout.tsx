import { Building2, Calculator, Globe, LogOut, ShieldCheck, UserCheck } from 'lucide-react';
import type { PropsWithChildren } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../context/useAuth';
import { setAppLanguage, type SupportedLocale } from '../i18n';

const LOCALES: { code: SupportedLocale; labelKey: string }[] = [
  { code: 'mr', labelKey: 'language.mr' },
  { code: 'hi', labelKey: 'language.hi' },
  { code: 'en', labelKey: 'language.en' },
];

export function Layout({ children }: PropsWithChildren) {
  const { t, i18n } = useTranslation();
  const { user, logout } = useAuth();

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `inline-flex items-center gap-1.5 rounded-lg px-2.5 py-2 text-xs font-semibold transition-colors ${isActive
      ? 'bg-emerald-700 text-white shadow-sm'
      : 'text-slate-700 hover:bg-slate-200/70'
    }`;

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-emerald-700 text-white shadow-sm">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-bold text-slate-900">{t('brand')}</p>
              <p className="text-[11px] text-slate-500">{t('subtitle')}</p>
            </div>
          </div>

          <nav className="flex flex-wrap items-center gap-1.5">
            <NavLink to="/" end className={navLinkClass}>
              <Calculator className="h-3.5 w-3.5" />
              {t('nav.dashboard')}
            </NavLink>
            <NavLink to="/onboarding" className={navLinkClass}>
              <Building2 className="h-3.5 w-3.5" />
              {t('nav.onboarding')}
            </NavLink>
            <NavLink to="/superadmin" className={navLinkClass}>
              <ShieldCheck className="h-3.5 w-3.5" />
              {t('nav.superadmin')}
            </NavLink>
            <NavLink to="/agent/login" className={navLinkClass}>
              <UserCheck className="h-3.5 w-3.5" />
              {t('nav.agentPortal')}
            </NavLink>
            <NavLink to="/login" className={navLinkClass}>
              <ShieldCheck className="h-3.5 w-3.5" />
              {t('nav.login')}
            </NavLink>
          </nav>

          <div className="flex items-center gap-2">
            {user && (
              <button
                type="button"
                onClick={() => void logout()}
                title={t('login.logoutBtn')}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-[11px] font-bold text-slate-700 hover:bg-slate-100"
              >
                <span className="text-emerald-700">{user.role}</span>
                <LogOut className="h-3.5 w-3.5" />
              </button>
            )}

            <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-slate-100 p-1">
              <Globe className="ml-1 h-3.5 w-3.5 text-slate-500" />
              {LOCALES.map((loc) => {
                const active = i18n.language === loc.code;
                return (
                  <button
                    key={loc.code}
                    type="button"
                    onClick={() => setAppLanguage(loc.code)}
                    className={`rounded-md px-2 py-1 text-[11px] font-semibold transition ${active
                      ? 'bg-white text-emerald-700 shadow-sm'
                      : 'text-slate-600 hover:text-slate-900'
                      }`}
                  >
                    {t(loc.labelKey)}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">{children}</main>
    </div>
  );
}
