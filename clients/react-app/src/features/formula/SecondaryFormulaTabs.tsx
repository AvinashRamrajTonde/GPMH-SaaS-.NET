import { Edit3, History, Plus, Save, Trash2, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import {
  type AreaSlabTaxDto,
  type DepreciationSlabDto,
  formulaApi,
  type SaveUsageWeightagePayload,
  type SaveWaterTaxMasterPayload,
  type UsageWeightageDto,
  type WaterTaxMasterDto,
} from '../../services/formulaApi';
import { formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';

interface DepreciationSlabsTabProps {
  slabs: DepreciationSlabDto[];
  canWrite: boolean;
  onSaved: (message: string) => Promise<void>;
}

interface EditableDepRow {
  id?: string;
  ageFrom: number;
  ageTo: number;
  hutMudPercent: number;
  stoneBrickRccPercent: number;
  lastResolutionNo?: string | null;
}

export function DepreciationSlabsTab({ slabs, canWrite, onSaved }: DepreciationSlabsTabProps) {
  const { t } = useTranslation();
  const [rows, setRows] = useState<EditableDepRow[]>(() =>
    slabs.map((s) => ({
      id: s.id,
      ageFrom: s.ageFrom,
      ageTo: s.ageTo,
      hutMudPercent: s.hutMudPercent,
      stoneBrickRccPercent: s.stoneBrickRccPercent,
      lastResolutionNo: s.lastResolutionNo,
    })),
  );
  const [resolutionNo, setResolutionNo] = useState('');
  const [resolutionDate, setResolutionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [changeReason, setChangeReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [errorSummary, setErrorSummary] = useState<string | null>(null);
  const [errorItems, setErrorItems] = useState<ParsedValidationItem[]>([]);

  function handleAddSlab() {
    const prevTo = rows.length > 0 ? rows[rows.length - 1].ageTo : 0;
    setRows((prev) => [
      ...prev,
      {
        ageFrom: prevTo,
        ageTo: prevTo + 10,
        hutMudPercent: 15,
        stoneBrickRccPercent: 30,
      },
    ]);
  }

  function handleRemoveSlab(index: number) {
    setRows((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorSummary(null);
    setErrorItems([]);

    const clientErrors: ParsedValidationItem[] = [];
    const ordered = [...rows].sort((a, b) => a.ageFrom - b.ageFrom || a.ageTo - b.ageTo);

    if (ordered.length === 0) {
      clientErrors.push({ key: 'slabs', rawField: 'Slabs', message: t('formula.errSlabsEmpty') });
    } else {
      if (ordered[0].ageFrom !== 0) {
        clientErrors.push({ key: 'slabs', rawField: 'Slabs', message: t('formula.errDepStartZero') });
      }
      for (let i = 0; i < ordered.length; i += 1) {
        const cur = ordered[i];
        if (cur.ageTo <= cur.ageFrom) {
          clientErrors.push({ key: `slabs.${i}`, rawField: 'Slabs', message: t('formula.errDepRangeOrder') });
          break;
        }
        if (
          cur.hutMudPercent < 0 ||
          cur.hutMudPercent > 100 ||
          cur.stoneBrickRccPercent < 0 ||
          cur.stoneBrickRccPercent > 100
        ) {
          clientErrors.push({ key: `slabs.${i}`, rawField: 'Slabs', message: t('formula.errPctRange') });
          break;
        }
        if (i > 0 && cur.ageFrom !== ordered[i - 1].ageTo) {
          clientErrors.push({
            key: `slabs.${i}`,
            rawField: 'Slabs',
            message: t('formula.errDepContiguous', { from: cur.ageFrom, prevTo: ordered[i - 1].ageTo }),
          });
          break;
        }
      }
      if (ordered[ordered.length - 1].ageTo < 100) {
        clientErrors.push({ key: 'slabs', rawField: 'Slabs', message: t('formula.errDepEnd100') });
      }
    }

    if (!resolutionNo.trim()) {
      clientErrors.push({ key: 'resolutionNo', rawField: 'ResolutionNo', message: t('formula.errResolutionNoRequired') });
    }
    if (!changeReason.trim()) {
      clientErrors.push({ key: 'changeReason', rawField: 'ChangeReason', message: t('formula.errReasonRequired') });
    }

    if (clientErrors.length > 0) {
      setErrorSummary(t('validation.summaryTitle'));
      setErrorItems(clientErrors);
      return;
    }

    setSaving(true);
    try {
      await formulaApi.saveDepreciationSlabs({
        slabs: ordered.map((r) => ({
          ageFrom: Number(r.ageFrom),
          ageTo: Number(r.ageTo),
          hutMudPercent: Number(r.hutMudPercent),
          stoneBrickRccPercent: Number(r.stoneBrickRccPercent),
        })),
        resolutionNo: resolutionNo.trim(),
        resolutionDate: resolutionDate || null,
        changeReason: changeReason.trim(),
      });
      setChangeReason('');
      await onSaved(t('formula.depSavedToast'));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setErrorSummary(parsed.summary);
      setErrorItems(parsed.items);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-extrabold text-ink">{t('formula.previewTitle.depreciation')}</h2>
          <p className="text-xs text-muted">{t('formula.depSubtitle')}</p>
        </div>
        {canWrite && (
          <button
            type="button"
            onClick={handleAddSlab}
            className="inline-flex items-center gap-1.5 rounded-xl border border-brand-200 bg-brand-50 px-3 py-1.5 text-xs font-bold text-brand-800 hover:bg-brand-100"
          >
            <Plus className="h-3.5 w-3.5" />
            {t('formula.addSlabRowBtn')}
          </button>
        )}
      </div>

      <ValidationSummary summary={errorSummary} items={errorItems} />

      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="space-y-4 text-xs">
        <div className="overflow-x-auto rounded-xl border border-line">
          <table className="w-full text-left text-xs">
            <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
              <tr>
                <th className="px-3 py-2.5">#</th>
                <th className="px-3 py-2.5">{t('formula.colAgeRule')}</th>
                <th className="px-3 py-2.5">{t('formula.colAgeFrom')}</th>
                <th className="px-3 py-2.5">{t('formula.colAgeTo')}</th>
                <th className="px-3 py-2.5">{t('formula.colHutMudPct')}</th>
                <th className="px-3 py-2.5">{t('formula.colStoneRccPct')}</th>
                <th className="px-3 py-2.5 text-right">{t('formula.colActions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line bg-card">
              {rows.map((row, idx) => (
                <tr key={row.id ?? `dep-${idx}`} className="hover:bg-slate-50/70">
                  <td className="px-3 py-2 mono text-muted">{idx + 1}</td>
                  <td className="px-3 py-2 font-semibold mono text-brand-800">
                    {row.ageFrom === 0 ? `0 <= Age <= ${row.ageTo}` : `${row.ageFrom} < Age <= ${row.ageTo}`}
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      disabled={!canWrite}
                      value={row.ageFrom}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, ageFrom: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="1"
                      disabled={!canWrite}
                      value={row.ageTo}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, ageTo: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      max="100"
                      step="0.01"
                      disabled={!canWrite}
                      value={row.hutMudPercent}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, hutMudPercent: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-28 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      max="100"
                      step="0.01"
                      disabled={!canWrite}
                      value={row.stoneBrickRccPercent}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) =>
                            i === idx ? { ...r, stoneBrickRccPercent: Number(e.target.value) } : r,
                          ),
                        )
                      }
                      className="w-28 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2 text-right">
                    <div className="inline-flex items-center gap-1.5">
                      {row.id && (
                        <Link
                          to={`/audit/DepreciationSlab/${row.id}`}
                          className="rounded-lg border border-line bg-surface p-1.5 text-slate-600 hover:text-brand-700"
                          title={t('properties.auditHistory')}
                        >
                          <History className="h-3.5 w-3.5" />
                        </Link>
                      )}
                      {canWrite && rows.length > 1 && (
                        <button
                          type="button"
                          onClick={() => handleRemoveSlab(idx)}
                          className="rounded-lg border border-rose-200 bg-rose-50 p-1.5 text-rose-700 hover:bg-rose-100"
                          aria-label={t('formula.removeRowBtn')}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {canWrite && (
          <div className="rounded-xl border border-line bg-surface p-4 space-y-3">
            <div className="grid gap-3 sm:grid-cols-3">
              <label className="space-y-1 font-bold text-slate-700">
                <span>
                  {t('formula.resolutionNoLabel')} <span className="text-rose-600">*</span>
                </span>
                <input
                  type="text"
                  placeholder={t('formula.resolutionNoPlaceholder')}
                  value={resolutionNo}
                  onChange={(e) => setResolutionNo(e.target.value)}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionDateLabel')}</span>
                <input
                  type="date"
                  value={resolutionDate}
                  onChange={(e) => setResolutionDate(e.target.value)}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 mono"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>
                  {t('formula.resolutionReasonLabel')} <span className="text-rose-600">*</span>
                </span>
                <input
                  type="text"
                  placeholder={t('formula.changeReasonPlaceholder')}
                  value={changeReason}
                  onChange={(e) => setChangeReason(e.target.value)}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                />
              </label>
            </div>
            <div className="flex justify-end">
              <button
                type="submit"
                disabled={saving}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
              >
                <Save className="h-3.5 w-3.5" />
                {saving ? t('onboarding.submitting') : t('formula.saveDepBatchBtn')}
              </button>
            </div>
          </div>
        )}
      </form>
    </section>
  );
}

interface UsageWeightagesTabProps {
  items: UsageWeightageDto[];
  canWrite: boolean;
  onSaved: (message: string) => Promise<void>;
}

export function UsageWeightagesTab({ items, canWrite, onSaved }: UsageWeightagesTabProps) {
  const { t } = useTranslation();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [form, setForm] = useState<SaveUsageWeightagePayload>({
    id: null,
    code: '',
    usageName: '',
    weightage: 1.0,
    isActive: true,
    resolutionNo: '',
    resolutionDate: new Date().toISOString().slice(0, 10),
    changeReason: '',
  });
  const [saving, setSaving] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  function openEditor(item?: UsageWeightageDto) {
    setErrSummary(null);
    setErrItems([]);
    setForm(
      item
        ? {
            id: item.id,
            code: item.code,
            usageName: item.usageName,
            weightage: item.weightage,
            isActive: item.isActive,
            resolutionNo: item.lastResolutionNo ?? '',
            resolutionDate: item.lastResolutionDate ?? new Date().toISOString().slice(0, 10),
            changeReason: '',
          }
        : {
            id: null,
            code: '',
            usageName: '',
            weightage: 1.0,
            isActive: true,
            resolutionNo: '',
            resolutionDate: new Date().toISOString().slice(0, 10),
            changeReason: '',
          },
    );
    setDrawerOpen(true);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrSummary(null);
    setErrItems([]);

    const errs: ParsedValidationItem[] = [];
    if (!form.code.trim() || !/^[A-Za-z0-9_-]+$/.test(form.code.trim())) {
      errs.push({ key: 'code', rawField: 'Code', message: t('formula.errCodeFormat') });
    }
    if (!form.usageName.trim()) {
      errs.push({ key: 'usageName', rawField: 'UsageName', message: t('validation.required') });
    }
    if (!(Number(form.weightage) > 0)) {
      errs.push({ key: 'weightage', rawField: 'Weightage', message: t('formula.errWeightPositive') });
    }
    if (!form.resolutionNo.trim()) {
      errs.push({ key: 'resolutionNo', rawField: 'ResolutionNo', message: t('formula.errResolutionNoRequired') });
    }
    if (!form.changeReason.trim()) {
      errs.push({ key: 'changeReason', rawField: 'ChangeReason', message: t('formula.errReasonRequired') });
    }

    if (errs.length > 0) {
      setErrSummary(t('validation.summaryTitle'));
      setErrItems(errs);
      return;
    }

    setSaving(true);
    try {
      await formulaApi.saveUsageWeightage({
        ...form,
        code: form.code.trim().toUpperCase(),
        usageName: form.usageName.trim(),
        resolutionNo: form.resolutionNo.trim(),
        resolutionDate: form.resolutionDate || null,
        changeReason: form.changeReason.trim(),
      });
      setDrawerOpen(false);
      await onSaved(t('formula.usageSavedToast'));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-extrabold text-ink">{t('formula.previewTitle.usage')}</h2>
          <p className="text-xs text-muted">{t('formula.usageSubtitle')}</p>
        </div>
        {canWrite && (
          <button
            type="button"
            onClick={() => openEditor()}
            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-brand-800"
          >
            <Plus className="h-4 w-4" />
            {t('formula.addUsageBtn')}
          </button>
        )}
      </div>

      <div className="overflow-x-auto rounded-xl border border-line">
        <table className="w-full text-left text-xs">
          <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
            <tr>
              <th className="px-3 py-2.5">#</th>
              <th className="px-3 py-2.5">{t('formula.colCode')}</th>
              <th className="px-3 py-2.5">{t('formula.colUsageName')}</th>
              <th className="px-3 py-2.5 text-right">{t('formula.colWeight')}</th>
              <th className="px-3 py-2.5">{t('formula.colStatus')}</th>
              <th className="px-3 py-2.5 text-right">{t('formula.colActions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line bg-card">
            {items.map((item, idx) => (
              <tr key={item.id} className="hover:bg-slate-50/70">
                <td className="px-3 py-2.5 mono text-muted">{idx + 1}</td>
                <td className="px-3 py-2.5 font-bold mono text-ink">{item.code}</td>
                <td className="px-3 py-2.5">
                  <div className="font-semibold text-ink">{item.usageName}</div>
                  {item.lastResolutionNo && (
                    <div className="text-[10.5px] text-muted">
                      {t('formula.resTag')}: {item.lastResolutionNo}
                    </div>
                  )}
                </td>
                <td className="px-3 py-2.5 text-right mono font-bold text-brand-800">
                  {item.weightage.toFixed(2)}x
                </td>
                <td className="px-3 py-2.5">
                  <span
                    className={`inline-block rounded-full px-2 py-0.5 text-[10.5px] font-bold ${
                      item.isActive
                        ? 'bg-emerald-50 text-emerald-800 border border-emerald-200'
                        : 'bg-slate-100 text-slate-600 border border-slate-200'
                    }`}
                  >
                    {item.isActive ? t('formula.active') : t('formula.inactive')}
                  </span>
                </td>
                <td className="px-3 py-2.5 text-right">
                  <div className="inline-flex items-center gap-1.5">
                    <Link
                      to={`/audit/UsageWeightage/${item.id}`}
                      className="rounded-lg border border-line bg-surface p-1.5 text-slate-600 hover:text-brand-700"
                      title={t('properties.auditHistory')}
                    >
                      <History className="h-3.5 w-3.5" />
                    </Link>
                    {canWrite && (
                      <button
                        type="button"
                        onClick={() => openEditor(item)}
                        className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-800 hover:bg-brand-100"
                      >
                        <Edit3 className="h-3 w-3" />
                        {t('formula.editRowBtn')}
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {drawerOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4"
        >
          <div className="w-full max-w-lg rounded-2xl border border-line bg-card p-6 shadow-xl space-y-4 text-xs">
            <div className="flex items-center justify-between border-b border-line pb-3">
              <h3 className="text-base font-extrabold text-ink">
                {form.id ? t('formula.editUsageTitle') : t('formula.addUsageBtn')}
              </h3>
              <button
                type="button"
                onClick={() => setDrawerOpen(false)}
                className="rounded-lg p-1 text-slate-500 hover:bg-slate-100"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <ValidationSummary summary={errSummary} items={errItems} />
            <form onSubmit={(e) => void handleSubmit(e)} noValidate className="space-y-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colCode')} *</span>
                  <input
                    type="text"
                    value={form.code}
                    onChange={(e) => setForm((p) => ({ ...p, code: e.target.value.toUpperCase() }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colWeight')} *</span>
                  <input
                    type="number"
                    min="0.01"
                    step="0.05"
                    value={form.weightage}
                    onChange={(e) => setForm((p) => ({ ...p, weightage: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>
              <label className="block space-y-1 font-bold text-slate-700">
                <span>{t('formula.colUsageName')} *</span>
                <input
                  type="text"
                  value={form.usageName}
                  onChange={(e) => setForm((p) => ({ ...p, usageName: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <label className="inline-flex items-center gap-2 font-bold text-slate-700 cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
                  className="h-4 w-4 rounded border-slate-300 text-brand-700"
                />
                <span>{t('formula.active')}</span>
              </label>
              <div className="grid gap-3 sm:grid-cols-2 border-t border-line pt-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionNoLabel')} *</span>
                  <input
                    type="text"
                    value={form.resolutionNo}
                    onChange={(e) => setForm((p) => ({ ...p, resolutionNo: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionDateLabel')}</span>
                  <input
                    type="date"
                    value={form.resolutionDate ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, resolutionDate: e.target.value || null }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>
              <label className="block space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionReasonLabel')} *</span>
                <input
                  type="text"
                  value={form.changeReason}
                  onChange={(e) => setForm((p) => ({ ...p, changeReason: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setDrawerOpen(false)}
                  className="rounded-xl border border-slate-300 px-4 py-2 font-bold text-slate-700"
                >
                  {t('properties.cancel')}
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
                >
                  {saving ? t('onboarding.submitting') : t('formula.saveRowBtn')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  );
}

interface AreaSlabTaxesTabProps {
  slabs: AreaSlabTaxDto[];
  canWrite: boolean;
  onSaved: (message: string) => Promise<void>;
}

interface EditableAreaRow {
  id?: string;
  areaFrom: number;
  areaTo: number;
  lightingTax: number;
  healthTax: number;
  sanitationTax: number;
  toiletTax: number;
}

export function AreaSlabTaxesTab({ slabs, canWrite, onSaved }: AreaSlabTaxesTabProps) {
  const { t } = useTranslation();
  const [rows, setRows] = useState<EditableAreaRow[]>(() =>
    slabs.map((s) => ({
      id: s.id,
      areaFrom: s.areaFrom,
      areaTo: s.areaTo,
      lightingTax: s.lightingTax,
      healthTax: s.healthTax,
      sanitationTax: s.sanitationTax,
      toiletTax: s.toiletTax,
    })),
  );
  const [resolutionNo, setResolutionNo] = useState('');
  const [resolutionDate, setResolutionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [changeReason, setChangeReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  function handleAddAreaSlab() {
    const prevTo = rows.length > 0 ? rows[rows.length - 1].areaTo : 0;
    setRows((prev) => [
      ...prev,
      {
        areaFrom: prevTo + 1,
        areaTo: Math.max(prevTo + 500, 999999),
        lightingTax: 100,
        healthTax: 100,
        sanitationTax: 100,
        toiletTax: 0,
      },
    ]);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrSummary(null);
    setErrItems([]);

    const errs: ParsedValidationItem[] = [];
    const ordered = [...rows].sort((a, b) => a.areaFrom - b.areaFrom || a.areaTo - b.areaTo);

    if (ordered.length === 0) {
      errs.push({ key: 'slabs', rawField: 'Slabs', message: t('formula.errSlabsEmpty') });
    } else {
      if (ordered[0].areaFrom !== 1) {
        errs.push({ key: 'slabs', rawField: 'Slabs', message: t('formula.errAreaStartOne') });
      }
      for (let i = 0; i < ordered.length; i += 1) {
        const cur = ordered[i];
        if (cur.areaTo < cur.areaFrom) {
          errs.push({ key: `slabs.${i}`, rawField: 'Slabs', message: t('formula.errAreaRangeOrder') });
          break;
        }
        if (cur.lightingTax < 0 || cur.healthTax < 0 || cur.sanitationTax < 0 || cur.toiletTax < 0) {
          errs.push({ key: `slabs.${i}`, rawField: 'Slabs', message: t('formula.errNegativeRate') });
          break;
        }
        if (i > 0 && cur.areaFrom !== ordered[i - 1].areaTo + 1) {
          errs.push({
            key: `slabs.${i}`,
            rawField: 'Slabs',
            message: t('formula.errAreaContiguous', { from: cur.areaFrom, nextStart: ordered[i - 1].areaTo + 1 }),
          });
          break;
        }
      }
    }

    if (!resolutionNo.trim()) {
      errs.push({ key: 'resolutionNo', rawField: 'ResolutionNo', message: t('formula.errResolutionNoRequired') });
    }
    if (!changeReason.trim()) {
      errs.push({ key: 'changeReason', rawField: 'ChangeReason', message: t('formula.errReasonRequired') });
    }

    if (errs.length > 0) {
      setErrSummary(t('validation.summaryTitle'));
      setErrItems(errs);
      return;
    }

    setSaving(true);
    try {
      await formulaApi.saveAreaSlabs({
        slabs: ordered.map((r) => ({
          areaFrom: Number(r.areaFrom),
          areaTo: Number(r.areaTo),
          lightingTax: Number(r.lightingTax),
          healthTax: Number(r.healthTax),
          sanitationTax: Number(r.sanitationTax),
          toiletTax: Number(r.toiletTax),
        })),
        resolutionNo: resolutionNo.trim(),
        resolutionDate: resolutionDate || null,
        changeReason: changeReason.trim(),
      });
      setChangeReason('');
      await onSaved(t('formula.areaSavedToast'));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-extrabold text-ink">{t('formula.previewTitle.areaSlabs')}</h2>
          <p className="text-xs text-muted">{t('formula.areaSlabsSubtitle')}</p>
        </div>
        {canWrite && (
          <button
            type="button"
            onClick={handleAddAreaSlab}
            className="inline-flex items-center gap-1.5 rounded-xl border border-brand-200 bg-brand-50 px-3 py-1.5 text-xs font-bold text-brand-800 hover:bg-brand-100"
          >
            <Plus className="h-3.5 w-3.5" />
            {t('formula.addSlabRowBtn')}
          </button>
        )}
      </div>

      <ValidationSummary summary={errSummary} items={errItems} />

      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="space-y-4 text-xs">
        <div className="overflow-x-auto rounded-xl border border-line">
          <table className="w-full text-left text-xs">
            <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
              <tr>
                <th className="px-3 py-2.5">#</th>
                <th className="px-3 py-2.5">{t('formula.colAreaFrom')}</th>
                <th className="px-3 py-2.5">{t('formula.colAreaTo')}</th>
                <th className="px-3 py-2.5">{t('formula.colLightingTax')}</th>
                <th className="px-3 py-2.5">{t('formula.colHealthTax')}</th>
                <th className="px-3 py-2.5">{t('formula.colSanitationTax')}</th>
                <th className="px-3 py-2.5">{t('formula.colToiletTax')}</th>
                <th className="px-3 py-2.5 text-right">{t('formula.colActions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line bg-card">
              {rows.map((row, idx) => (
                <tr key={row.id ?? `area-${idx}`} className="hover:bg-slate-50/70">
                  <td className="px-3 py-2 mono text-muted">{idx + 1}</td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="1"
                      disabled={!canWrite}
                      value={row.areaFrom}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, areaFrom: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="1"
                      disabled={!canWrite}
                      value={row.areaTo}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, areaTo: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-28 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      disabled={!canWrite}
                      value={row.lightingTax}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, lightingTax: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      disabled={!canWrite}
                      value={row.healthTax}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, healthTax: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      disabled={!canWrite}
                      value={row.sanitationTax}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, sanitationTax: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      disabled={!canWrite}
                      value={row.toiletTax}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, toiletTax: Number(e.target.value) } : r)),
                        )
                      }
                      className="w-24 rounded-lg border border-slate-300 px-2.5 py-1.5 mono disabled:bg-slate-100"
                    />
                  </td>
                  <td className="px-3 py-2 text-right">
                    <div className="inline-flex items-center gap-1.5">
                      {row.id && (
                        <Link
                          to={`/audit/AreaSlabTax/${row.id}`}
                          className="rounded-lg border border-line bg-surface p-1.5 text-slate-600 hover:text-brand-700"
                          title={t('properties.auditHistory')}
                        >
                          <History className="h-3.5 w-3.5" />
                        </Link>
                      )}
                      {canWrite && rows.length > 1 && (
                        <button
                          type="button"
                          onClick={() => setRows((prev) => prev.filter((_, i) => i !== idx))}
                          className="rounded-lg border border-rose-200 bg-rose-50 p-1.5 text-rose-700 hover:bg-rose-100"
                          aria-label={t('formula.removeRowBtn')}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {canWrite && (
          <div className="rounded-xl border border-line bg-surface p-4 space-y-3">
            <div className="grid gap-3 sm:grid-cols-3">
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionNoLabel')} *</span>
                <input
                  type="text"
                  placeholder={t('formula.resolutionNoPlaceholder')}
                  value={resolutionNo}
                  onChange={(e) => setResolutionNo(e.target.value)}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionDateLabel')}</span>
                <input
                  type="date"
                  value={resolutionDate}
                  onChange={(e) => setResolutionDate(e.target.value)}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 mono"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionReasonLabel')} *</span>
                <input
                  type="text"
                  placeholder={t('formula.changeReasonPlaceholder')}
                  value={changeReason}
                  onChange={(e) => setChangeReason(e.target.value)}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                />
              </label>
            </div>
            <div className="flex justify-end">
              <button
                type="submit"
                disabled={saving}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
              >
                <Save className="h-3.5 w-3.5" />
                {saving ? t('onboarding.submitting') : t('formula.saveAreaBatchBtn')}
              </button>
            </div>
          </div>
        )}
      </form>
    </section>
  );
}

interface WaterTaxMastersTabProps {
  items: WaterTaxMasterDto[];
  canWrite: boolean;
  onSaved: (message: string) => Promise<void>;
}

export function WaterTaxMastersTab({ items, canWrite, onSaved }: WaterTaxMastersTabProps) {
  const { t } = useTranslation();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [form, setForm] = useState<SaveWaterTaxMasterPayload>({
    id: null,
    code: '',
    waterTypeName: '',
    amount: 0,
    isActive: true,
    resolutionNo: '',
    resolutionDate: new Date().toISOString().slice(0, 10),
    changeReason: '',
  });
  const [saving, setSaving] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  function openEditor(item?: WaterTaxMasterDto) {
    setErrSummary(null);
    setErrItems([]);
    setForm(
      item
        ? {
            id: item.id,
            code: item.code,
            waterTypeName: item.waterTypeName,
            amount: item.amount,
            isActive: item.isActive,
            resolutionNo: item.lastResolutionNo ?? '',
            resolutionDate: item.lastResolutionDate ?? new Date().toISOString().slice(0, 10),
            changeReason: '',
          }
        : {
            id: null,
            code: '',
            waterTypeName: '',
            amount: 720,
            isActive: true,
            resolutionNo: '',
            resolutionDate: new Date().toISOString().slice(0, 10),
            changeReason: '',
          },
    );
    setDrawerOpen(true);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrSummary(null);
    setErrItems([]);

    const errs: ParsedValidationItem[] = [];
    if (!form.code.trim() || !/^[A-Za-z0-9_-]+$/.test(form.code.trim())) {
      errs.push({ key: 'code', rawField: 'Code', message: t('formula.errCodeFormat') });
    }
    if (!form.waterTypeName.trim()) {
      errs.push({ key: 'waterTypeName', rawField: 'WaterTypeName', message: t('validation.required') });
    }
    if (Number(form.amount) < 0) {
      errs.push({ key: 'amount', rawField: 'Amount', message: t('formula.errNegativeRate') });
    }
    if (!form.resolutionNo.trim()) {
      errs.push({ key: 'resolutionNo', rawField: 'ResolutionNo', message: t('formula.errResolutionNoRequired') });
    }
    if (!form.changeReason.trim()) {
      errs.push({ key: 'changeReason', rawField: 'ChangeReason', message: t('formula.errReasonRequired') });
    }

    if (errs.length > 0) {
      setErrSummary(t('validation.summaryTitle'));
      setErrItems(errs);
      return;
    }

    setSaving(true);
    try {
      await formulaApi.saveWaterTax({
        ...form,
        code: form.code.trim().toUpperCase(),
        waterTypeName: form.waterTypeName.trim(),
        resolutionNo: form.resolutionNo.trim(),
        resolutionDate: form.resolutionDate || null,
        changeReason: form.changeReason.trim(),
      });
      setDrawerOpen(false);
      await onSaved(t('formula.waterSavedToast'));
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.saveFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-extrabold text-ink">{t('formula.previewTitle.waterTax')}</h2>
          <p className="text-xs text-muted">{t('formula.waterTaxSubtitle')}</p>
        </div>
        {canWrite && (
          <button
            type="button"
            onClick={() => openEditor()}
            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-brand-800"
          >
            <Plus className="h-4 w-4" />
            {t('formula.addWaterBtn')}
          </button>
        )}
      </div>

      <div className="overflow-x-auto rounded-xl border border-line">
        <table className="w-full text-left text-xs">
          <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
            <tr>
              <th className="px-3 py-2.5">#</th>
              <th className="px-3 py-2.5">{t('formula.colCode')}</th>
              <th className="px-3 py-2.5">{t('formula.colWaterName')}</th>
              <th className="px-3 py-2.5 text-right">{t('formula.colWaterAmount')}</th>
              <th className="px-3 py-2.5">{t('formula.colStatus')}</th>
              <th className="px-3 py-2.5 text-right">{t('formula.colActions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line bg-card">
            {items.map((item, idx) => (
              <tr key={item.id} className="hover:bg-slate-50/70">
                <td className="px-3 py-2.5 mono text-muted">{idx + 1}</td>
                <td className="px-3 py-2.5 font-bold mono text-ink">{item.code}</td>
                <td className="px-3 py-2.5">
                  <div className="font-semibold text-ink">{item.waterTypeName}</div>
                  {item.lastResolutionNo && (
                    <div className="text-[10.5px] text-muted">
                      {t('formula.resTag')}: {item.lastResolutionNo}
                    </div>
                  )}
                </td>
                <td className="px-3 py-2.5 text-right mono font-bold text-brand-800">
                  {formatInr(item.amount)}
                </td>
                <td className="px-3 py-2.5">
                  <span
                    className={`inline-block rounded-full px-2 py-0.5 text-[10.5px] font-bold ${
                      item.isActive
                        ? 'bg-emerald-50 text-emerald-800 border border-emerald-200'
                        : 'bg-slate-100 text-slate-600 border border-slate-200'
                    }`}
                  >
                    {item.isActive ? t('formula.active') : t('formula.inactive')}
                  </span>
                </td>
                <td className="px-3 py-2.5 text-right">
                  <div className="inline-flex items-center gap-1.5">
                    <Link
                      to={`/audit/WaterTaxMaster/${item.id}`}
                      className="rounded-lg border border-line bg-surface p-1.5 text-slate-600 hover:text-brand-700"
                      title={t('properties.auditHistory')}
                    >
                      <History className="h-3.5 w-3.5" />
                    </Link>
                    {canWrite && (
                      <button
                        type="button"
                        onClick={() => openEditor(item)}
                        className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-800 hover:bg-brand-100"
                      >
                        <Edit3 className="h-3 w-3" />
                        {t('formula.editRowBtn')}
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {drawerOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4"
        >
          <div className="w-full max-w-lg rounded-2xl border border-line bg-card p-6 shadow-xl space-y-4 text-xs">
            <div className="flex items-center justify-between border-b border-line pb-3">
              <h3 className="text-base font-extrabold text-ink">
                {form.id ? t('formula.editWaterTitle') : t('formula.addWaterBtn')}
              </h3>
              <button
                type="button"
                onClick={() => setDrawerOpen(false)}
                className="rounded-lg p-1 text-slate-500 hover:bg-slate-100"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <ValidationSummary summary={errSummary} items={errItems} />
            <form onSubmit={(e) => void handleSubmit(e)} noValidate className="space-y-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colCode')} *</span>
                  <input
                    type="text"
                    value={form.code}
                    onChange={(e) => setForm((p) => ({ ...p, code: e.target.value.toUpperCase() }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.colWaterAmount')} *</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={form.amount}
                    onChange={(e) => setForm((p) => ({ ...p, amount: Number(e.target.value) }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>
              <label className="block space-y-1 font-bold text-slate-700">
                <span>{t('formula.colWaterName')} *</span>
                <input
                  type="text"
                  value={form.waterTypeName}
                  onChange={(e) => setForm((p) => ({ ...p, waterTypeName: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <label className="inline-flex items-center gap-2 font-bold text-slate-700 cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
                  className="h-4 w-4 rounded border-slate-300 text-brand-700"
                />
                <span>{t('formula.active')}</span>
              </label>
              <div className="grid gap-3 sm:grid-cols-2 border-t border-line pt-3">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionNoLabel')} *</span>
                  <input
                    type="text"
                    value={form.resolutionNo}
                    onChange={(e) => setForm((p) => ({ ...p, resolutionNo: e.target.value }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2"
                  />
                </label>
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.resolutionDateLabel')}</span>
                  <input
                    type="date"
                    value={form.resolutionDate ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, resolutionDate: e.target.value || null }))}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                  />
                </label>
              </div>
              <label className="block space-y-1 font-bold text-slate-700">
                <span>{t('formula.resolutionReasonLabel')} *</span>
                <input
                  type="text"
                  value={form.changeReason}
                  onChange={(e) => setForm((p) => ({ ...p, changeReason: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setDrawerOpen(false)}
                  className="rounded-xl border border-slate-300 px-4 py-2 font-bold text-slate-700"
                >
                  {t('properties.cancel')}
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
                >
                  {saving ? t('onboarding.submitting') : t('formula.saveRowBtn')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  );
}

