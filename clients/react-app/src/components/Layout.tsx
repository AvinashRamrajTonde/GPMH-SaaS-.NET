import { Building2, Calculator, Globe, ShieldCheck } from 'lucide-react';
import type { PropsWithChildren } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router-dom';
import { setAppLanguage, type SupportedLocale } from '../i18n';

const LOCALES: { code: SupportedLocale; labelKey: string }[] = [
  { code: 'mr', labelKey: 'language.mr' },
  { code: 'hi', labelKey: 'language.hi' },
  { code: 'en', labelKey: 'language.en' },
];

export function Layout({ children }: PropsWithChildren) {
  const { t, i18n } = useTranslation();

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${isActive
      ? 'bg-emerald-700 text-white shadow-sm'
      : 'text-slate-700 hover:bg-slate-200/70'
    }`;

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-700 text-white shadow-sm">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <p className="text-base font-bold text-slate-900">{t('brand')}</p>
              <p className="text-xs text-slate-500">{t('subtitle')}</p>
            </div>
          </div>

          <nav className="flex flex-wrap items-center gap-2">
            <NavLink to="/" end className={navLinkClass}>
              <Calculator className="h-4 w-4" />
              {t('nav.dashboard')}
            </NavLink>
            <NavLink to="/onboarding" className={navLinkClass}>
              <Building2 className="h-4 w-4" />
              {t('nav.onboarding')}
            </NavLink>
            <NavLink to="/login" className={navLinkClass}>
              <ShieldCheck className="h-4 w-4" />
              {t('nav.login')}
            </NavLink>
          </nav>

          <div className="flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-100 p-1">
            <Globe className="ml-1.5 h-4 w-4 text-slate-500" />
            {LOCALES.map((loc) => {
              const active = i18n.language === loc.code;
              return (
                <button
                  key={loc.code}
                  type="button"
                  onClick={() => setAppLanguage(loc.code)}
                  className={`rounded-md px-2.5 py-1 text-xs font-semibold transition ${active
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
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">{children}</main>
    </div>
  );
}

