import {
  Building2,
  ClipboardCheck,
  Crown,
  FileSpreadsheet,
  GitBranch,
  History,
  Landmark,
  LayoutDashboard,
  LogIn,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  ReceiptText,
  ShieldCheck,
  SlidersHorizontal,
  UserCheck,
  X,
} from 'lucide-react';
import { useEffect, useState, type PropsWithChildren } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../context/useAuth';
import { setAppLanguage, type SupportedLocale } from '../i18n';
import {
  applyFontSize,
  type FontSizeOption,
  getSavedFontSize,
} from '../utils/formatters';
import { RoleBadge } from './StatusBadge';

const LOCALES: { code: SupportedLocale; labelKey: string }[] = [
  { code: 'mr', labelKey: 'language.mr' },
  { code: 'hi', labelKey: 'language.hi' },
  { code: 'en', labelKey: 'language.en' },
];

const FONT_OPTIONS: { size: FontSizeOption; label: string; ariaKey: string }[] = [
  { size: 14, label: 'A-', ariaKey: 'accessibility.fontSmaller' },
  { size: 16, label: 'A', ariaKey: 'accessibility.fontReset' },
  { size: 18, label: 'A+', ariaKey: 'accessibility.fontLarger' },
];

export function Layout({ children }: PropsWithChildren) {
  const { t, i18n } = useTranslation();
  const { user, switchTenant, logout } = useAuth();
  const location = useLocation();

  const [fontSize, setFontSize] = useState<FontSizeOption>(() => getSavedFontSize());
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [switchingTenant, setSwitchingTenant] = useState(false);

  useEffect(() => {
    applyFontSize(fontSize);
  }, [fontSize]);

  const isDarkSuperAdmin = location.pathname.startsWith('/superadmin');
  const isDarkAgent = location.pathname.startsWith('/agent');
  const isDarkConsole = isDarkSuperAdmin || isDarkAgent;
  const isLoginRoute = location.pathname === '/login';

  async function handleTenantChange(tenantId: string) {
    if (!tenantId || tenantId === user?.activeTenantId) return;
    setSwitchingTenant(true);
    try {
      await switchTenant(tenantId);
    } finally {
      setSwitchingTenant(false);
    }
  }

  function renderUtilityControls(dark = false) {
    return (
      <div className="flex flex-wrap items-center gap-2">
        <div
          className={`hidden sm:inline-flex items-center gap-0.5 rounded-lg border p-0.5 ${
            dark
              ? 'border-slate-700 bg-slate-950 text-slate-300'
              : 'border-line bg-surface text-slate-600'
          }`}
          title={t('accessibility.fontGroupTitle')}
          role="group"
          aria-label={t('accessibility.fontGroupTitle')}
        >
          {FONT_OPTIONS.map((opt) => {
            const active = fontSize === opt.size;
            return (
              <button
                key={opt.size}
                type="button"
                aria-label={t(opt.ariaKey)}
                aria-pressed={active}
                onClick={() => setFontSize(opt.size)}
                className={`rounded-md px-2 py-1 text-xs font-bold transition ${
                  active
                    ? dark
                      ? 'bg-slate-800 text-amber-300'
                      : 'bg-white text-brand-700 shadow-xs'
                    : dark
                      ? 'hover:bg-slate-800/60 hover:text-white'
                      : 'hover:bg-white hover:text-brand-700'
                }`}
              >
                {opt.label}
              </button>
            );
          })}
        </div>

        <div
          className={`inline-flex rounded-full p-[3px] border ${
            dark
              ? 'border-slate-700 bg-slate-950'
              : 'border-line bg-slate-100'
          }`}
          role="group"
          aria-label={t('language.label')}
        >
          {LOCALES.map((loc) => {
            const active = i18n.language === loc.code;
            return (
              <button
                key={loc.code}
                type="button"
                aria-pressed={active}
                onClick={() => setAppLanguage(loc.code)}
                className={`rounded-full px-2.5 py-1 text-[11px] font-semibold transition ${
                  active
                    ? dark
                      ? 'bg-brand-600 text-white'
                      : 'bg-brand-700 text-white shadow-xs'
                    : dark
                      ? 'text-slate-300 hover:text-white'
                      : 'text-slate-600 hover:text-ink'
                }`}
              >
                {t(loc.labelKey)}
              </button>
            );
          })}
        </div>
      </div>
    );
  }

  // Dedicated split-screen Login shell with minimal utility bar
  if (isLoginRoute) {
    return (
      <div className="min-h-screen flex flex-col bg-surface text-ink">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-brand-700 focus:px-3 focus:py-2 focus:text-xs focus:font-bold focus:text-white"
        >
          {t('accessibility.skipToMain')}
        </a>
        <header className="border-b border-line bg-white/95 backdrop-blur px-4 py-2.5">
          <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3">
            <Link to="/" className="flex items-center gap-2.5">
              <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-700 text-white shadow-xs">
                <Landmark className="h-5 w-5" />
              </div>
              <div className="leading-tight">
                <p className="text-sm font-extrabold text-ink">{t('brand')}</p>
                <p className="text-[11px] text-muted">{t('subtitle')}</p>
              </div>
            </Link>
            <div className="flex items-center gap-2">
              <Link
                to="/dashboard"
                className="hidden sm:inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-brand-600 hover:text-brand-700"
              >
                <LayoutDashboard className="h-3.5 w-3.5" />
                {t('nav.dashboard')}
              </Link>
              {renderUtilityControls(false)}
            </div>
          </div>
        </header>
        <main id="main-content" className="flex-1 flex flex-col">
          {children}
        </main>
      </div>
    );
  }

  // Dark Executive Console Shell for SuperAdmin HQ & Field Agent Portals
  if (isDarkConsole) {
    return (
      <div className="min-h-screen flex flex-col bg-slate-950 text-slate-100">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-amber-500 focus:px-3 focus:py-2 focus:text-xs focus:font-bold focus:text-slate-950"
        >
          {t('accessibility.skipToMain')}
        </a>
        <header className="sticky top-0 z-40 border-b border-slate-800 bg-slate-900/95 backdrop-blur px-4 py-3 shadow-lg no-print">
          <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <div
                className={`flex h-10 w-10 items-center justify-center rounded-xl text-white shadow-md ${
                  isDarkSuperAdmin
                    ? 'bg-gradient-to-tr from-amber-500 to-rose-600'
                    : 'bg-gradient-to-tr from-emerald-500 to-teal-700'
                }`}
              >
                {isDarkSuperAdmin ? (
                  <Crown className="h-5 w-5" />
                ) : (
                  <UserCheck className="h-5 w-5" />
                )}
              </div>
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-sm font-extrabold text-white tracking-wide">
                    {isDarkSuperAdmin ? t('superadmin.title') : t('agent.consoleTitle')}
                  </p>
                  <span
                    className={`rounded-full border px-2 py-0.5 text-[10px] font-bold ${
                      isDarkSuperAdmin
                        ? 'border-amber-500/30 bg-amber-500/20 text-amber-300'
                        : 'border-emerald-500/30 bg-emerald-500/20 text-emerald-300'
                    }`}
                  >
                    {isDarkSuperAdmin ? t('superadmin.consoleBadge') : 'MOD-AGT'}
                  </span>
                </div>
                <p className="text-[11px] text-slate-400">
                  {isDarkSuperAdmin ? t('superadmin.subtitle') : t('agent.consoleSub')}
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <nav className="flex flex-wrap items-center gap-1.5 text-xs">
                <Link
                  to="/dashboard"
                  className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-1.5 font-semibold text-slate-200 hover:bg-slate-700 transition"
                >
                  {t('nav.dashboard')}
                </Link>
                {!isDarkSuperAdmin && (
                  <Link
                    to="/agent/login"
                    className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-1.5 font-semibold text-slate-200 hover:bg-slate-700 transition"
                  >
                    {t('agent.goToLogin')}
                  </Link>
                )}
                {!isDarkSuperAdmin && (
                  <Link
                    to="/agent/register"
                    className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-1.5 font-semibold text-emerald-300 hover:bg-emerald-500/20 transition"
                  >
                    + KYC Registration
                  </Link>
                )}
                {isDarkSuperAdmin && (
                  <Link
                    to="/onboarding"
                    className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-1.5 font-semibold text-slate-200 hover:bg-slate-700 transition"
                  >
                    {t('nav.onboarding')}
                  </Link>
                )}
              </nav>

              {renderUtilityControls(true)}

              {user ? (
                <div className="flex items-center gap-2 border-l border-slate-800 pl-2">
                  <RoleBadge role={user.role} dark />
                  <button
                    type="button"
                    onClick={() => void logout()}
                    className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1.5 text-xs font-semibold text-slate-200 hover:border-rose-500/50 hover:bg-rose-950/60 hover:text-rose-200 transition"
                  >
                    <LogOut className="h-3.5 w-3.5" />
                    <span className="hidden sm:inline">{t('login.logoutBtn')}</span>
                  </button>
                </div>
              ) : (
                <Link
                  to="/login"
                  className="inline-flex items-center gap-1 rounded-lg bg-amber-500 px-3 py-1.5 text-xs font-bold text-slate-950 hover:bg-amber-400 transition"
                >
                  <LogIn className="h-3.5 w-3.5" />
                  {t('nav.login')}
                </Link>
              )}
            </div>
          </div>
        </header>

        <main id="main-content" className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">
          {children}
        </main>

        <footer className="border-t border-slate-800 bg-slate-900/70 px-4 py-3 text-[11px] text-slate-400 no-print">
          <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-2">
            <span>{t('footer.standards')}</span>
            <span>{t('footer.helpline')}</span>
          </div>
        </footer>
      </div>
    );
  }

  // Light Institutional GP ERP Shell (with Collapsible Left Sidebar + Mobile Drawer)
  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `group relative flex items-center gap-3 rounded-xl px-2.5 py-2 text-[13px] transition whitespace-nowrap ${
      isActive
        ? 'bg-brand-50 font-bold text-brand-700'
        : 'font-medium text-slate-600 hover:bg-slate-100/80 hover:text-brand-700'
    }`;

  const iconBoxClass = (isActive: boolean) =>
    `flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition ${
      isActive
        ? 'bg-brand-700 text-white shadow-xs'
        : 'bg-slate-100 text-slate-500 group-hover:bg-brand-100 group-hover:text-brand-700'
    }`;

  return (
    <div className="min-h-screen flex bg-surface text-ink">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-brand-700 focus:px-3 focus:py-2 focus:text-xs focus:font-bold focus:text-white"
      >
        {t('accessibility.skipToMain')}
      </a>

      {/* Mobile Drawer Backdrop */}
      {mobileOpen && (
        <div
          className="fixed inset-0 z-30 bg-slate-900/40 backdrop-blur-[1px] lg:hidden no-print"
          onClick={() => setMobileOpen(false)}
          aria-hidden="true"
        />
      )}

      {/* Persistent Collapsible Sidebar */}
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex flex-col justify-between border-r border-line bg-card transition-all duration-300 lg:sticky lg:top-0 lg:z-auto lg:h-screen lg:shrink-0 no-print ${
          mobileOpen ? 'translate-x-0 w-72 max-w-[85vw]' : '-translate-x-full lg:translate-x-0'
        } ${sidebarCollapsed ? 'lg:w-[76px]' : 'lg:w-[260px]'}`}
      >
        <div className="flex min-h-0 flex-1 flex-col">
          <div className="flex h-16 shrink-0 items-center justify-between gap-2 overflow-hidden border-b border-slate-100 px-3.5">
            <Link to="/" className="flex min-w-0 items-center gap-3">
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-brand-700 text-white shadow-xs">
                <Landmark className="h-5 w-5" />
              </div>
              {!sidebarCollapsed && (
                <div className="min-w-0 leading-tight">
                  <p className="truncate text-[14px] font-extrabold text-ink">{t('brand')}</p>
                  <p className="text-[11px] font-semibold text-brand-700">{t('brandSub')}</p>
                </div>
              )}
            </Link>
            <button
              type="button"
              aria-label={t('accessibility.closeMenu')}
              onClick={() => setMobileOpen(false)}
              className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100 lg:hidden"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          <nav className="flex-1 space-y-4 overflow-y-auto overflow-x-hidden px-2.5 py-4" aria-label="Primary Sidebar">
            {/* Core Group */}
            <div className="space-y-1">
              {!sidebarCollapsed && (
                <p className="px-2.5 text-[10.5px] font-bold uppercase tracking-wider text-slate-400">
                  {t('nav.sectionCore')}
                </p>
              )}
              <NavLink to="/" end className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    {isActive && (
                      <span className="absolute bottom-2 left-0 top-2 w-[3px] rounded-r-full bg-brand-700" />
                    )}
                    <span className={iconBoxClass(isActive)}>
                      <LayoutDashboard className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.dashboard')}</span>}
                  </>
                )}
              </NavLink>

              <NavLink to="/onboarding" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    {isActive && (
                      <span className="absolute bottom-2 left-0 top-2 w-[3px] rounded-r-full bg-brand-700" />
                    )}
                    <span className={iconBoxClass(isActive)}>
                      <ClipboardCheck className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.onboarding')}</span>}
                  </>
                )}
              </NavLink>

              <NavLink to="/properties" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    {isActive && (
                      <span className="absolute bottom-2 left-0 top-2 w-[3px] rounded-r-full bg-brand-700" />
                    )}
                    <span className={iconBoxClass(isActive)}>
                      <Building2 className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.properties')}</span>}
                  </>
                )}
              </NavLink>

              <NavLink to="/formula" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    {isActive && (
                      <span className="absolute bottom-2 left-0 top-2 w-[3px] rounded-r-full bg-brand-700" />
                    )}
                    <span className={iconBoxClass(isActive)}>
                      <SlidersHorizontal className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.formula')}</span>}
                  </>
                )}
              </NavLink>
            </div>

            {/* Statutory Modules (Sprint 3-5) */}
            <div className="space-y-1">
              {!sidebarCollapsed && (
                <p className="px-2.5 text-[10.5px] font-bold uppercase tracking-wider text-slate-400">
                  {t('nav.sectionStatutory')}
                </p>
              )}
              <NavLink to="/namuna-9ka/generate" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    <span className={iconBoxClass(isActive)}>
                      <FileSpreadsheet className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && (
                      <span className="flex flex-1 items-center justify-between gap-2 min-w-0">
                        <span className="truncate">{t('nav.demandBills')}</span>
                        <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[9.5px] font-semibold text-slate-500">
                          S4
                        </span>
                      </span>
                    )}
                  </>
                )}
              </NavLink>

              <NavLink to="/namuna-10/collect" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    <span className={iconBoxClass(isActive)}>
                      <ReceiptText className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && (
                      <span className="flex flex-1 items-center justify-between gap-2 min-w-0">
                        <span className="truncate">{t('nav.receipts')}</span>
                        <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[9.5px] font-semibold text-slate-500">
                          S5
                        </span>
                      </span>
                    )}
                  </>
                )}
              </NavLink>

              <NavLink to="/namuna-8/mutation/new" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    <span className={iconBoxClass(isActive)}>
                      <GitBranch className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && (
                      <span className="flex flex-1 items-center justify-between gap-2 min-w-0">
                        <span className="truncate">{t('nav.mutations')}</span>
                        <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[9.5px] font-semibold text-slate-500">
                          S3
                        </span>
                      </span>
                    )}
                  </>
                )}
              </NavLink>
            </div>

            {/* Governance & Audit */}
            <div className="space-y-1">
              {!sidebarCollapsed && (
                <p className="px-2.5 text-[10.5px] font-bold uppercase tracking-wider text-slate-400">
                  {t('nav.sectionGovernance')}
                </p>
              )}
              <NavLink to="/audit" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    {isActive && (
                      <span className="absolute bottom-2 left-0 top-2 w-[3px] rounded-r-full bg-brand-700" />
                    )}
                    <span className={iconBoxClass(isActive)}>
                      <History className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.audit')}</span>}
                  </>
                )}
              </NavLink>
            </div>

            {/* External & HQ Portals */}
            <div className="space-y-1">
              {!sidebarCollapsed && (
                <p className="px-2.5 text-[10.5px] font-bold uppercase tracking-wider text-slate-400">
                  {t('nav.sectionPortals')}
                </p>
              )}
              <NavLink to="/superadmin" className={navLinkClass} onClick={() => setMobileOpen(false)}>
                {({ isActive }) => (
                  <>
                    <span className={iconBoxClass(isActive)}>
                      <Crown className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.superadmin')}</span>}
                  </>
                )}
              </NavLink>

              <NavLink
                to={user?.role === 'Field_Agent' ? '/agent/dashboard' : '/agent/login'}
                className={navLinkClass}
                onClick={() => setMobileOpen(false)}
              >
                {({ isActive }) => (
                  <>
                    <span className={iconBoxClass(isActive)}>
                      <UserCheck className="h-[18px] w-[18px]" />
                    </span>
                    {!sidebarCollapsed && <span className="truncate">{t('nav.agentPortal')}</span>}
                  </>
                )}
              </NavLink>
            </div>
          </nav>
        </div>

        <div className="border-t border-slate-100 p-3">
          {user ? (
            <button
              type="button"
              onClick={() => void logout()}
              className="flex w-full items-center justify-center gap-2 rounded-xl border border-line bg-surface px-3 py-2 text-xs font-bold text-slate-700 hover:border-rose-300 hover:bg-rose-50 hover:text-rose-700 transition"
            >
              <LogOut className="h-4 w-4 shrink-0" />
              {!sidebarCollapsed && <span>{t('login.logoutBtn')}</span>}
            </button>
          ) : (
            <Link
              to="/login"
              onClick={() => setMobileOpen(false)}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-700 px-3 py-2 text-xs font-bold text-white hover:bg-brand-800 transition"
            >
              <ShieldCheck className="h-4 w-4 shrink-0" />
              {!sidebarCollapsed && <span>{t('nav.login')}</span>}
            </Link>
          )}
        </div>
      </aside>

      {/* Main Content Column */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between gap-3 border-b border-line bg-white/95 px-4 backdrop-blur lg:px-6 no-print">
          <div className="flex items-center gap-2.5">
            <button
              type="button"
              aria-label={t('accessibility.openMenu')}
              onClick={() => setMobileOpen(true)}
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-line bg-surface text-slate-600 hover:border-brand-700 hover:text-brand-700 lg:hidden"
            >
              <Menu className="h-[18px] w-[18px]" />
            </button>

            <button
              type="button"
              aria-label={t('accessibility.toggleSidebar')}
              aria-expanded={!sidebarCollapsed}
              onClick={() => setSidebarCollapsed((c) => !c)}
              className="hidden h-9 w-9 items-center justify-center rounded-lg border border-line bg-surface text-slate-600 hover:border-brand-700 hover:text-brand-700 lg:flex"
            >
              {sidebarCollapsed ? (
                <PanelLeftOpen className="h-[18px] w-[18px]" />
              ) : (
                <PanelLeftClose className="h-[18px] w-[18px]" />
              )}
            </button>

            <div className="hidden md:block leading-tight">
              <p className="text-xs font-bold text-ink">{t('brand')} {t('brandSub')}</p>
              <p className="text-[11px] text-muted">{t('subtitle')}</p>
            </div>
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-2.5">
            {user && user.memberships.length > 1 && (
              <label className="hidden md:flex items-center gap-1.5 text-xs">
                <span className="font-semibold text-muted">{t('nav.switchTenant')}:</span>
                <select
                  aria-label={t('nav.switchTenant')}
                  value={user.activeTenantId ?? ''}
                  disabled={switchingTenant}
                  onChange={(e) => void handleTenantChange(e.target.value)}
                  className="rounded-lg border border-line bg-surface px-2 py-1 font-mono text-xs font-semibold text-ink"
                >
                  {user.memberships.map((m) => (
                    <option key={m.tenantId} value={m.tenantId}>
                      {m.tenantId.slice(0, 8)} · {t(`roles.${m.role}`)}
                    </option>
                  ))}
                </select>
              </label>
            )}

            {renderUtilityControls(false)}

            {user ? (
              <div className="flex items-center gap-2 border-l border-line pl-2.5">
                <div className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-700 text-xs font-bold text-white">
                  {(user.fullNameEn || user.username).slice(0, 1).toUpperCase()}
                </div>
                <div className="hidden sm:block leading-tight">
                  <p className="text-xs font-bold text-ink">
                    {i18n.language === 'mr' && user.fullNameMr ? user.fullNameMr : user.fullNameEn}
                  </p>
                  <RoleBadge role={user.role} />
                </div>
              </div>
            ) : (
              <Link
                to="/login"
                className="inline-flex items-center gap-1.5 rounded-lg bg-brand-700 px-3 py-1.5 text-xs font-bold text-white hover:bg-brand-800 transition"
              >
                <LogIn className="h-3.5 w-3.5" />
                {t('nav.login')}
              </Link>
            )}
          </div>
        </header>

        <main id="main-content" className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 lg:px-6">
          {children}
        </main>

        <footer className="border-t border-line bg-white px-4 py-3 text-[11px] text-muted lg:px-6 no-print">
          <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-2">
            <span>{t('footer.standards')}</span>
            <span>{t('footer.helpline')}</span>
          </div>
        </footer>
      </div>
    </div>
  );
}
