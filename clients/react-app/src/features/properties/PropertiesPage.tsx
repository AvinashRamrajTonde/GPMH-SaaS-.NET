import { useQuery } from '@tanstack/react-query';
import {
  Building2,
  Edit3,
  Eye,
  GitBranch,
  History,
  IndianRupee,
  Layers,
  MapPin,
  Plus,
  Printer,
  Search,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { namuna8Api } from '../../services/namuna8Api';
import { formatInr } from '../../utils/formatters';

export function PropertiesPage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();

  const [filterTerm, setFilterTerm] = useState('');
  const [wardFilter, setWardFilter] = useState('');

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor', 'Auditor'].includes(user.role),
  );
  const canWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor'].includes(user.role),
  );

  const propertiesQuery = useQuery({
    queryKey: ['properties', user?.activeTenantId],
    queryFn: namuna8Api.listProperties,
    enabled: canRead,
  });

  const kpisQuery = useQuery({
    queryKey: ['properties-hub-summary', user?.activeTenantId],
    queryFn: namuna8Api.getHubSummary,
    enabled: canRead,
  });

  const filteredProperties = useMemo(() => {
    const items = propertiesQuery.data ?? [];
    const q = filterTerm.trim().toLowerCase();
    const wq = wardFilter.trim().toLowerCase();
    return items.filter((p) => {
      if (wq && !p.wardNo.toLowerCase().includes(wq)) {
        return false;
      }
      if (!q) return true;
      return (
        p.wardNo.toLowerCase().includes(q) ||
        p.propertyNo.toLowerCase().includes(q) ||
        (p.primaryOwnerNameMr ?? '').toLowerCase().includes(q) ||
        (p.primaryOwnerNameEn ?? '').toLowerCase().includes(q) ||
        (p.citySurveyOrGutNo ?? '').toLowerCase().includes(q) ||
        p.waterConnectionCode.toLowerCase().includes(q)
      );
    });
  }, [propertiesQuery.data, filterTerm, wardFilter]);

  if (!canRead) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <div className="flex items-center gap-2 font-bold text-sm">
          <Building2 className="h-5 w-5 text-brand-700" />
          <h1>{t('properties.title')}</h1>
        </div>
        <p>{t('properties.signInRequired')}</p>
        <Link
          to="/login"
          className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800"
        >
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  const kpis = kpisQuery.data;

  return (
    <div className="space-y-5">
      {/* KPI Summary Strip */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="rounded-2xl border border-line bg-card p-4 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('properties.kpiTotal')}</p>
            <p className="mt-1 text-2xl font-extrabold text-ink mono">
              {kpis?.totalProperties ?? propertiesQuery.data?.length ?? 0}
            </p>
          </div>
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-50 text-brand-700">
            <Building2 className="h-5 w-5" />
          </div>
        </div>

        <div className="rounded-2xl border border-line bg-card p-4 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('properties.kpiAssessedDemand')}</p>
            <p className="mt-1 text-2xl font-extrabold text-brand-800 mono">
              {formatInr(kpis?.totalAssessedDemand ?? 0)}
            </p>
          </div>
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50 text-emerald-700">
            <IndianRupee className="h-5 w-5" />
          </div>
        </div>

        <div className="rounded-2xl border border-line bg-card p-4 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('properties.kpiExempt')}</p>
            <p className="mt-1 text-2xl font-extrabold text-ink mono">
              {kpis?.exemptPropertiesCount ?? 0}
            </p>
          </div>
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-50 text-amber-700">
            <ShieldCheck className="h-5 w-5" />
          </div>
        </div>

        <div className="rounded-2xl border border-line bg-card p-4 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('properties.kpiWards')}</p>
            <p className="mt-1 text-2xl font-extrabold text-ink mono">
              {kpis?.activeWardsCount ?? 0}
            </p>
          </div>
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-sky-50 text-sky-700">
            <MapPin className="h-5 w-5" />
          </div>
        </div>
      </div>

      {/* Main Namuna 8 Register Hub Card */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-xl font-extrabold text-ink">{t('properties.title')}</h1>
            <p className="text-xs text-muted">{t('properties.subtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Link
              to="/citizens"
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3.5 py-2 text-xs font-bold text-slate-700 hover:border-brand-300 hover:bg-brand-50 hover:text-brand-800"
            >
              <Users className="h-4 w-4" />
              {t('nav.citizens')}
            </Link>
            <Link
              to="/namuna-8/print"
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3.5 py-2 text-xs font-bold text-slate-700 hover:border-brand-300 hover:bg-brand-50 hover:text-brand-800"
            >
              <Printer className="h-4 w-4" />
              {t('nav.namuna8Print')}
            </Link>
            {canWrite && (
              <Link
                to="/namuna-8/add"
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2.5 text-xs font-bold text-white hover:bg-brand-800 transition shadow-xs"
              >
                <Plus className="h-4 w-4" />
                {t('properties.addBtn')}
              </Link>
            )}
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-[1fr_180px] text-xs">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              type="search"
              aria-label={t('properties.searchPlaceholder')}
              value={filterTerm}
              onChange={(e) => setFilterTerm(e.target.value)}
              placeholder={t('properties.searchPlaceholder')}
              className="w-full rounded-xl border border-line bg-surface pl-9 pr-3 py-2 focus:bg-white focus:border-brand-600 focus:outline-none"
            />
          </div>

          <input
            type="text"
            value={wardFilter}
            onChange={(e) => setWardFilter(e.target.value)}
            placeholder={t('citizens.filterWardPlaceholder')}
            className="rounded-xl border border-line bg-surface px-3 py-2 mono focus:bg-white focus:border-brand-600 focus:outline-none"
          />
        </div>

        {propertiesQuery.isLoading && (
          <div className="space-y-2">
            {[1, 2, 3].map((n) => (
              <div key={n} className="h-12 animate-pulse rounded-xl bg-slate-200" />
            ))}
          </div>
        )}

        {propertiesQuery.error instanceof ApiProblemError && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs text-rose-800">
            {propertiesQuery.error.message}
          </div>
        )}

        {!propertiesQuery.isLoading && filteredProperties.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-300 bg-surface p-8 text-center space-y-2">
            <Building2 className="mx-auto h-8 w-8 text-slate-400" />
            <p className="text-sm font-bold text-ink">{t('properties.emptyTitle')}</p>
            <p className="text-xs text-muted">{t('properties.emptyDesc')}</p>
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line">
            <table className="min-w-full border-collapse text-left text-xs">
              <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase tracking-wide text-brand-900 border-b border-line">
                <tr>
                  <th className="px-3.5 py-3">{t('properties.colWardProp')}</th>
                  <th className="px-3.5 py-3">{t('properties.colOwner')}</th>
                  <th className="px-3.5 py-3 text-right">{t('properties.colFloorsArea')}</th>
                  <th className="px-3.5 py-3">{t('properties.waterConnection')}</th>
                  <th className="px-3.5 py-3 text-right">{t('properties.colAnnualTax')}</th>
                  <th className="px-3.5 py-3 text-right">{t('properties.actions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredProperties.map((p) => (
                  <tr key={p.id} className="odd:bg-card even:bg-slate-50/70 hover:bg-brand-50/40">
                    <td className="px-3.5 py-3">
                      <div className="flex items-center gap-1.5">
                        <span className="mono font-bold text-brand-800">{p.wardNo}</span>
                        <span className="text-slate-400">/</span>
                        <Link
                          to={`/namuna-8/view/${p.id}`}
                          className="mono font-extrabold text-ink hover:text-brand-700 hover:underline"
                        >
                          {p.propertyNo}
                        </Link>
                        {p.status !== 'Active' && (
                          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold text-amber-900">
                            {p.status}
                          </span>
                        )}
                      </div>
                      {(p.citySurveyOrGutNo || p.streetOrLaneName) && (
                        <div className="text-[11px] text-muted">
                          {[p.citySurveyOrGutNo, p.streetOrLaneName].filter(Boolean).join(' · ')}
                        </div>
                      )}
                    </td>

                    <td className="px-3.5 py-3">
                      {p.primaryOwnerNameMr ? (
                        <div>
                          <div className="font-bold text-ink">{p.primaryOwnerNameMr}</div>
                          <div className="text-[11px] text-muted">{p.primaryOwnerNameEn}</div>
                        </div>
                      ) : (
                        <span className="text-muted italic">{t('properties.unassignedOwner')}</span>
                      )}
                    </td>

                    <td className="px-3.5 py-3 text-right">
                      <div className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[10.5px] font-bold text-slate-700">
                        <Layers className="h-3 w-3" />
                        {p.floorCount} {t('properties.floorsUnit')}
                      </div>
                      <div className="mt-0.5 mono font-bold text-ink">
                        {p.totalAreaSqFt} {t('dashboard.sqFtUnit')}
                      </div>
                      <div className="text-[10.5px] text-muted mono">
                        ({p.builtUpAreaSqFt} + {p.openPlotAreaSqFt})
                      </div>
                    </td>

                    <td className="px-3.5 py-3 space-y-1">
                      <span className="inline-block rounded-md border border-sky-200 bg-sky-50 px-2 py-0.5 mono text-[10.5px] font-bold text-sky-800">
                        {p.waterConnectionCode}
                      </span>
                      {p.isTaxExempt && (
                        <div className="inline-block ml-1 rounded-md border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-bold text-amber-900">
                          {t('properties.exemptBadge')}
                        </div>
                      )}
                    </td>

                    <td className="px-3.5 py-3 text-right">
                      {p.totalAnnualTax !== null && p.totalAnnualTax !== undefined ? (
                        <div>
                          <div className="mono font-extrabold text-brand-800">
                            {formatInr(p.totalAnnualTax)}
                          </div>
                          {p.pendingBalance !== null && p.pendingBalance !== undefined && (
                            <div className="text-[10.5px] text-muted mono">
                              {t('properties.pendingPrefix')}: {formatInr(p.pendingBalance)}
                            </div>
                          )}
                        </div>
                      ) : (
                        <span className="text-muted text-[11px]">—</span>
                      )}
                    </td>

                    <td className="px-3.5 py-3 text-right">
                      <div className="inline-flex flex-wrap items-center justify-end gap-1.5">
                        <Link
                          to={`/namuna-8/view/${p.id}`}
                          className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-800 hover:bg-brand-100"
                        >
                          <Eye className="h-3 w-3" />
                          {t('properties.view360Btn')}
                        </Link>
                        {canWrite && (
                          <Link
                            to={`/namuna-8/edit/${p.id}`}
                            className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2 py-1 text-[11px] font-semibold text-slate-700 hover:bg-slate-50"
                            title={t('formula.editRowBtn')}
                          >
                            <Edit3 className="h-3 w-3" />
                          </Link>
                        )}
                        <Link
                          to={`/namuna-8/print?propertyId=${encodeURIComponent(p.id)}`}
                          className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2 py-1 text-[11px] font-semibold text-slate-700 hover:bg-slate-50"
                          title={t('properties.printExtractBtn')}
                        >
                          <Printer className="h-3 w-3" />
                        </Link>
                        <Link
                          to={`/namuna-8/partition/${p.id}`}
                          className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2 py-1 text-[11px] font-semibold text-slate-700 hover:bg-slate-50"
                          title={t('properties.partitionBtn')}
                        >
                          <GitBranch className="h-3 w-3" />
                        </Link>
                        <Link
                          to={`/audit/Property/${p.id}`}
                          className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2 py-1 text-[11px] font-semibold text-brand-700 hover:bg-brand-50"
                          title={t('properties.auditHistory')}
                        >
                          <History className="h-3 w-3" />
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
