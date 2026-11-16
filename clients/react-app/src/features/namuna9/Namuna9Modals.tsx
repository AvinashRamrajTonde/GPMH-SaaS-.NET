import {
  AlertTriangle,
  Download,
  FileSpreadsheet,
  Lock,
  Percent,
  Printer,
  RefreshCw,
  X,
} from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ValidationSummary } from '../../components/ValidationSummary';
import {
  namuna9Api,
  type Namuna9Format,
  type Namuna9PrintStyle,
  type Namuna9RebateResultDto,
  type Namuna9RegisterPart,
  type Namuna9RolloverResultDto,
  type Namuna9WorkspaceDto,
} from '../../services/namuna9Api';
import { formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';

function todayIsoDate(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function suggestNextFyLabel(yearLabel: string): string {
  const match = /^(\d{4})-(\d{2})$/.exec(yearLabel.trim());
  if (!match) return '';
  const nextStart = Number(match[1]) + 1;
  const nextEndShort = String((nextStart + 1) % 100).padStart(2, '0');
  return `${nextStart}-${nextEndShort}`;
}

interface EarlyRebateModalProps {
  workspace: Namuna9WorkspaceDto;
  selectedPropertyIds: string[];
  onClose: () => void;
  onSuccess: (result: Namuna9RebateResultDto) => void;
}

export function EarlyRebateModal({
  workspace,
  selectedPropertyIds,
  onClose,
  onSuccess,
}: EarlyRebateModalProps) {
  const { t } = useTranslation();
  const [applicationDate, setApplicationDate] = useState(todayIsoDate);
  const [cutoffDate, setCutoffDate] = useState(workspace.formulaRates.defaultRebateCutoffDate);
  const [submitting, setSubmitting] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setErrSummary(null);
    setErrItems([]);
    try {
      const result = await namuna9Api.applyEarlyRebate({
        assessmentYearId: workspace.selectedYear.id,
        applicationDate,
        cutoffDate,
        propertyIds: selectedPropertyIds.length > 0 ? selectedPropertyIds : undefined,
      });
      onSuccess(result);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9.errRebateFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/55 p-4 backdrop-blur-xs">
      <div className="w-full max-w-lg rounded-2xl border border-line bg-card p-5 shadow-xl">
        <div className="flex items-start justify-between gap-3 border-b border-line pb-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-amber-100 text-amber-700">
              <Percent className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-ink">{t('namuna9.rebateModalTitle')}</h2>
              <p className="text-xs text-muted">
                {workspace.selectedYear.yearLabel} ·{' '}
                {t('namuna9.rebateRateLabel', { pct: workspace.formulaRates.earlyRebatePct })}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('namuna9.cancelBtn')}
            className="rounded-lg p-1.5 text-muted hover:bg-slate-100 hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <ValidationSummary summary={errSummary} items={errItems} />

          <div className="rounded-xl border border-amber-200 bg-amber-50/70 p-3 text-xs text-amber-900">
            <p className="font-semibold">
              {selectedPropertyIds.length > 0
                ? t('namuna9.rebateScopeSelected', { count: selectedPropertyIds.length })
                : t('namuna9.rebateScopeAll', { count: workspace.items.length })}
            </p>
            <p className="mt-1 text-[11.5px] text-amber-800">{t('namuna9.rebateRuleHint')}</p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block space-y-1 text-xs">
              <span className="font-semibold text-ink">{t('namuna9.applicationDateLabel')}</span>
              <input
                type="date"
                required
                value={applicationDate}
                onChange={(e) => setApplicationDate(e.target.value)}
                className="w-full rounded-xl border border-line bg-white px-3 py-2 text-xs text-ink focus:border-brand-600 focus:outline-none"
              />
            </label>

            <label className="block space-y-1 text-xs">
              <span className="font-semibold text-ink">{t('namuna9.cutoffDateLabel')}</span>
              <input
                type="date"
                required
                value={cutoffDate}
                onChange={(e) => setCutoffDate(e.target.value)}
                className="w-full rounded-xl border border-line bg-white px-3 py-2 text-xs text-ink focus:border-brand-600 focus:outline-none"
              />
            </label>
          </div>

          <div className="flex justify-end gap-2 border-t border-line pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-line bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"
            >
              {t('namuna9.cancelBtn')}
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="inline-flex items-center gap-1.5 rounded-xl bg-amber-600 px-4 py-2 text-xs font-bold text-white hover:bg-amber-700 disabled:opacity-50"
            >
              <Percent className="h-3.5 w-3.5" />
              {submitting ? t('namuna9.processingLabel') : t('namuna9.confirmRebateBtn')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

interface FyRolloverModalProps {
  workspace: Namuna9WorkspaceDto;
  onClose: () => void;
  onSuccess: (result: Namuna9RolloverResultDto) => void;
}

export function FyRolloverModal({ workspace, onClose, onSuccess }: FyRolloverModalProps) {
  const { t } = useTranslation();
  const [targetYearLabel, setTargetYearLabel] = useState(() =>
    suggestNextFyLabel(workspace.selectedYear.yearLabel),
  );
  const [carryForwardCurrentTaxCalc, setCarryForwardCurrentTaxCalc] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setErrSummary(null);
    setErrItems([]);
    try {
      const result = await namuna9Api.executeRollover({
        sourceAssessmentYearId: workspace.selectedYear.id,
        targetYearLabel: targetYearLabel.trim(),
        carryForwardCurrentTaxCalc,
      });
      onSuccess(result);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9.errRolloverFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/55 p-4 backdrop-blur-xs">
      <div className="w-full max-w-lg rounded-2xl border border-line bg-card p-5 shadow-xl">
        <div className="flex items-start justify-between gap-3 border-b border-line pb-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-rose-100 text-rose-700">
              <RefreshCw className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-ink">{t('namuna9.rolloverModalTitle')}</h2>
              <p className="text-xs text-muted">
                {t('namuna9.rolloverSourceLabel', { year: workspace.selectedYear.yearLabel })}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('namuna9.cancelBtn')}
            className="rounded-lg p-1.5 text-muted hover:bg-slate-100 hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <ValidationSummary summary={errSummary} items={errItems} />

          <div className="rounded-xl border border-rose-200 bg-rose-50/75 p-3 text-xs text-rose-900">
            <div className="flex items-start gap-2">
              <Lock className="mt-0.5 h-4 w-4 shrink-0 text-rose-700" />
              <div className="space-y-1">
                <p className="font-bold">{t('namuna9.rolloverWarningTitle')}</p>
                <p className="text-[11.5px] text-rose-800">
                  {t('namuna9.rolloverWarningDesc', {
                    year: workspace.selectedYear.yearLabel,
                    arrears: formatInr(workspace.kpis.totalBalanceDues),
                    count: workspace.items.length,
                  })}
                </p>
              </div>
            </div>
          </div>

          <label className="block space-y-1 text-xs">
            <span className="font-semibold text-ink">{t('namuna9.targetYearLabel')}</span>
            <input
              type="text"
              required
              pattern="\d{4}-\d{2}"
              placeholder="2027-28"
              value={targetYearLabel}
              onChange={(e) => setTargetYearLabel(e.target.value)}
              className="w-full rounded-xl border border-line bg-white px-3 py-2 font-mono text-xs text-ink focus:border-brand-600 focus:outline-none"
            />
            <span className="block text-[11px] text-muted">{t('namuna9.targetYearHint')}</span>
          </label>

          <label className="flex items-start gap-2.5 rounded-xl border border-line bg-slate-50/80 p-3 text-xs">
            <input
              type="checkbox"
              checked={carryForwardCurrentTaxCalc}
              onChange={(e) => setCarryForwardCurrentTaxCalc(e.target.checked)}
              className="mt-0.5 h-4 w-4 rounded border-slate-300 text-brand-700"
            />
            <div>
              <span className="font-semibold text-ink">{t('namuna9.carryForwardCalcLabel')}</span>
              <p className="mt-0.5 text-[11px] text-muted">{t('namuna9.carryForwardCalcHint')}</p>
            </div>
          </label>

          <div className="flex justify-end gap-2 border-t border-line pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-line bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"
            >
              {t('namuna9.cancelBtn')}
            </button>
            <button
              type="submit"
              disabled={submitting || workspace.selectedYear.isLocked}
              className="inline-flex items-center gap-1.5 rounded-xl bg-rose-700 px-4 py-2 text-xs font-bold text-white hover:bg-rose-800 disabled:opacity-50"
            >
              <AlertTriangle className="h-3.5 w-3.5" />
              {submitting ? t('namuna9.processingLabel') : t('namuna9.confirmRolloverBtn')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

interface PrintRegisterModalProps {
  workspace: Namuna9WorkspaceDto;
  initialFormat: Namuna9Format;
  wardNo: string;
  search: string;
  pendingOnly: boolean;
  onClose: () => void;
  onActionSuccess: (msg: string) => void;
}

const MARGIN_OPTIONS = [0, 10, 15, 20, 25, 30] as const;

export function PrintRegisterModal({
  workspace,
  initialFormat,
  wardNo,
  search,
  pendingOnly,
  onClose,
  onActionSuccess,
}: PrintRegisterModalProps) {
  const { t } = useTranslation();
  const [format, setFormat] = useState<Namuna9Format>(initialFormat);
  const [part, setPart] = useState<Namuna9RegisterPart>('Both');
  const [style, setStyle] = useState<Namuna9PrintStyle>('color');
  const [leftMarginMm, setLeftMarginMm] = useState<number>(10);
  const [downloading, setDownloading] = useState<'pdf' | 'csv' | null>(null);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  const handleDownloadPdf = async () => {
    setDownloading('pdf');
    setErrSummary(null);
    setErrItems([]);
    try {
      await namuna9Api.downloadRegisterPdf({
        yearId: workspace.selectedYear.id,
        wardNo: wardNo || undefined,
        search: search || undefined,
        format,
        part,
        style,
        leftMarginMm,
        pendingOnly,
      });
      onActionSuccess(t('namuna9.pdfDownloadedMsg', { format }));
      onClose();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9.errPrintFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setDownloading(null);
    }
  };

  const handleDownloadCsv = async () => {
    setDownloading('csv');
    setErrSummary(null);
    setErrItems([]);
    try {
      await namuna9Api.downloadCsv({
        yearId: workspace.selectedYear.id,
        wardNo: wardNo || undefined,
        search: search || undefined,
        format,
        pendingOnly,
      });
      onActionSuccess(t('namuna9.csvDownloadedMsg', { format }));
      onClose();
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna9.errExportFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setDownloading(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/55 p-4 backdrop-blur-xs">
      <div className="w-full max-w-2xl rounded-2xl border border-line bg-card p-5 shadow-xl">
        <div className="flex items-start justify-between gap-3 border-b border-line pb-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-indigo-100 text-indigo-700">
              <Printer className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-ink">{t('namuna9.printModalTitle')}</h2>
              <p className="text-xs text-muted">{t('namuna9.printModalSubtitle')}</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('namuna9.cancelBtn')}
            className="rounded-lg p-1.5 text-muted hover:bg-slate-100 hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="mt-4 space-y-4">
          <ValidationSummary summary={errSummary} items={errItems} />

          {/* Format selector A / B / C */}
          <div className="space-y-1.5">
            <p className="text-xs font-bold text-ink">{t('namuna9.printFormatLabel')}</p>
            <div className="grid gap-2 sm:grid-cols-3">
              {(['A', 'B', 'C'] as const).map((fmt) => (
                <button
                  key={fmt}
                  type="button"
                  onClick={() => setFormat(fmt)}
                  className={`rounded-xl border p-2.5 text-left transition ${
                    format === fmt
                      ? 'border-brand-700 bg-brand-50 text-brand-900 ring-1 ring-brand-600'
                      : 'border-line bg-white text-ink hover:bg-slate-50'
                  }`}
                >
                  <p className="text-xs font-extrabold">{t(`namuna9.formatShort${fmt}`)}</p>
                  <p className="mt-0.5 text-[11px] text-muted">{t(`namuna9.formatDesc${fmt}`)}</p>
                </button>
              ))}
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            {/* Register Spread Part */}
            <label className="block space-y-1 text-xs">
              <span className="font-bold text-ink">{t('namuna9.printPartLabel')}</span>
              <select
                value={part}
                onChange={(e) => setPart(e.target.value as Namuna9RegisterPart)}
                className="w-full rounded-xl border border-line bg-white px-3 py-2 text-xs font-medium text-ink focus:border-brand-600 focus:outline-none"
              >
                <option value="Both">{t('namuna9.partBoth')}</option>
                <option value="Demand">{t('namuna9.partDemand')}</option>
                <option value="Recovery">{t('namuna9.partRecovery')}</option>
              </select>
            </label>

            {/* Color vs B&W Style */}
            <div className="space-y-1 text-xs">
              <span className="block font-bold text-ink">{t('namuna9.printStyleLabel')}</span>
              <div className="grid grid-cols-2 overflow-hidden rounded-xl border border-line bg-white">
                <button
                  type="button"
                  onClick={() => setStyle('color')}
                  className={`py-2 text-xs font-bold transition ${
                    style === 'color'
                      ? 'bg-brand-700 text-white'
                      : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  {t('namuna9.styleColor')}
                </button>
                <button
                  type="button"
                  onClick={() => setStyle('bw')}
                  className={`py-2 text-xs font-bold transition ${
                    style === 'bw'
                      ? 'bg-slate-800 text-white'
                      : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  {t('namuna9.styleBw')}
                </button>
              </div>
            </div>

            {/* Left Binding Margin */}
            <label className="block space-y-1 text-xs">
              <span className="font-bold text-ink">{t('namuna9.bindingMarginLabel')}</span>
              <select
                value={leftMarginMm}
                onChange={(e) => setLeftMarginMm(Number(e.target.value))}
                className="w-full rounded-xl border border-line bg-white px-3 py-2 text-xs font-medium text-ink focus:border-brand-600 focus:outline-none"
              >
                {MARGIN_OPTIONS.map((mm) => (
                  <option key={mm} value={mm}>
                    {mm === 0 ? t('namuna9.marginNormal') : t('namuna9.marginMm', { mm })}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {/* Live Configuration Pills */}
          <div className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-[11px] font-medium text-slate-700">
            <span className="rounded-md bg-white px-2 py-0.5 font-bold text-slate-900 shadow-2xs">
              A4 Landscape (297×210 mm)
            </span>
            <span>
              {t('namuna9.printScopeSummary', {
                year: workspace.selectedYear.yearLabel,
                ward: wardNo || t('namuna9.allWards'),
                count: workspace.items.length,
              })}
            </span>
            <span>· {t(`namuna9.style${style === 'color' ? 'Color' : 'Bw'}`)}</span>
            <span>· {leftMarginMm} mm</span>
          </div>

          <div className="flex flex-wrap items-center justify-end gap-2 border-t border-line pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-line bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"
            >
              {t('namuna9.cancelBtn')}
            </button>
            <button
              type="button"
              disabled={downloading !== null}
              onClick={handleDownloadCsv}
              className="inline-flex items-center gap-1.5 rounded-xl border border-emerald-300 bg-emerald-50 px-4 py-2 text-xs font-bold text-emerald-800 hover:bg-emerald-100 disabled:opacity-50"
            >
              <FileSpreadsheet className="h-3.5 w-3.5" />
              {downloading === 'csv' ? t('namuna9.processingLabel') : t('namuna9.exportCsvBtn')}
            </button>
            <button
              type="button"
              disabled={downloading !== null}
              onClick={handleDownloadPdf}
              className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-700 px-4 py-2 text-xs font-bold text-white hover:bg-indigo-800 disabled:opacity-50"
            >
              <Download className="h-3.5 w-3.5" />
              {downloading === 'pdf' ? t('namuna9.processingLabel') : t('namuna9.downloadPdfBtn')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
