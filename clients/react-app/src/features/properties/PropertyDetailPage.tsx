import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeft,
  Building2,
  Compass,
  Edit3,
  FileSpreadsheet,
  GitBranch,
  History,
  IndianRupee,
  Layers,
  Printer,
  ShieldCheck,
  Users,
  Wallet,
} from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { type CalculatedPropertyTaxDto } from '../../services/formulaApi';
import { namuna8Api } from '../../services/namuna8Api';
import { formatInr } from '../../utils/formatters';

export function PropertyDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();

  const canRead = Boolean(accessToken && user?.activeTenantId && id);
  const canWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor'].includes(user.role),
  );

  const detailQuery = useQuery({
    queryKey: ['property-detail', id],
    queryFn: () => namuna8Api.getPropertyDetail(id!),
    enabled: canRead,
  });

  const parsedSnapshot = useMemo<CalculatedPropertyTaxDto | null>(() => {
    const raw = detailQuery.data?.taxCalc?.calculationSnapshotJson;
    if (!raw) return null;
    try {
      return JSON.parse(raw) as CalculatedPropertyTaxDto;
    } catch {
      return null;
    }
  }, [detailQuery.data?.taxCalc?.calculationSnapshotJson]);

  if (!accessToken) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <p>{t('properties.signInRequired')}</p>
        <Link to="/login" className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white">
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  if (detailQuery.isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-32 animate-pulse rounded-2xl bg-slate-200" />
        <div className="h-64 animate-pulse rounded-2xl bg-slate-200" />
      </div>
    );
  }

  const prop = detailQuery.data;
  if (!prop) {
    return (
      <div className="rounded-2xl border border-rose-200 bg-rose-50 p-6 text-xs text-rose-900 space-y-3">
        <p className="font-bold">{t('properties.notFound')}</p>
        <Link to="/properties" className="inline-block rounded-lg bg-brand-700 px-3 py-1.5 font-bold text-white">
          {t('properties.backToHub')}
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-5 text-xs">
      {/* Header Summary Card */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <Link
              to="/properties"
              className="inline-flex items-center gap-1 text-[11px] font-bold text-brand-700 hover:underline"
            >
              <ArrowLeft className="h-3 w-3" />
              {t('properties.backToHub')}
            </Link>
            <div className="flex flex-wrap items-center gap-2">
              <Building2 className="h-5 w-5 text-brand-700" />
              <h1 className="text-xl font-extrabold text-ink">
                {t('properties.view360Title')} — {prop.wardNo} / {prop.propertyNo}
              </h1>
              <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-[11px] font-bold text-emerald-800">
                {prop.status}
              </span>
              {prop.isTaxExempt && (
                <span className="rounded-full border border-amber-200 bg-amber-50 px-2.5 py-0.5 text-[11px] font-bold text-amber-900">
                  {t('properties.exemptBadge')}
                </span>
              )}
            </div>
            <p className="text-xs text-muted">
              {[prop.citySurveyOrGutNo, prop.streetOrLaneName, prop.propertyDescription]
                .filter(Boolean)
                .join(' · ') || '—'}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {canWrite && (
              <Link
                to={`/namuna-8/edit/${prop.id}`}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-3.5 py-2 font-bold text-white hover:bg-brand-800"
              >
                <Edit3 className="h-3.5 w-3.5" />
                {t('formula.editRowBtn')}
              </Link>
            )}
            <Link
              to={`/namuna-8/print?propertyId=${encodeURIComponent(prop.id)}`}
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3 py-2 font-bold text-slate-700 hover:bg-brand-50"
            >
              <Printer className="h-3.5 w-3.5" />
              {t('properties.printExtractBtn')}
            </Link>
            {prop.demand && (
              <Link
                to={`/namuna-9ka/generate?propertyId=${encodeURIComponent(prop.id)}`}
                className="inline-flex items-center gap-1.5 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 font-bold text-emerald-800 hover:bg-emerald-100"
              >
                <FileSpreadsheet className="h-3.5 w-3.5" />
                {t('namuna9Ka.viewDemandBillBtn')}
              </Link>
            )}
            {prop.demand && prop.demand.pendingBalance > 0 && (
              <Link
                to={`/namuna-10/collect?propertyId=${encodeURIComponent(prop.id)}`}
                className="inline-flex items-center gap-1.5 rounded-xl border border-indigo-200 bg-indigo-50 px-3 py-2 font-bold text-indigo-800 hover:bg-indigo-100"
              >
                <Wallet className="h-3.5 w-3.5" />
                {t('namuna10.collectTaxBtn')}
              </Link>
            )}
            <Link
              to={`/namuna-8/partition/${prop.id}`}
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3 py-2 font-bold text-slate-700 hover:bg-brand-50"
            >
              <GitBranch className="h-3.5 w-3.5" />
              {t('properties.partitionBtn')}
            </Link>
            <Link
              to={`/audit/Property/${prop.id}`}
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3 py-2 font-bold text-brand-700 hover:bg-brand-50"
            >
              <History className="h-3.5 w-3.5" />
              {t('properties.auditHistory')}
            </Link>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="rounded-xl border border-line bg-surface p-3">
            <p className="text-[11px] font-semibold text-muted">{t('properties.totalArea')}</p>
            <p className="mt-0.5 text-base font-extrabold text-ink mono">
              {prop.totalAreaSqFt} {t('dashboard.sqFtUnit')}
            </p>
            <p className="text-[10.5px] text-muted mono">
              ({prop.builtUpAreaSqFt} + {prop.openPlotAreaSqFt})
            </p>
          </div>

          <div className="rounded-xl border border-line bg-surface p-3">
            <p className="text-[11px] font-semibold text-muted">{t('properties.waterConnection')}</p>
            <p className="mt-0.5 text-base font-extrabold text-ink mono">{prop.waterConnectionCode}</p>
            {prop.occupierName && (
              <p className="text-[10.5px] text-muted">
                {t('properties.occupierName')}: {prop.occupierName}
              </p>
            )}
          </div>

          <div className="rounded-xl border border-brand-200 bg-brand-50/50 p-3">
            <p className="text-[11px] font-semibold text-brand-800">{t('formula.simTotalTax')}</p>
            <p className="mt-0.5 text-xl font-extrabold text-brand-900 mono">
              {prop.taxCalc ? formatInr(prop.taxCalc.totalTax) : '—'}
            </p>
            <p className="text-[10.5px] text-brand-700">
              {prop.taxCalc?.assessmentYearLabel || ''}
            </p>
          </div>

          <div className="rounded-xl border border-line bg-surface p-3">
            <p className="text-[11px] font-semibold text-muted">{t('properties.demandStatusTitle')}</p>
            <p className="mt-0.5 text-base font-extrabold text-ink mono">
              {prop.demand ? formatInr(prop.demand.pendingBalance) : '—'}
            </p>
            {prop.demand && (
              <span className="inline-block mt-0.5 rounded-full bg-amber-50 border border-amber-200 px-2 py-0.5 text-[10px] font-bold text-amber-900">
                {prop.demand.status}
              </span>
            )}
          </div>
        </div>
      </section>

      <div className="grid gap-5 lg:grid-cols-2">
        {/* Owners List */}
        <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-3">
          <div className="flex items-center gap-2 border-b border-line pb-2">
            <Users className="h-4 w-4 text-brand-700" />
            <h2 className="text-sm font-extrabold text-ink">{t('properties.secOwners')}</h2>
          </div>
          {prop.owners.length === 0 ? (
            <p className="text-muted">{t('properties.unassignedOwner')}</p>
          ) : (
            <div className="space-y-2">
              {prop.owners.map((o) => (
                <div
                  key={o.id}
                  className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface p-3"
                >
                  <div>
                    <div className="font-bold text-ink flex items-center gap-1.5">
                      <span>{o.fullNameMr}</span>
                      {o.isPrimaryOwner && (
                        <span className="rounded-full bg-brand-700 px-2 py-0.5 text-[10px] font-bold text-white">
                          {t('properties.primaryBadge')}
                        </span>
                      )}
                    </div>
                    <div className="text-[11px] text-muted mono">
                      {o.fullNameEn} · {o.citizenCode} · {o.aadhaarMasked || '—'} · {o.mobileNo || '—'}
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="mono font-extrabold text-brand-800">{o.ownershipSharePct}%</div>
                    <div className="text-[10.5px] text-muted">{t(`citizens.exemption.${o.exemptionCategory}`)}</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* Directional Boundaries */}
        <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-3">
          <div className="flex items-center gap-2 border-b border-line pb-2">
            <Compass className="h-4 w-4 text-brand-700" />
            <h2 className="text-sm font-extrabold text-ink">{t('properties.boundariesTitle')}</h2>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-xl border border-line bg-surface p-3">
              <p className="text-[11px] font-bold text-muted">{t('properties.east')}</p>
              <p className="mt-0.5 font-semibold text-ink">{prop.boundaryEast || '—'}</p>
            </div>
            <div className="rounded-xl border border-line bg-surface p-3">
              <p className="text-[11px] font-bold text-muted">{t('properties.west')}</p>
              <p className="mt-0.5 font-semibold text-ink">{prop.boundaryWest || '—'}</p>
            </div>
            <div className="rounded-xl border border-line bg-surface p-3">
              <p className="text-[11px] font-bold text-muted">{t('properties.north')}</p>
              <p className="mt-0.5 font-semibold text-ink">{prop.boundaryNorth || '—'}</p>
            </div>
            <div className="rounded-xl border border-line bg-surface p-3">
              <p className="text-[11px] font-bold text-muted">{t('properties.south')}</p>
              <p className="mt-0.5 font-semibold text-ink">{prop.boundarySouth || '—'}</p>
            </div>
          </div>
        </section>
      </div>

      {/* Multi-Floor Construction & Capital Valuation Breakdown */}
      <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-3">
        <div className="flex items-center gap-2 border-b border-line pb-2">
          <Layers className="h-4 w-4 text-brand-700" />
          <h2 className="text-sm font-extrabold text-ink">{t('properties.secFloors')}</h2>
        </div>

        <div className="overflow-x-auto rounded-xl border border-line">
          <table className="w-full text-left text-xs">
            <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
              <tr>
                <th className="px-3 py-2">{t('properties.floorLabel')}</th>
                <th className="px-3 py-2">{t('formula.colTypeName')}</th>
                <th className="px-3 py-2">{t('formula.colUsageName')}</th>
                <th className="px-3 py-2 text-right">{t('formula.simBuiltYear')}</th>
                <th className="px-3 py-2 text-right">L x W</th>
                <th className="px-3 py-2 text-right">{t('dashboard.sqFtUnit')} ({t('dashboard.sqMUnit')})</th>
                <th className="px-3 py-2 text-right">{t('formula.simCapitalVal')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {prop.constructions.map((f, idx) => {
                const breakdown = parsedSnapshot?.calculation?.floorBreakdowns?.[idx];
                return (
                  <tr key={f.id}>
                    <td className="px-3 py-2 font-bold text-ink">{f.floorLabel}</td>
                    <td className="px-3 py-2 mono font-semibold">{f.constructionTypeCode}</td>
                    <td className="px-3 py-2 mono">{f.usageCode}</td>
                    <td className="px-3 py-2 text-right mono">{f.constructionStartYear}</td>
                    <td className="px-3 py-2 text-right mono">
                      {f.lengthFt} x {f.widthFt}
                    </td>
                    <td className="px-3 py-2 text-right mono font-bold">
                      {f.areaSqFt} ({f.areaSqM})
                    </td>
                    <td className="px-3 py-2 text-right mono font-bold text-brand-800">
                      {breakdown ? formatInr(breakdown.floorCapitalValue) : '—'}
                    </td>
                  </tr>
                );
              })}
              {prop.constructions.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-3 py-5 text-center text-muted">
                    {t('properties.noFloorsOpenPlot')}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* Head-Wise Tax Assessment Card */}
      {prop.taxCalc && (
        <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-3">
          <div className="flex items-center justify-between border-b border-line pb-2">
            <div className="flex items-center gap-2">
              <IndianRupee className="h-4 w-4 text-brand-700" />
              <h2 className="text-sm font-extrabold text-ink">
                {t('properties.taxLedgerTitle')} ({prop.taxCalc.assessmentYearLabel})
              </h2>
            </div>
            <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-800">
              <ShieldCheck className="h-3.5 w-3.5" />
              ADR-016 Snapshot
            </span>
          </div>

          <div className="grid gap-2.5 sm:grid-cols-3 lg:grid-cols-9">
            {[
              { label: t('formula.simBuildingTax'), val: prop.taxCalc.buildingTax },
              { label: t('formula.simOpenLandTax'), val: prop.taxCalc.openLandTax },
              { label: t('formula.colLightingTax'), val: prop.taxCalc.lightingTax },
              { label: t('formula.colHealthTax'), val: prop.taxCalc.healthTax },
              { label: t('formula.colSanitationTax'), val: prop.taxCalc.sanitationTax },
              { label: t('formula.colToiletTax'), val: prop.taxCalc.toiletTax },
              { label: t('formula.tabWaterTax'), val: prop.taxCalc.waterTax },
              { label: t('formula.simOtherTax'), val: prop.taxCalc.otherTax },
              { label: t('formula.simTotalTax'), val: prop.taxCalc.totalTax, highlight: true },
            ].map((item) => (
              <div
                key={item.label}
                className={`rounded-xl border p-2.5 ${
                  item.highlight
                    ? 'border-brand-700 bg-brand-700 text-white'
                    : 'border-line bg-surface text-ink'
                }`}
              >
                <p className={`text-[10px] font-semibold ${item.highlight ? 'text-brand-100' : 'text-muted'}`}>
                  {item.label}
                </p>
                <p className="mt-0.5 mono text-sm font-extrabold">{formatInr(item.val)}</p>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

