import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, CreditCard, FileDown, Upload, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ProtectedAssetPreview } from '../../components/ProtectedAssetPreview';
import { QrMatrixSvg } from '../../components/QrMatrixSvg';
import { useAuth } from '../../context/useAuth';
import { sprint2Api } from '../../services/sprint2Api';
import { gpSubscriptionApi } from '../../services/superAdminApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation } from '../../utils/validation';

function RenewalFormInner({
  annualFee,
  isReadOnly,
  onSubmitted,
}: {
  annualFee: number;
  isReadOnly: boolean;
  onSubmitted: () => void;
}) {
  const { t } = useTranslation();
  const [planName, setPlanName] = useState<'Renewal' | 'Standard' | 'Enterprise'>('Renewal');
  const [paymentMode, setPaymentMode] = useState<'UPI' | 'NEFT_RTGS' | 'BANK_DEPOSIT' | 'CHEQUE'>('UPI');
  const [amount, setAmount] = useState(annualFee);
  const [utrReference, setUtrReference] = useState('');
  const [paymentDate, setPaymentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [slipAssetKey, setSlipAssetKey] = useState<string | null>(null);
  const [notes, setNotes] = useState('');
  const [uploadingSlip, setUploadingSlip] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successNotice, setSuccessNotice] = useState(false);

  async function handleFileChange(file: File | undefined) {
    if (!file) return;
    setError(null);
    setUploadingSlip(true);
    try {
      const uploaded = await sprint2Api.uploadProtectedAsset('PAYMENT_SLIP', file);
      setSlipAssetKey(uploaded.assetKey);
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to upload payment slip.').summary);
    } finally {
      setUploadingSlip(false);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (utrReference.trim().length < 6) {
      setError(t('subscription.errUtrShort'));
      return;
    }
    setError(null);
    setSuccessNotice(false);
    setSubmitting(true);
    try {
      await gpSubscriptionApi.submitSubscriptionPayment({
        planName,
        paymentMode,
        amount: Number(amount),
        utrReference: utrReference.trim(),
        paymentDate,
        slipAssetKey,
        notes: notes.trim() || null,
      });
      setUtrReference('');
      setNotes('');
      setSlipAssetKey(null);
      setSuccessNotice(true);
      onSubmitted();
    } catch (err) {
      setError(extractProblemValidation(err, t('subscription.errSubmitFailed')).summary);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={(e) => void handleSubmit(e)} className="space-y-3 text-xs">
      <h3 className="text-sm font-extrabold text-ink">{t('subscription.submitSectionTitle')}</h3>

      {error && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-rose-800">
          {error}
        </div>
      )}

      {successNotice && (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-emerald-800 flex items-center gap-2 font-semibold">
          <CheckCircle2 className="h-4 w-4 text-emerald-600" />
          <span>{t('subscription.submittedSuccess')}</span>
        </div>
      )}

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="space-y-1 font-bold text-slate-700">
          <span>{t('subscription.planLabel')}</span>
          <select
            disabled={isReadOnly}
            value={planName}
            onChange={(e) => setPlanName(e.target.value as 'Renewal' | 'Standard' | 'Enterprise')}
            className="w-full rounded-lg border border-line bg-white px-3 py-2 text-ink"
          >
            <option value="Renewal">Annual Renewal</option>
            <option value="Standard">{t('superadmin.planStandard')}</option>
            <option value="Enterprise">{t('superadmin.planEnterprise')}</option>
          </select>
        </label>

        <label className="space-y-1 font-bold text-slate-700">
          <span>{t('subscription.modeLabel')}</span>
          <select
            disabled={isReadOnly}
            value={paymentMode}
            onChange={(e) =>
              setPaymentMode(e.target.value as 'UPI' | 'NEFT_RTGS' | 'BANK_DEPOSIT' | 'CHEQUE')
            }
            className="w-full rounded-lg border border-line bg-white px-3 py-2 text-ink"
          >
            <option value="UPI">UPI</option>
            <option value="NEFT_RTGS">NEFT / RTGS</option>
            <option value="BANK_DEPOSIT">Bank Deposit / Challan</option>
            <option value="CHEQUE">Cheque</option>
          </select>
        </label>

        <label className="space-y-1 font-bold text-slate-700">
          <span>{t('subscription.amountLabel')}</span>
          <input
            type="number"
            min={1}
            step="0.01"
            required
            disabled={isReadOnly}
            value={amount}
            onChange={(e) => setAmount(Number(e.target.value))}
            className="w-full rounded-lg border border-line bg-white px-3 py-2 mono font-bold text-ink"
          />
        </label>

        <label className="space-y-1 font-bold text-slate-700">
          <span>{t('subscription.paymentDateLabel')}</span>
          <input
            type="date"
            required
            disabled={isReadOnly}
            value={paymentDate}
            onChange={(e) => setPaymentDate(e.target.value)}
            className="w-full rounded-lg border border-line bg-white px-3 py-2 mono text-ink"
          />
        </label>
      </div>

      <label className="block space-y-1 font-bold text-slate-700">
        <span>{t('subscription.utrLabel')}</span>
        <input
          type="text"
          required
          disabled={isReadOnly}
          value={utrReference}
          onChange={(e) => setUtrReference(e.target.value)}
          placeholder={t('subscription.utrPlaceholder')}
          className="w-full rounded-lg border border-line bg-white px-3 py-2 mono font-bold text-ink"
        />
      </label>

      <div className="space-y-2">
        <label className="block font-bold text-slate-700">
          <span>{t('subscription.slipUploadLabel')}</span>
          <div className="mt-1 flex items-center gap-2">
            <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-line bg-surface px-3 py-2 font-bold text-brand-700 hover:bg-brand-50">
              <Upload className="h-3.5 w-3.5" />
              <span>{uploadingSlip ? '...' : t('subscription.slipUploadLabel')}</span>
              <input
                type="file"
                accept="image/png,image/jpeg,application/pdf"
                disabled={isReadOnly || uploadingSlip}
                onChange={(e) => void handleFileChange(e.target.files?.[0])}
                className="sr-only"
              />
            </label>
            {slipAssetKey && (
              <span className="rounded bg-emerald-100 px-2 py-0.5 text-[11px] font-bold text-emerald-800">
                {t('subscription.slipAttached')}
              </span>
            )}
          </div>
        </label>

        {slipAssetKey && (
          <div className="rounded-lg border border-line bg-surface p-2 max-w-[200px]">
            <ProtectedAssetPreview assetKey={slipAssetKey} alt="Payment slip" />
          </div>
        )}
      </div>

      <label className="block space-y-1 font-bold text-slate-700">
        <span>{t('subscription.notesLabel')}</span>
        <textarea
          rows={2}
          disabled={isReadOnly}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          className="w-full rounded-lg border border-line bg-white px-3 py-2 text-ink"
        />
      </label>

      <button
        type="submit"
        disabled={isReadOnly || submitting || uploadingSlip}
        className="w-full rounded-xl bg-brand-700 px-4 py-2.5 text-xs font-extrabold text-white hover:bg-brand-800 disabled:opacity-50 transition"
      >
        {submitting ? t('subscription.submittingBtn') : t('subscription.submitBtn')}
      </button>
    </form>
  );
}

export function SubscriptionRenewalModal({ onClose }: { onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const { isImpersonating, user } = useAuth();
  const queryClient = useQueryClient();
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const subQuery = useQuery({
    queryKey: ['onboarding', 'subscription-status', user?.activeTenantId],
    queryFn: gpSubscriptionApi.getSubscriptionStatus,
  });

  const status = subQuery.data;
  const acct = status?.platformPaymentAccount;
  const upiUri = acct
    ? `upi://pay?pa=${encodeURIComponent(acct.upiId)}&pn=${encodeURIComponent(acct.accountName)}&am=${acct.annualFee.toFixed(2)}&cu=INR&tn=${encodeURIComponent(`GP-${status.lgdCode}-RENEWAL`)}`
    : '';

  const isReadOnly =
    isImpersonating ||
    (user?.role !== 'Gramsevak' && user?.role !== 'GP_Admin' && user?.role !== 'SuperAdmin');

  async function handleDownloadInvoice(id: string) {
    setDownloadingId(id);
    try {
      await gpSubscriptionApi.downloadGpInvoicePdf(id);
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/70 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
    >
      <div className="w-full max-w-4xl rounded-2xl border border-line bg-white p-6 shadow-2xl space-y-5 max-h-[90vh] overflow-y-auto text-xs">
        <div className="flex items-center justify-between border-b border-line pb-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-700 text-white">
              <CreditCard className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-extrabold text-ink">{t('subscription.modalTitle')}</h2>
              {status && (
                <p className="mono text-[11px] text-muted">
                  {status.panchayatNameMr} ({status.panchayatNameEn}) · LGD {status.lgdCode}
                </p>
              )}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100 hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {subQuery.isLoading && <div className="h-48 animate-pulse rounded-xl bg-slate-100" />}

        {status && acct && (
          <>
            <div className="grid gap-6 md:grid-cols-2">
              {/* Left: Platform UPI QR & Bank Account Details */}
              <div className="space-y-4 rounded-xl border border-brand-200 bg-brand-50/50 p-4">
                <p className="font-extrabold text-brand-900 text-sm">
                  {t('subscription.upiQrTitle')}
                </p>

                <div className="flex flex-col items-center rounded-xl border border-brand-200 bg-white p-4 space-y-2">
                  <QrMatrixSvg value={upiUri} sizePx={164} label={acct.upiId} />
                  <p className="mono text-xs font-extrabold text-brand-700">{acct.upiId}</p>
                  <p className="text-lg font-extrabold text-ink mono">
                    {formatInr(acct.annualFee)}
                  </p>
                  <p className="text-[11px] text-muted">
                    {t('subscription.annualFeeLabel', { gst: acct.gstPercent })}
                  </p>
                </div>

                <div className="space-y-1.5 rounded-xl border border-line bg-white p-3.5 text-[11px]">
                  <p className="font-extrabold text-ink">{t('subscription.bankDetailsTitle')}</p>
                  <p>
                    <span className="text-muted">{t('subscription.accountLabel')}: </span>
                    <strong className="text-ink">{acct.accountName}</strong>
                  </p>
                  <p>
                    <span className="text-muted">{t('subscription.bankLabel')}: </span>
                    <span className="text-ink">
                      {acct.bankName}, {acct.branchName}
                    </span>
                  </p>
                  <p>
                    <span className="text-muted">{t('subscription.accNoIfscLabel')}: </span>
                    <span className="mono font-bold text-ink">
                      {acct.accountNumber} · {acct.ifscCode}
                    </span>
                  </p>
                  {acct.supportPhone && (
                    <p>
                      <span className="text-muted">Helpline: </span>
                      <span className="mono text-ink">{acct.supportPhone}</span>
                    </p>
                  )}
                </div>
              </div>

              {/* Right: UTR Submission Form */}
              <RenewalFormInner
                key={acct.annualFee}
                annualFee={acct.annualFee}
                isReadOnly={isReadOnly}
                onSubmitted={() => {
                  void queryClient.invalidateQueries({ queryKey: ['onboarding', 'subscription-status'] });
                  void queryClient.invalidateQueries({ queryKey: ['dashboard', 'summary'] });
                }}
              />
            </div>

            {/* Submissions & Approved Invoices History */}
            <div className="space-y-2 border-t border-line pt-4">
              <h3 className="text-sm font-extrabold text-ink">{t('subscription.historyTitle')}</h3>
              {status.recentSubmissions.length === 0 && status.paymentHistory.length === 0 ? (
                <p className="text-muted">{t('subscription.noHistory')}</p>
              ) : (
                <div className="divide-y divide-line rounded-xl border border-line">
                  {status.recentSubmissions.map((s) => (
                    <div
                      key={s.submissionId}
                      className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2.5"
                    >
                      <div>
                        <p className="font-bold text-ink">
                          {s.utrReference} · <span className="mono">{formatInr(s.amount)}</span>
                        </p>
                        <p className="mono text-[11px] text-muted">
                          {s.paymentMode} · {formatDateOnly(s.paymentDate, i18n.language)} · {s.status}
                          {s.rejectionReason ? ` · ${s.rejectionReason}` : ''}
                        </p>
                      </div>
                      {s.status === 'Approved' && (
                        <button
                          type="button"
                          disabled={downloadingId === s.submissionId}
                          onClick={() => void handleDownloadInvoice(s.submissionId)}
                          className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-700 hover:bg-brand-100"
                        >
                          <FileDown className="h-3.5 w-3.5" />
                          {t('subscription.downloadInvoiceBtn')}
                        </button>
                      )}
                    </div>
                  ))}
                  {status.paymentHistory
                    .filter((h) => !status.recentSubmissions.some((s) => s.approvedSubscriptionPaymentId === h.paymentId))
                    .map((h) => (
                      <div
                        key={h.paymentId}
                        className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2.5"
                      >
                        <div>
                          <p className="font-bold text-ink">
                            {h.invoiceNo || h.paymentReference || h.planName} ·{' '}
                            <span className="mono">{formatInr(h.amount)}</span>
                          </p>
                          <p className="mono text-[11px] text-muted">
                            {h.paymentMode} · {h.durationDays}d · {formatDateOnly(h.paymentDateUtc, i18n.language)}
                          </p>
                        </div>
                        <button
                          type="button"
                          disabled={downloadingId === h.paymentId}
                          onClick={() => void handleDownloadInvoice(h.paymentId)}
                          className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-700 hover:bg-brand-100"
                        >
                          <FileDown className="h-3.5 w-3.5" />
                          {t('subscription.downloadInvoiceBtn')}
                        </button>
                      </div>
                    ))}
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

