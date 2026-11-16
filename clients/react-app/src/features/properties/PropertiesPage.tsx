import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, History, Plus, Search, X } from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { type CreatePropertyPayload, sprint2Api } from '../../services/sprint2Api';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';

export function PropertiesPage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [filterTerm, setFilterTerm] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formErrorItems, setFormErrorItems] = useState<ParsedValidationItem[]>([]);

  const [form, setForm] = useState<CreatePropertyPayload>({
    wardNo: 'W-01',
    propertyNo: '',
    builtUpAreaSqFt: 450,
    openPlotAreaSqFt: 100,
    boundaryEast: '',
    boundaryWest: '',
    boundaryNorth: '',
    boundarySouth: '',
    waterConnectionCode: 'NONE',
    isTaxExempt: false,
  });

  const canRead = Boolean(accessToken && user?.activeTenantId);
  const propertiesQuery = useQuery({
    queryKey: ['properties', user?.activeTenantId],
    queryFn: sprint2Api.listProperties,
    enabled: canRead,
  });

  const filteredProperties = useMemo(() => {
    const items = propertiesQuery.data ?? [];
    const q = filterTerm.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (p) =>
        p.wardNo.toLowerCase().includes(q) ||
        p.propertyNo.toLowerCase().includes(q) ||
        p.waterConnectionCode.toLowerCase().includes(q),
    );
  }, [propertiesQuery.data, filterTerm]);

  async function handleCreateSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setFormErrorItems([]);

    const clientItems: ParsedValidationItem[] = [];
    if (!form.wardNo.trim()) {
      clientItems.push({ key: 'wardNo', rawField: 'WardNo', message: t('validation.required') });
    }
    if (!form.propertyNo.trim()) {
      clientItems.push({ key: 'propertyNo', rawField: 'PropertyNo', message: t('validation.required') });
    }
    if ((Number(form.builtUpAreaSqFt) || 0) + (Number(form.openPlotAreaSqFt) || 0) <= 0) {
      clientItems.push({
        key: 'totalArea',
        rawField: 'TotalArea',
        message: t('validation.propertyAreaPositive'),
      });
    }
    if (clientItems.length > 0) {
      setFormError(t('validation.summaryTitle'));
      setFormErrorItems(clientItems);
      return;
    }

    setSubmitting(true);
    try {
      await sprint2Api.createProperty({
        ...form,
        wardNo: form.wardNo.trim(),
        propertyNo: form.propertyNo.trim(),
        boundaryEast: form.boundaryEast?.trim() || null,
        boundaryWest: form.boundaryWest?.trim() || null,
        boundaryNorth: form.boundaryNorth?.trim() || null,
        boundarySouth: form.boundarySouth?.trim() || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['properties', user?.activeTenantId] });
      await queryClient.invalidateQueries({ queryKey: ['dashboard', 'summary', user?.activeTenantId] });
      setModalOpen(false);
      setForm((prev) => ({ ...prev, propertyNo: '' }));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('onboarding.submitFailed'));
      setFormError(parsed.summary);
      setFormErrorItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  }

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

  return (
    <div className="space-y-5">
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-xl font-extrabold text-ink">{t('properties.title')}</h1>
            <p className="text-xs text-muted">{t('properties.subtitle')}</p>
          </div>
          <button
            type="button"
            onClick={() => {
              setFormError(null);
              setModalOpen(true);
            }}
            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2.5 text-xs font-bold text-white hover:bg-brand-800 transition shadow-xs"
          >
            <Plus className="h-4 w-4" />
            {t('properties.addBtn')}
          </button>
        </div>

        <div className="relative max-w-sm">
          <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <input
            type="text"
            aria-label={t('properties.searchPlaceholder')}
            value={filterTerm}
            onChange={(e) => setFilterTerm(e.target.value)}
            placeholder={t('properties.searchPlaceholder')}
            className="w-full rounded-xl border border-line bg-surface pl-9 pr-3 py-2 text-xs focus:bg-white focus:border-brand-600"
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
              <thead className="bg-thead text-[11px] font-bold uppercase tracking-wide text-brand-900 border-b border-line">
                <tr>
                  <th className="px-4 py-3">{t('properties.wardNo')}</th>
                  <th className="px-4 py-3">{t('properties.propertyNo')}</th>
                  <th className="px-4 py-3 text-right">{t('properties.builtUpArea')}</th>
                  <th className="px-4 py-3 text-right">{t('properties.openPlotArea')}</th>
                  <th className="px-4 py-3 text-right">{t('properties.totalArea')}</th>
                  <th className="px-4 py-3">{t('properties.waterConnection')}</th>
                  <th className="px-4 py-3">{t('properties.exempt')}</th>
                  <th className="px-4 py-3 text-right">{t('properties.actions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredProperties.map((p) => (
                  <tr key={p.id} className="odd:bg-white even:bg-slate-50/70 hover:bg-brand-50/40">
                    <td className="px-4 py-3 mono font-bold text-brand-800">{p.wardNo}</td>
                    <td className="px-4 py-3 mono font-bold text-ink">{p.propertyNo}</td>
                    <td className="px-4 py-3 mono text-right">{p.builtUpAreaSqFt}</td>
                    <td className="px-4 py-3 mono text-right">{p.openPlotAreaSqFt}</td>
                    <td className="px-4 py-3 mono font-bold text-right">{p.totalAreaSqFt}</td>
                    <td className="px-4 py-3">
                      <span className="rounded-md border border-sky-200 bg-sky-50 px-2 py-0.5 mono text-[10.5px] font-bold text-sky-800">
                        {p.waterConnectionCode}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-semibold">
                      {p.isTaxExempt ? t('properties.yes') : t('properties.no')}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <Link
                        to={`/audit/Property/${p.id}`}
                        className="inline-flex items-center gap-1 rounded-lg border border-line bg-white px-2.5 py-1 text-[11px] font-semibold text-brand-700 hover:bg-brand-50"
                      >
                        <History className="h-3 w-3" />
                        {t('properties.auditTrailBtn')}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {modalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-xs"
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-prop-title"
        >
          <div className="w-full max-w-2xl rounded-2xl border border-line bg-white p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <h2 id="create-prop-title" className="text-base font-extrabold text-ink">
                {t('properties.modalTitle')}
              </h2>
              <button
                type="button"
                aria-label={t('properties.cancel')}
                onClick={() => setModalOpen(false)}
                className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <ValidationSummary summary={formError} items={formErrorItems} />

            <form onSubmit={(e) => void handleCreateSubmit(e)} className="space-y-4 text-xs">
              <div className="grid gap-3 sm:grid-cols-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>
                    {t('properties.wardNo')} <span className="text-rose-600">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.wardNo}
                    onChange={(e) => setForm((p) => ({ ...p, wardNo: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>
                    {t('properties.propertyNo')} <span className="text-rose-600">*</span>
                  </span>
                  <input
                    type="text"
                    required
                    value={form.propertyNo}
                    onChange={(e) => setForm((p) => ({ ...p, propertyNo: e.target.value }))}
                    placeholder="e.g. 102"
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('properties.waterConnection')}</span>
                  <select
                    value={form.waterConnectionCode}
                    onChange={(e) => setForm((p) => ({ ...p, waterConnectionCode: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                  >
                    <option value="NONE">{t('properties.waterNone')}</option>
                    <option value="GENERAL">{t('properties.waterGeneral')}</option>
                    <option value="SPECIAL">{t('properties.waterSpecial')}</option>
                  </select>
                </label>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('properties.builtUpArea')}</span>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={form.builtUpAreaSqFt}
                    onChange={(e) =>
                      setForm((p) => ({ ...p, builtUpAreaSqFt: Number(e.target.value) }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('properties.openPlotArea')}</span>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={form.openPlotAreaSqFt}
                    onChange={(e) =>
                      setForm((p) => ({ ...p, openPlotAreaSqFt: Number(e.target.value) }))
                    }
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>

              <div className="space-y-2 border-t border-slate-100 pt-3">
                <p className="font-bold text-slate-700">{t('properties.boundariesTitle')}</p>
                <div className="grid gap-2.5 sm:grid-cols-4">
                  <input
                    type="text"
                    aria-label={t('properties.east')}
                    placeholder={t('properties.east')}
                    value={form.boundaryEast ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, boundaryEast: e.target.value }))}
                    className="rounded-lg border border-slate-300 px-3 py-2"
                  />
                  <input
                    type="text"
                    aria-label={t('properties.west')}
                    placeholder={t('properties.west')}
                    value={form.boundaryWest ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, boundaryWest: e.target.value }))}
                    className="rounded-lg border border-slate-300 px-3 py-2"
                  />
                  <input
                    type="text"
                    aria-label={t('properties.north')}
                    placeholder={t('properties.north')}
                    value={form.boundaryNorth ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, boundaryNorth: e.target.value }))}
                    className="rounded-lg border border-slate-300 px-3 py-2"
                  />
                  <input
                    type="text"
                    aria-label={t('properties.south')}
                    placeholder={t('properties.south')}
                    value={form.boundarySouth ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, boundarySouth: e.target.value }))}
                    className="rounded-lg border border-slate-300 px-3 py-2"
                  />
                </div>
              </div>

              <label className="inline-flex items-center gap-2 font-semibold text-slate-700 cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.isTaxExempt}
                  onChange={(e) => setForm((p) => ({ ...p, isTaxExempt: e.target.checked }))}
                  className="h-4 w-4 rounded border-slate-300 text-brand-700"
                />
                <span>{t('properties.exempt')}</span>
              </label>

              <div className="flex justify-end gap-2 border-t border-slate-100 pt-3">
                <button
                  type="button"
                  onClick={() => setModalOpen(false)}
                  className="rounded-xl border border-slate-300 px-4 py-2 font-bold text-slate-700 hover:bg-slate-100"
                >
                  {t('properties.cancel')}
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="rounded-xl bg-brand-700 px-5 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
                >
                  {submitting ? t('onboarding.loading') : t('properties.submitCreate')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

