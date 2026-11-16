import { Download, QrCode, ShieldAlert, X } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { QrMatrixSvg } from '../../components/QrMatrixSvg';
import type { Namuna9KaBillRowDto, Namuna9KaWorkspaceDto } from '../../services/namuna9KaApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';

interface Namuna9KaBillPreviewModalProps {
  workspace: Namuna9KaWorkspaceDto;
  bill: Namuna9KaBillRowDto;
  downloadingPdf: boolean;
  onClose: () => void;
  onDownloadSinglePdf: (propertyId: string) => Promise<void>;
}

function fmtNum(val: number): string {
  return val.toLocaleString('en-IN', { maximumFractionDigits: 2 });
}

export function Namuna9KaBillPreviewModal({
  workspace,
  bill,
  downloadingPdf,
  onClose,
  onDownloadSinglePdf,
}: Namuna9KaBillPreviewModalProps) {
  const { t, i18n } = useTranslation();
  const [copyTab, setCopyTab] = useState<'taxpayer' | 'gp'>('taxpayer');

  const h = bill.heads;
  const feesCurrent = h.noticeFee + h.warrantFee + h.penaltyAmount;
  const waterArr = h.generalWaterArrears + h.specialWaterArrears;
  const waterCur = h.generalWaterCurrent + h.specialWaterCurrent;

  const ownerName = bill.primaryOwnerNameMr || bill.primaryOwnerNameEn || '—';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-3 backdrop-blur-xs sm:p-5">
      <div className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-line bg-card shadow-2xl">
        {/* Top Modal Bar */}
        <div className="flex items-center justify-between gap-3 border-b border-line bg-slate-50 px-5 py-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-brand-700 text-white">
              <QrCode className="h-4 w-4" />
            </div>
            <div>
              <h2 className="text-sm font-extrabold text-ink">{t('namuna9Ka.previewModalTitle')}</h2>
              <p className="text-[11px] text-muted">
                {bill.billNo || t('namuna9Ka.statusPendingGen')} · {bill.wardNo}/{bill.propertyNo}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <div className="inline-flex overflow-hidden rounded-lg border border-line bg-white p-0.5 text-[11px] font-bold">
              <button
                type="button"
                onClick={() => setCopyTab('taxpayer')}
                className={`rounded-md px-2.5 py-1 transition ${
                  copyTab === 'taxpayer'
                    ? 'bg-rose-800 text-white'
                    : 'text-slate-600 hover:text-ink'
                }`}
              >
                {t('namuna9Ka.copyTaxpayer')}
              </button>
              <button
                type="button"
                onClick={() => setCopyTab('gp')}
                className={`rounded-md px-2.5 py-1 transition ${
                  copyTab === 'gp'
                    ? 'bg-brand-700 text-white'
                    : 'text-slate-600 hover:text-ink'
                }`}
              >
                {t('namuna9Ka.copyGp')}
              </button>
            </div>

            <button
              type="button"
              onClick={onClose}
              aria-label={t('namuna9.cancelBtn')}
              className="rounded-lg p-1.5 text-muted hover:bg-slate-200/70 hover:text-ink"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>

        {/* Scrollable Bill Card Preview matching 13_namuna_9ka_bill_print_template.html */}
        <div className="overflow-y-auto p-4 sm:p-5">
          <article className="space-y-3 rounded-xl border-2 border-brand-700 bg-white p-4 text-xs shadow-xs">
            {/* GP Header */}
            <div className="border-b-2 border-brand-700 pb-2 text-center">
              <p className="text-base font-extrabold text-brand-700">
                {workspace.configuredPayeeName || t('namuna9Ka.billHeadingFallback')}
              </p>
              <p className="text-[11px] font-semibold text-slate-600">
                {t('namuna9Ka.statutorySubheader', { year: workspace.selectedYear.yearLabel })}
              </p>
            </div>

            {/* Banner & Copy Pill */}
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-1.5">
              <span className="text-xs font-extrabold text-rose-900">
                {t('namuna9Ka.billBannerTitle')}
              </span>
              <span
                className={`rounded-full px-2.5 py-0.5 text-[10.5px] font-bold text-white ${
                  copyTab === 'gp' ? 'bg-brand-700' : 'bg-rose-800'
                }`}
              >
                {copyTab === 'gp' ? t('namuna9Ka.copyGp') : t('namuna9Ka.copyTaxpayer')}
              </span>
            </div>

            {/* Meta Row */}
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-dashed border-slate-300 pb-2 text-[11px]">
              <span>
                {t('namuna9Ka.billNoLabel')}:{' '}
                <strong className="mono text-ink">
                  {bill.billNo || t('namuna9Ka.previewPlaceholder')}
                </strong>
              </span>
              <span>
                {t('namuna9.assessmentYearLabel')}:{' '}
                <strong>{workspace.selectedYear.yearLabel}</strong>
              </span>
              <span>
                {t('namuna9Ka.issueDateLabel')}:{' '}
                <strong>
                  {bill.issueDate ? formatDateOnly(bill.issueDate, i18n.language) : '—'}
                </strong>
              </span>
              <span className="text-rose-800">
                {t('namuna9Ka.dueDateLabel')}:{' '}
                <strong>
                  {bill.dueDate
                    ? formatDateOnly(bill.dueDate, i18n.language)
                    : t('namuna9Ka.graceDaysHint', { days: workspace.noticeGraceDays })}
                </strong>
              </span>
            </div>

            {/* Owner Details + Live Dynamic UPI QR */}
            <div className="flex flex-col justify-between gap-4 border-b border-slate-200 pb-3 sm:flex-row">
              <div className="space-y-1 text-xs">
                <p>
                  <span className="text-muted">{t('namuna9.colOwner')}: </span>
                  <strong className="text-ink">{ownerName}</strong>
                  {bill.primaryOwnerNameEn && bill.primaryOwnerNameMr && (
                    <span className="ml-1 text-[11px] text-muted">({bill.primaryOwnerNameEn})</span>
                  )}
                </p>
                <p>
                  <span className="text-muted">{t('namuna9.colWardProp')}: </span>
                  <strong className="mono text-ink">
                    {bill.wardNo} / {bill.propertyNo}
                  </strong>
                  <span className="ml-2 text-muted">
                    · {t('properties.waterConnection')}: <strong>{bill.waterConnectionCode}</strong>
                  </span>
                </p>
                <p>
                  <span className="text-muted">{t('properties.occupierName')}: </span>
                  <span className="text-ink">{bill.occupierName}</span>
                </p>
                {(bill.primaryOwnerAadhaarMasked || bill.primaryOwnerMobileMasked) && (
                  <p className="mono text-[11px] text-slate-600">
                    {bill.primaryOwnerAadhaarMasked ? `UID: ${bill.primaryOwnerAadhaarMasked}` : ''}
                    {bill.primaryOwnerAadhaarMasked && bill.primaryOwnerMobileMasked ? ' · ' : ''}
                    {bill.primaryOwnerMobileMasked ? `Mob: ${bill.primaryOwnerMobileMasked}` : ''}
                  </p>
                )}
                {bill.rebateCutoffDate && bill.netPayableAmount > 0 && (
                  <p className="inline-block rounded bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-800">
                    {t('namuna9Ka.rebateEligibleChip', {
                      pct: workspace.earlyRebatePct,
                      date: formatDateOnly(bill.rebateCutoffDate, i18n.language),
                    })}
                  </p>
                )}
              </div>

              {/* Dynamic NPCI UPI QR Card */}
              <div className="flex shrink-0 flex-col items-center rounded-xl border border-brand-200 bg-brand-50/40 p-2.5 text-center">
                {bill.upiPayUri ? (
                  <>
                    <QrMatrixSvg
                      value={bill.upiPayUri}
                      sizePx={96}
                      label={`UPI QR ${bill.billNo || bill.propertyNo}`}
                    />
                    <span className="mono mt-1.5 text-[10px] font-bold text-brand-800">
                      {bill.upiIdUsed}
                    </span>
                    <span className="text-[10px] text-muted">{t('namuna9Ka.scanToPayLabel')}</span>
                  </>
                ) : (
                  <div className="flex h-24 w-28 flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-slate-300 bg-slate-50 px-2 text-[10.5px] text-slate-600">
                    <ShieldAlert className="h-4 w-4 text-amber-600" />
                    <span>
                      {bill.netPayableAmount <= 0
                        ? t('namuna9Ka.noDuesQrMsg')
                        : t('namuna9Ka.noUpiQrMsg')}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {bill.upiPayUri && (
              <div className="rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1.5">
                <p className="text-[10px] font-bold uppercase tracking-wider text-muted">
                  NPCI UPI Payment URI
                </p>
                <p className="mono break-all text-[10.5px] text-slate-700">{bill.upiPayUri}</p>
              </div>
            )}

            {/* Itemized Tax Table */}
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-[11.5px]">
                <thead>
                  <tr className="bg-slate-100 font-bold text-slate-700">
                    <th className="border border-slate-300 px-2.5 py-1.5 text-left">
                      {t('namuna9Ka.colTaxHead')}
                    </th>
                    <th className="border border-slate-300 px-2 py-1.5 text-right">
                      {t('namuna9Ka.colArrears')}
                    </th>
                    <th className="border border-slate-300 px-2 py-1.5 text-right">
                      {t('namuna9Ka.colCurrent')}
                    </th>
                    <th className="border border-slate-300 px-2 py-1.5 text-right">
                      {t('namuna9Ka.colTotal')}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <TaxRow label={t('namuna9.headHouse')} arr={h.buildingArrears} cur={h.buildingCurrent} />
                  <TaxRow label={t('namuna9.headLighting')} arr={h.lightingArrears} cur={h.lightingCurrent} />
                  <TaxRow label={t('namuna9.headHealth')} arr={h.healthArrears} cur={h.healthCurrent} />
                  <TaxRow label={t('namuna9.headSanitation')} arr={h.sanitationArrears} cur={h.sanitationCurrent} />
                  {h.toiletArrears + h.toiletCurrent > 0 && (
                    <TaxRow label={t('formula.colToiletTax')} arr={h.toiletArrears} cur={h.toiletCurrent} />
                  )}
                  <TaxRow label={t('formula.tabWaterTax')} arr={waterArr} cur={waterCur} />
                  {h.otherArrears + h.otherCurrent > 0 && (
                    <TaxRow label={t('namuna9.headOther')} arr={h.otherArrears} cur={h.otherCurrent} />
                  )}
                  {h.carriedFeesAndPenalty + feesCurrent > 0 && (
                    <TaxRow
                      label={t('namuna9Ka.feesRowLabel')}
                      arr={h.carriedFeesAndPenalty}
                      cur={feesCurrent}
                    />
                  )}
                  {h.rebateAmount > 0 && (
                    <TaxRow
                      label={t('namuna9Ka.rebateRowLabel')}
                      arr={0}
                      cur={-h.rebateAmount}
                      positiveHighlight
                    />
                  )}
                  {h.paidAmount > 0 && (
                    <TaxRow
                      label={t('namuna9Ka.paidRowLabel')}
                      arr={0}
                      cur={-h.paidAmount}
                      positiveHighlight
                    />
                  )}
                  <tr className="bg-slate-100 font-extrabold text-ink">
                    <td className="border border-slate-300 px-2.5 py-1.5">
                      {t('namuna9Ka.totalPayableLabel')}
                    </td>
                    <td className="border border-slate-300 px-2 py-1.5 text-right tabular-nums">
                      {fmtNum(bill.arrearsAmount)}
                    </td>
                    <td className="border border-slate-300 px-2 py-1.5 text-right tabular-nums">
                      {fmtNum(bill.currentAmount)}
                    </td>
                    <td className="border border-slate-300 px-2 py-1.5 text-right tabular-nums text-brand-800">
                      {formatInr(bill.netPayableAmount)}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            {/* Highlight Payable + Marathi Amount Words */}
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-emerald-500 bg-emerald-50 px-3 py-2">
              <div>
                <p className="text-[11px] font-bold text-emerald-900">
                  {t('namuna9Ka.totalPayableLabel')}
                </p>
                <p className="text-xs font-extrabold text-ink">
                  {t('namuna9Ka.inWordsPrefix')}: {bill.amountInWordsMr}
                </p>
              </div>
              <p className="mono text-lg font-extrabold text-emerald-800">
                {formatInr(bill.netPayableAmount)}
              </p>
            </div>

            {/* Mandatory Section 129(1) 15-Day Notice Clause */}
            <div className="rounded-lg border border-rose-200 border-l-4 border-l-rose-800 bg-rose-50/70 px-3 py-2 text-[11px] leading-snug text-rose-950">
              <strong>{t('namuna9Ka.statutoryNoticePrefix')}: </strong>
              {t('namuna9Ka.statutoryNoticeBody', { days: workspace.noticeGraceDays })}
            </div>
          </article>
        </div>

        {/* Footer Actions */}
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-line bg-slate-50 px-5 py-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border border-line bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-100"
          >
            {t('namuna9.closeBtn')}
          </button>
          <button
            type="button"
            disabled={downloadingPdf}
            onClick={() => onDownloadSinglePdf(bill.propertyId)}
            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-xs font-bold text-white hover:bg-brand-800 disabled:opacity-50"
          >
            <Download className="h-3.5 w-3.5" />
            {downloadingPdf ? t('namuna9.processingLabel') : t('namuna9Ka.downloadSinglePdfBtn')}
          </button>
        </div>
      </div>
    </div>
  );
}

function TaxRow({
  label,
  arr,
  cur,
  positiveHighlight = false,
}: {
  label: string;
  arr: number;
  cur: number;
  positiveHighlight?: boolean;
}) {
  const tone = positiveHighlight ? 'text-emerald-700 font-semibold' : 'text-ink';
  return (
    <tr>
      <td className={`border border-slate-300 px-2.5 py-1 ${tone}`}>{label}</td>
      <td className={`border border-slate-300 px-2 py-1 text-right tabular-nums ${tone}`}>
        {fmtNum(arr)}
      </td>
      <td className={`border border-slate-300 px-2 py-1 text-right tabular-nums ${tone}`}>
        {fmtNum(cur)}
      </td>
      <td className={`border border-slate-300 px-2 py-1 text-right font-semibold tabular-nums ${tone}`}>
        {fmtNum(arr + cur)}
      </td>
    </tr>
  );
}

