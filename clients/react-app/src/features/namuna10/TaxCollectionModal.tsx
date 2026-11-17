import { useQuery } from '@tanstack/react-query';
import {
  Banknote,
  Building,
  CheckCircle2,
  CreditCard,
  Info,
  Lock,
  Printer,
  QrCode,
  ReceiptText,
  Sparkles,
  Wallet,
  X,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ValidationSummary } from '../../components/ValidationSummary';
import {
  namuna10Api,
  PaymentMode,
  type Namuna10RegisterRowDto,
  type Namuna10RegisterWorkspaceDto,
  type PaymentModeCode,
  type TaxReceiptDto,
} from '../../services/namuna10Api';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { computeLiveFifoPreview, type FifoPreviewHeadRow } from './fifoPreview';

interface TaxCollectionModalProps {
  workspace: Namuna10RegisterWorkspaceDto;
  row: Namuna10RegisterRowDto;
  canCollect: boolean;
  onClose: () => void;
  onCollected: (receipt: TaxReceiptDto, pdfNotice?: string | null) => Promise<void>;
}

function getTodayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function paymentModeBadgeClass(mode: PaymentModeCode): string {
  switch (mode) {
    case PaymentMode.Upi:
      return 'border-indigo-200 bg-indigo-50 text-indigo-800';
    case PaymentMode.Cheque:
      return 'border-amber-200 bg-amber-50 text-amber-800';
    case PaymentMode.NeftRtgs:
    case PaymentMode.OnlineGateway:
      return 'border-cyan-200 bg-cyan-50 text-cyan-800';
    default:
      return 'border-emerald-200 bg-emerald-50 text-emerald-800';
  }
}

export function TaxCollectionModal({
  workspace,
  row,
  canCollect,
  onClose,
  onCollected,
}: TaxCollectionModalProps) {
  const { t, i18n } = useTranslation();
  const isLocked = workspace.selectedYear.isLocked;
  const readOnly = isLocked || !canCollect || row.remainingBalance <= 0;

  const ownerDisplay = row.primaryOwnerNameMr || row.primaryOwnerNameEn || '—';

  const [operationId, setOperationId] = useState<string>(() => crypto.randomUUID());
  const [bookNo, setBookNo] = useState<string>('BOOK-1');
  const [receiptDate, setReceiptDate] = useState<string>(getTodayIso);
  const [paymentMode, setPaymentMode] = useState<PaymentModeCode>(PaymentMode.Cash);
  const [payerName, setPayerName] = useState<string>(ownerDisplay === '—' ? '' : ownerDisplay);
  const [referenceNo, setReferenceNo] = useState<string>('');
  const [bankName, setBankName] = useState<string>('');
  const [chequeDate, setChequeDate] = useState<string>(getTodayIso);
  const [remarks, setRemarks] = useState<string>('');
  const [applyEarlyRebate, setApplyEarlyRebate] = useState<boolean>(false);
  const [amountInput, setAmountInput] = useState<string>(
    row.remainingBalance > 0 ? String(row.remainingBalance) : '',
  );

  const [submitting, setSubmitting] = useState<boolean>(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);
  const [localNotice, setLocalNotice] = useState<string | null>(null);

  const receiptsQuery = useQuery({
    queryKey: ['namuna10-property-receipts', workspace.selectedYear.id, row.propertyId],
    queryFn: () =>
      namuna10Api.listReceipts({
        yearId: workspace.selectedYear.id,
        propertyId: row.propertyId,
        includeCancelled: true,
      }),
  });

  const cutoffDate = row.rebateCutoffDate || workspace.defaultRebateCutoffDate;
  const canCheckEarlyRebate =
    !readOnly &&
    !row.isTaxExempt &&
    !row.earlyRebateApplied &&
    row.rebateAmount === 0 &&
    row.currentDue.generalTaxTotal > 0 &&
    receiptDate <= cutoffDate;

  const effectiveEarlyRebateEstimate = useMemo(() => {
    if (!applyEarlyRebate || !canCheckEarlyRebate) return 0;
    const raw = row.currentDue.generalTaxTotal * (workspace.earlyRebatePct / 100);
    return Math.min(Math.round(raw * 100) / 100, row.currentDue.total);
  }, [applyEarlyRebate, canCheckEarlyRebate, row.currentDue.generalTaxTotal, row.currentDue.total, workspace.earlyRebatePct]);

  const effectiveNetRemaining = Math.max(
    0,
    Math.round((row.remainingBalance - effectiveEarlyRebateEstimate) * 100) / 100,
  );

  const parsedAmount = Math.max(0, Number.parseFloat(amountInput) || 0);

  const fifoPreview = useMemo(
    () => computeLiveFifoPreview(row, parsedAmount, effectiveEarlyRebateEstimate),
    [row, parsedAmount, effectiveEarlyRebateEstimate],
  );

  const visiblePreviewRows = useMemo(
    () =>
      fifoPreview.rows.filter(
        (r) => r.key !== 'toilet' || r.arrearsDue > 0 || r.currentDue > 0 || r.totalPaid > 0,
      ),
    [fifoPreview.rows],
  );

  const rowLabel = (key: FifoPreviewHeadRow['key']): string => {
    switch (key) {
      case 'fees':
        return t('namuna10.headFeesPenalty');
      case 'house':
        return t('namuna9.headHouse');
      case 'lighting':
        return t('namuna9.headLighting');
      case 'health':
        return t('namuna9.headHealth');
      case 'sanitation':
        return t('namuna9.headSanitation');
      case 'toilet':
        return t('formula.colToiletTax');
      case 'generalWater':
        return t('namuna9.headGenWater');
      case 'specialWater':
        return t('namuna9.headSpecWater');
      default:
        return t('namuna9.headOther');
    }
  };

  const modeLabel = (mode: PaymentModeCode): string => {
    switch (mode) {
      case PaymentMode.Upi:
        return t('namuna10.modeUpi');
      case PaymentMode.Cheque:
        return t('namuna10.modeCheque');
      case PaymentMode.NeftRtgs:
        return t('namuna10.modeNeft');
      case PaymentMode.OnlineGateway:
        return t('namuna10.modeOnline');
      default:
        return t('namuna10.modeCash');
    }
  };

  const handleDownloadPdf = async (receiptId: string, receiptNo?: string) => {
    setDownloadingId(receiptId);
    setLocalNotice(null);
    try {
      await namuna10Api.downloadDualReceiptPdf(receiptId, receiptNo);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna10.pdfNotAvailableYet'));
      setLocalNotice(parsed.summary || t('namuna10.pdfNotAvailableYet'));
    } finally {
      setDownloadingId(null);
    }
  };

  const handleSubmit = async (alsoPrintPdf: boolean) => {
    setErrSummary(null);
    setErrItems([]);
    setLocalNotice(null);

    if (parsedAmount <= 0) {
      setErrSummary(t('namuna10.errAmountRequired'));
      return;
    }

    setSubmitting(true);
    try {
      const receipt = await namuna10Api.collectTax({
        propertyId: row.propertyId,
        assessmentYearId: workspace.selectedYear.id,
        operationId,
        amountPaid: Math.round(parsedAmount * 100) / 100,
        paymentMode,
        receiptDate: receiptDate || undefined,
        bookNo: bookNo.trim() || undefined,
        payerName: payerName.trim() || undefined,
        referenceNo: referenceNo.trim() || undefined,
        bankName: paymentMode === PaymentMode.Cheque ? bankName.trim() || undefined : undefined,
        chequeDate: paymentMode === PaymentMode.Cheque && chequeDate ? chequeDate : undefined,
        remarks: remarks.trim() || undefined,
        applyEligibleEarlyRebate: canCheckEarlyRebate && applyEarlyRebate,
      });

      let pdfNote: string | null = null;
      if (alsoPrintPdf) {
        try {
          await namuna10Api.downloadDualReceiptPdf(receipt.receiptId, receipt.receiptNo);
        } catch {
          pdfNote = t('namuna10.savedPdfPendingHint', { receiptNo: receipt.receiptNo });
        }
      }

      // Generate fresh idempotency key ONLY after confirmed receipt creation
      setOperationId(crypto.randomUUID());
      await onCollected(receipt, pdfNote);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('namuna10.errCollectFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  };

  const historyItems = receiptsQuery.data?.items ?? [];

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-900/65 p-2 backdrop-blur-xs sm:p-4"
      role="dialog"
      aria-modal="true"
    >
      <div className="flex max-h-[94vh] w-full max-w-6xl flex-col overflow-hidden rounded-2xl border border-line bg-card shadow-2xl">
        {/* Modal Header */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-slate-50 px-5 py-3.5">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-700 text-white shadow-xs">
              <Wallet className="h-5 w-5" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-base font-extrabold text-ink">{t('namuna10.modalTitle')}</h2>
                <span className="rounded-full bg-indigo-100 px-2.5 py-0.5 font-mono text-xs font-extrabold text-indigo-900">
                  {row.wardNo} / {row.propertyNo}
                </span>
                {row.activeBillNo && (
                  <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 font-mono text-[11px] font-bold text-emerald-800">
                    {row.activeBillNo}
                  </span>
                )}
              </div>
              <p className="mt-0.5 text-xs text-muted">
                {ownerDisplay}
                {row.primaryOwnerMobileMasked ? ` · ${row.primaryOwnerMobileMasked}` : ''}
                {row.primaryOwnerAadhaarMasked ? ` · Aadhaar: ${row.primaryOwnerAadhaarMasked}` : ''}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className="rounded-xl border border-rose-200 bg-rose-50 px-3 py-1.5 text-right">
              <p className="text-[10px] font-bold uppercase tracking-wider text-rose-700">
                {t('namuna10.colBalance')}
              </p>
              <p className="mono text-sm font-extrabold text-rose-800">
                {formatInr(effectiveNetRemaining)}
              </p>
            </div>
            <button
              type="button"
              onClick={onClose}
              aria-label={t('namuna9.closeBtn')}
              className="rounded-xl border border-line bg-white p-2 text-slate-600 hover:bg-slate-100"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>

        {/* Body: 7/12 Collection Form + 5/12 Receipt History */}
        <div className="grid flex-1 grid-cols-1 gap-0 overflow-y-auto lg:grid-cols-12">
          {/* LEFT 7/12 — POS Entry & Live FIFO Waterfall */}
          <div className="space-y-4 border-b border-line p-4 sm:p-5 lg:col-span-7 lg:border-b-0 lg:border-r">
            {isLocked && (
              <div className="flex items-center gap-2 rounded-xl border border-slate-300 bg-slate-100 px-3 py-2 text-xs font-semibold text-slate-800">
                <Lock className="h-4 w-4 shrink-0" />
                <span>{t('namuna10.lockedYearNotice', { year: workspace.selectedYear.yearLabel })}</span>
              </div>
            )}

            {!isLocked && row.remainingBalance <= 0 && (
              <div className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2.5 text-xs font-bold text-emerald-800">
                <CheckCircle2 className="h-4 w-4 shrink-0" />
                <span>{t('namuna10.propertyAlreadySettledNotice')}</span>
              </div>
            )}

            <ValidationSummary summary={errSummary} items={errItems} />

            {/* Book / Receipt Meta Row */}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <div>
                <label className="block text-[11px] font-bold text-muted">
                  {t('namuna10.receiptNoAutoLabel')}
                </label>
                <div className="mt-1 flex h-9 items-center rounded-xl border border-line bg-slate-100 px-3 font-mono text-xs font-bold text-slate-600">
                  RCP-{workspace.selectedYear.startYear}-XXXX
                </div>
              </div>
              <div>
                <label htmlFor="pos-book-no" className="block text-[11px] font-bold text-muted">
                  {t('namuna10.bookNoLabel')}
                </label>
                <input
                  id="pos-book-no"
                  type="text"
                  disabled={readOnly}
                  value={bookNo}
                  onChange={(e) => setBookNo(e.target.value)}
                  className="mt-1 h-9 w-full rounded-xl border border-line bg-white px-3 text-xs font-bold text-ink focus:border-indigo-600 focus:outline-none disabled:bg-slate-100"
                />
              </div>
              <div>
                <label htmlFor="pos-receipt-date" className="block text-[11px] font-bold text-muted">
                  {t('namuna10.receiptDateLabel')}
                </label>
                <input
                  id="pos-receipt-date"
                  type="date"
                  disabled={readOnly}
                  value={receiptDate}
                  onChange={(e) => setReceiptDate(e.target.value)}
                  className="mt-1 h-9 w-full rounded-xl border border-line bg-white px-3 text-xs font-bold text-ink focus:border-indigo-600 focus:outline-none disabled:bg-slate-100"
                />
              </div>
            </div>

            {/* Payer Name & Payment Mode Tabs */}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label htmlFor="pos-payer-name" className="block text-[11px] font-bold text-muted">
                  {t('namuna10.payerNameLabel')}
                </label>
                <input
                  id="pos-payer-name"
                  type="text"
                  disabled={readOnly}
                  value={payerName}
                  onChange={(e) => setPayerName(e.target.value)}
                  placeholder={ownerDisplay}
                  className="mt-1 h-9 w-full rounded-xl border border-line bg-white px-3 text-xs font-semibold text-ink focus:border-indigo-600 focus:outline-none disabled:bg-slate-100"
                />
              </div>

              <div>
                <span className="block text-[11px] font-bold text-muted">
                  {t('namuna10.paymentModeLabel')}
                </span>
                <div className="mt-1 grid grid-cols-4 gap-1 rounded-xl border border-line bg-slate-50 p-1">
                  {[
                    { mode: PaymentMode.Cash, label: t('namuna10.modeCash'), icon: Banknote },
                    { mode: PaymentMode.Upi, label: t('namuna10.modeUpi'), icon: QrCode },
                    { mode: PaymentMode.Cheque, label: t('namuna10.modeCheque'), icon: CreditCard },
                    { mode: PaymentMode.NeftRtgs, label: t('namuna10.modeNeft'), icon: Building },
                  ].map((item) => {
                    const Icon = item.icon;
                    const active = paymentMode === item.mode;
                    return (
                      <button
                        key={item.mode}
                        type="button"
                        disabled={readOnly}
                        onClick={() => setPaymentMode(item.mode)}
                        className={`inline-flex items-center justify-center gap-1 rounded-lg py-1.5 text-[11px] font-bold transition ${
                          active
                            ? 'bg-indigo-700 text-white shadow-2xs'
                            : 'text-slate-600 hover:bg-white'
                        } disabled:opacity-50`}
                      >
                        <Icon className="h-3.5 w-3.5 shrink-0" />
                        <span className="truncate">{item.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>

            {/* Conditional Payment Reference Details */}
            {paymentMode !== PaymentMode.Cash && (
              <div className="grid grid-cols-1 gap-3 rounded-xl border border-indigo-200 bg-indigo-50/50 p-3 sm:grid-cols-3">
                <div>
                  <label htmlFor="pos-ref-no" className="block text-[11px] font-bold text-indigo-950">
                    {paymentMode === PaymentMode.Cheque
                      ? t('namuna10.chequeNoLabel')
                      : t('namuna10.utrRefLabel')}{' '}
                    *
                  </label>
                  <input
                    id="pos-ref-no"
                    type="text"
                    disabled={readOnly}
                    value={referenceNo}
                    onChange={(e) => setReferenceNo(e.target.value)}
                    placeholder={
                      paymentMode === PaymentMode.Cheque ? 'e.g. CHQ-44102' : 'e.g. UTR4091182'
                    }
                    className="mt-1 h-9 w-full rounded-lg border border-indigo-200 bg-white px-2.5 font-mono text-xs font-bold text-ink focus:border-indigo-600 focus:outline-none"
                  />
                </div>

                {paymentMode === PaymentMode.Cheque && (
                  <>
                    <div>
                      <label htmlFor="pos-bank-name" className="block text-[11px] font-bold text-indigo-950">
                        {t('namuna10.bankNameLabel')} *
                      </label>
                      <input
                        id="pos-bank-name"
                        type="text"
                        disabled={readOnly}
                        value={bankName}
                        onChange={(e) => setBankName(e.target.value)}
                        placeholder="State Bank of India"
                        className="mt-1 h-9 w-full rounded-lg border border-indigo-200 bg-white px-2.5 text-xs font-semibold text-ink focus:border-indigo-600 focus:outline-none"
                      />
                    </div>
                    <div>
                      <label htmlFor="pos-cheque-date" className="block text-[11px] font-bold text-indigo-950">
                        {t('namuna10.chequeDateLabel')}
                      </label>
                      <input
                        id="pos-cheque-date"
                        type="date"
                        disabled={readOnly}
                        value={chequeDate}
                        onChange={(e) => setChequeDate(e.target.value)}
                        className="mt-1 h-9 w-full rounded-lg border border-indigo-200 bg-white px-2.5 text-xs font-semibold text-ink focus:border-indigo-600 focus:outline-none"
                      />
                    </div>
                  </>
                )}
              </div>
            )}

            {/* Early Rebate Offer + Amount Entry Row */}
            <div className="space-y-2.5 rounded-xl border border-line bg-slate-50/70 p-3.5">
              {canCheckEarlyRebate && (
                <label className="flex cursor-pointer items-center justify-between gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-950">
                  <span className="flex items-center gap-2 font-bold">
                    <input
                      type="checkbox"
                      checked={applyEarlyRebate}
                      onChange={(e) => {
                        const checked = e.target.checked;
                        setApplyEarlyRebate(checked);
                        const est = checked
                          ? Math.min(
                              Math.round(
                                row.currentDue.generalTaxTotal * (workspace.earlyRebatePct / 100) * 100,
                              ) / 100,
                              row.currentDue.total,
                            )
                          : 0;
                        const nextFull = Math.max(0, Math.round((row.remainingBalance - est) * 100) / 100);
                        setAmountInput(String(nextFull));
                      }}
                      className="h-4 w-4 rounded accent-emerald-700"
                    />
                    <Sparkles className="h-3.5 w-3.5 text-emerald-700" />
                    {t('namuna10.applyEarlyRebateCheck', {
                      pct: workspace.earlyRebatePct,
                      cutoff: formatDateOnly(cutoffDate, i18n.language),
                    })}
                  </span>
                  <span className="mono font-extrabold text-emerald-800">
                    -{formatInr(
                      Math.min(
                        Math.round(
                          row.currentDue.generalTaxTotal * (workspace.earlyRebatePct / 100) * 100,
                        ) / 100,
                        row.currentDue.total,
                      ),
                    )}
                  </span>
                </label>
              )}

              <div className="flex flex-wrap items-end justify-between gap-3">
                <div className="flex-1 min-w-[200px]">
                  <label htmlFor="pos-amount-paid" className="block text-[11px] font-bold text-ink">
                    {t('namuna10.amountPaidLabel')} (₹) *
                  </label>
                  <input
                    id="pos-amount-paid"
                    type="number"
                    min="0"
                    step="0.01"
                    disabled={readOnly}
                    value={amountInput}
                    onChange={(e) => setAmountInput(e.target.value)}
                    className="mono mt-1 h-10 w-full rounded-xl border-2 border-indigo-300 bg-white px-3 text-base font-extrabold text-ink focus:border-indigo-700 focus:outline-none disabled:bg-slate-100"
                  />
                </div>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={readOnly}
                    onClick={() => setAmountInput(String(effectiveNetRemaining))}
                    className="h-10 rounded-xl border border-emerald-300 bg-emerald-50 px-3.5 text-xs font-extrabold text-emerald-800 hover:bg-emerald-100 disabled:opacity-50"
                  >
                    {t('namuna10.payFullDuesBtn')}
                  </button>
                  <button
                    type="button"
                    disabled={readOnly}
                    onClick={() => setAmountInput('')}
                    className="h-10 rounded-xl border border-line bg-white px-3 text-xs font-bold text-slate-600 hover:bg-slate-100 disabled:opacity-50"
                  >
                    {t('namuna10.clearAmountBtn')}
                  </button>
                </div>
              </div>
            </div>

            {/* Live Statutory FIFO Waterfall Preview Table */}
            <div className="overflow-hidden rounded-xl border border-line">
              <div className="flex items-center justify-between border-b border-line bg-slate-100 px-3 py-2 text-[11px] font-extrabold text-slate-700">
                <span>{t('namuna10.fifoPreviewTitle')}</span>
                <span className="text-indigo-800">
                  {t('namuna10.fifoPrioritySubtitle')}
                </span>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-xs">
                  <thead>
                    <tr className="border-b border-line bg-slate-50 text-[10.5px] font-bold uppercase text-slate-600">
                      <th className="px-2.5 py-1.5 text-left">{t('namuna9Ka.colTaxHead')}</th>
                      <th className="px-2 py-1.5 text-right">{t('namuna10.colAvailArr')}</th>
                      <th className="px-2 py-1.5 text-right">{t('namuna10.colAllocArr')}</th>
                      <th className="px-2 py-1.5 text-right">{t('namuna10.colAvailCur')}</th>
                      <th className="px-2 py-1.5 text-right">{t('namuna10.colAllocCur')}</th>
                      <th className="px-2.5 py-1.5 text-right">{t('namuna9Ka.colTotal')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {visiblePreviewRows.map((r) => (
                      <tr key={r.key} className={r.totalPaid > 0 ? 'bg-indigo-50/35' : 'bg-white'}>
                        <td className="px-2.5 py-1.5 font-semibold text-ink">{rowLabel(r.key)}</td>
                        <td className="px-2 py-1.5 text-right tabular-nums text-muted">
                          {formatInr(r.arrearsDue)}
                        </td>
                        <td className="px-2 py-1.5 text-right tabular-nums font-bold text-indigo-800">
                          {r.arrearsPaid > 0 ? formatInr(r.arrearsPaid) : '—'}
                        </td>
                        <td className="px-2 py-1.5 text-right tabular-nums text-muted">
                          {formatInr(r.currentDue)}
                        </td>
                        <td className="px-2 py-1.5 text-right tabular-nums font-bold text-emerald-800">
                          {r.currentPaid > 0 ? formatInr(r.currentPaid) : '—'}
                        </td>
                        <td className="px-2.5 py-1.5 text-right tabular-nums font-extrabold text-ink">
                          {formatInr(r.totalPaid)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t-2 border-slate-300 bg-slate-50 font-extrabold text-ink">
                      <td className="px-2.5 py-2">{t('namuna10.totalAllocatedRow')}</td>
                      <td className="px-2 py-2 text-right tabular-nums" colSpan={2}>
                        {formatInr(fifoPreview.arrearsPaidTotal)}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums" colSpan={2}>
                        {formatInr(fifoPreview.currentPaidTotal)}
                      </td>
                      <td className="px-2.5 py-2 text-right tabular-nums text-indigo-800">
                        {formatInr(fifoPreview.appliedTowardDemand)}
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>

              {fifoPreview.excessCredit > 0 && (
                <div className="flex items-center justify-between border-t border-amber-200 bg-amber-50 px-3 py-1.5 text-[11px] font-bold text-amber-900">
                  <span>{t('namuna10.excessCreditNote')}</span>
                  <span className="mono">{formatInr(fifoPreview.excessCredit)}</span>
                </div>
              )}
            </div>

            {/* Remarks + Submit Buttons */}
            <div>
              <label htmlFor="pos-remarks" className="block text-[11px] font-bold text-muted">
                {t('namuna10.remarksLabel')}
              </label>
              <input
                id="pos-remarks"
                type="text"
                disabled={readOnly}
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                placeholder={t('namuna9.remarksPlaceholder')}
                className="mt-1 h-9 w-full rounded-xl border border-line bg-white px-3 text-xs text-ink focus:border-indigo-600 focus:outline-none"
              />
            </div>

            <div className="flex flex-wrap items-center justify-end gap-2 pt-1">
              <button
                type="button"
                onClick={onClose}
                className="rounded-xl border border-line bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"
              >
                {t('namuna9.closeBtn')}
              </button>

              {canCollect && (
                <>
                  <button
                    type="button"
                    disabled={readOnly || submitting || parsedAmount <= 0}
                    onClick={() => handleSubmit(false)}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-700 px-4 py-2 text-xs font-extrabold text-white shadow-xs hover:bg-indigo-800 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    {submitting ? t('namuna9.processingLabel') : t('namuna10.saveReceiptBtn')}
                  </button>

                  <button
                    type="button"
                    disabled={readOnly || submitting || parsedAmount <= 0}
                    onClick={() => handleSubmit(true)}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-700 px-4 py-2 text-xs font-extrabold text-white shadow-xs hover:bg-emerald-800 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <Printer className="h-3.5 w-3.5" />
                    {submitting ? t('namuna9.processingLabel') : t('namuna10.saveAndPrintBtn')}
                  </button>
                </>
              )}
            </div>
          </div>

          {/* RIGHT 5/12 — Summary & Property Receipt History (Immutable per ADR-008) */}
          <div className="flex flex-col space-y-3 bg-slate-50/70 p-4 sm:p-5 lg:col-span-5">
            <div className="grid grid-cols-3 gap-2">
              <div className="rounded-xl border border-line bg-white p-2.5 text-center">
                <p className="text-[10px] font-bold uppercase text-muted">{t('namuna10.summaryNet')}</p>
                <p className="mono mt-0.5 text-xs font-extrabold text-ink">{formatInr(row.netDemand)}</p>
              </div>
              <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-2.5 text-center">
                <p className="text-[10px] font-bold uppercase text-emerald-700">{t('namuna10.summaryPaid')}</p>
                <p className="mono mt-0.5 text-xs font-extrabold text-emerald-800">
                  {formatInr(row.paidAmount)}
                </p>
              </div>
              <div className="rounded-xl border border-rose-200 bg-rose-50 p-2.5 text-center">
                <p className="text-[10px] font-bold uppercase text-rose-700">{t('namuna10.summaryBal')}</p>
                <p className="mono mt-0.5 text-xs font-extrabold text-rose-800">
                  {formatInr(row.remainingBalance)}
                </p>
              </div>
            </div>

            {localNotice && (
              <div className="flex items-start gap-2 rounded-xl border border-brand-200 bg-brand-50 px-3 py-2 text-[11px] font-semibold text-brand-900">
                <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-brand-700" />
                <span>{localNotice}</span>
              </div>
            )}

            <div className="flex items-center justify-between">
              <h3 className="flex items-center gap-1.5 text-xs font-extrabold uppercase tracking-wide text-slate-700">
                <ReceiptText className="h-3.5 w-3.5 text-indigo-700" />
                {t('namuna10.historyHeading')} ({historyItems.length})
              </h3>
              <span className="text-[10.5px] font-medium text-muted">
                {t('namuna10.immutableHint')}
              </span>
            </div>

            {receiptsQuery.isLoading ? (
              <div className="py-10 text-center text-xs text-muted">{t('namuna9.processingLabel')}</div>
            ) : historyItems.length === 0 ? (
              <div className="rounded-xl border border-dashed border-slate-300 bg-white p-8 text-center">
                <ReceiptText className="mx-auto h-8 w-8 text-slate-300" />
                <p className="mt-2 text-xs font-bold text-slate-600">{t('namuna10.noReceiptsHistory')}</p>
              </div>
            ) : (
              <div className="space-y-2.5 overflow-y-auto pr-0.5">
                {historyItems.map((rec) => {
                  const arrAmt = rec.headAllocation?.arrearsAllocated?.total ?? 0;
                  const curAmt = rec.headAllocation?.currentAllocated?.total ?? 0;
                  const feeAmt =
                    (rec.headAllocation?.noticeFeePaid ?? 0) +
                    (rec.headAllocation?.warrantFeePaid ?? 0) +
                    (rec.headAllocation?.penaltyPaid ?? 0);

                  return (
                    <div
                      key={rec.receiptId}
                      className={`rounded-xl border p-3 shadow-2xs ${
                        rec.isCancelled
                          ? 'border-rose-200 bg-rose-50/70 opacity-85'
                          : 'border-line bg-white'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div>
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span
                              className={`font-mono text-xs font-extrabold ${
                                rec.isCancelled ? 'line-through text-rose-700' : 'text-indigo-800'
                              }`}
                            >
                              {rec.receiptNo}
                            </span>
                            <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[10px] font-semibold text-slate-600">
                              {rec.bookNo}
                            </span>
                            <span
                              className={`rounded-full border px-2 py-0.5 text-[10px] font-bold ${paymentModeBadgeClass(rec.paymentMode)}`}
                            >
                              {modeLabel(rec.paymentMode)}
                            </span>
                            {rec.isCancelled && (
                              <span className="rounded-full bg-rose-700 px-2 py-0.5 text-[10px] font-extrabold text-white">
                                {t('namuna10.cancelledBadge')}
                              </span>
                            )}
                          </div>
                          <p className="mt-0.5 text-[11px] text-muted">
                            {formatDateOnly(rec.receiptDate, i18n.language)}
                            {rec.referenceNo ? ` · Ref: ${rec.referenceNo}` : ''}
                          </p>
                        </div>

                        <div className="text-right">
                          <p
                            className={`mono text-sm font-extrabold ${
                              rec.isCancelled ? 'line-through text-rose-700' : 'text-emerald-800'
                            }`}
                          >
                            {formatInr(rec.totalPaid)}
                          </p>
                          <button
                            type="button"
                            disabled={downloadingId === rec.receiptId}
                            onClick={() => handleDownloadPdf(rec.receiptId, rec.receiptNo)}
                            className="mt-1 inline-flex items-center gap-1 rounded-lg border border-line bg-slate-50 px-2 py-1 text-[10.5px] font-bold text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                          >
                            <Printer className="h-3 w-3" />
                            {t('namuna10.printCopyBtn')}
                          </button>
                        </div>
                      </div>

                      <div className="mt-2 flex flex-wrap gap-2 border-t border-slate-100 pt-1.5 text-[10.5px] text-slate-600">
                        {feeAmt > 0 && (
                          <span>
                            {t('namuna9.shortFees')}: <strong>{formatInr(feeAmt)}</strong>
                          </span>
                        )}
                        {arrAmt > 0 && (
                          <span>
                            {t('namuna9.subArr')}: <strong>{formatInr(arrAmt)}</strong>
                          </span>
                        )}
                        {curAmt > 0 && (
                          <span>
                            {t('namuna9.subCur')}: <strong>{formatInr(curAmt)}</strong>
                          </span>
                        )}
                        {rec.excessCredit > 0 && (
                          <span className="text-amber-800">
                            Adv: <strong>{formatInr(rec.excessCredit)}</strong>
                          </span>
                        )}
                      </div>

                      {rec.isCancelled && rec.cancelReason && (
                        <p className="mt-1.5 rounded bg-rose-100 px-2 py-1 text-[10.5px] font-semibold text-rose-900">
                          {rec.cancelReason}
                        </p>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

