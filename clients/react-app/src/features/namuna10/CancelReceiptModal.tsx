import { useMutation } from '@tanstack/react-query';
import { AlertTriangle, Eye, EyeOff, KeyRound, ShieldAlert, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ValidationSummary } from '../../components/ValidationSummary';
import { namuna10Api, type TaxReceiptDto } from '../../services/namuna10Api';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';

interface CancelReceiptModalProps {
  receipt: TaxReceiptDto;
  onClose: () => void;
  onCancelled: (cancelledReceipt: TaxReceiptDto) => Promise<void> | void;
}

export function CancelReceiptModal({ receipt, onClose, onCancelled }: CancelReceiptModalProps) {
  const { t, i18n } = useTranslation();
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  const mutation = useMutation({
    mutationFn: () =>
      namuna10Api.cancelReceipt(receipt.receiptId, {
        confirmPassword,
        cancelReason: cancelReason.trim(),
      }),
    onSuccess: async (updated) => {
      setConfirmPassword('');
      setCancelReason('');
      await onCancelled(updated);
      onClose();
    },
    onError: (err) => {
      const parsed = extractProblemValidation(err, t('namuna10.errCancelFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    },
  });

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setErrSummary(null);
    setErrItems([]);

    if (cancelReason.trim().length < 5) {
      setErrSummary(t('namuna10.errCancelReasonLength'));
      return;
    }
    if (!confirmPassword) {
      setErrSummary(t('namuna10.errCancelPasswordRequired'));
      return;
    }

    mutation.mutate();
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-[2px]"
      role="dialog"
      aria-modal="true"
      aria-labelledby="cancel-receipt-modal-title"
    >
      <div className="w-full max-w-lg overflow-hidden rounded-2xl border border-rose-200 bg-card shadow-2xl">
        <div className="flex items-center justify-between border-b border-rose-200 bg-rose-700 px-5 py-3.5 text-white">
          <div className="flex items-center gap-2.5">
            <ShieldAlert className="h-5 w-5 shrink-0" />
            <div>
              <h2 id="cancel-receipt-modal-title" className="text-sm font-extrabold">
                {t('namuna10.cancelModalTitle')}
              </h2>
              <p className="text-[11px] text-rose-100">
                {receipt.receiptNo} · {receipt.bookNo} ·{' '}
                {formatDateOnly(receipt.receiptDate, i18n.language)}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('namuna10.closeModalBtn')}
            className="rounded-lg p-1 text-rose-100 hover:bg-rose-600 hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 p-5">
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-950">
            <div className="flex items-start gap-2">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-700" />
              <p className="leading-relaxed">
                {t('namuna10.cancelStatutoryWarning', {
                  receiptNo: receipt.receiptNo,
                  amount: formatInr(receipt.totalPaid),
                })}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 rounded-xl border border-line bg-slate-50 px-3.5 py-2.5 text-xs">
            <div>
              <span className="text-muted">{t('namuna10.wardShort')}: </span>
              <strong className="font-mono text-ink">
                {receipt.wardNo} / {receipt.propertyNo}
              </strong>
            </div>
            <div className="text-right">
              <span className="text-muted">{t('namuna10.paidShort')}: </span>
              <strong className="mono text-rose-800">{formatInr(receipt.totalPaid)}</strong>
            </div>
            <div className="col-span-2 truncate">
              <span className="text-muted">{t('namuna10.payerNameLabel')}: </span>
              <strong className="text-ink">{receipt.payerName}</strong>
            </div>
          </div>

          {errSummary && <ValidationSummary summary={errSummary} items={errItems} />}

          <div>
            <label
              htmlFor="cancel-receipt-reason"
              className="mb-1 block text-xs font-bold text-slate-700"
            >
              {t('namuna10.cancelReasonLabel')} <span className="text-rose-600">*</span>
            </label>
            <textarea
              id="cancel-receipt-reason"
              rows={3}
              required
              minLength={5}
              maxLength={500}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder={t('namuna10.cancelReasonPlaceholder')}
              className="w-full rounded-xl border border-line bg-white px-3 py-2 text-xs text-ink focus:border-rose-600 focus:outline-none"
            />
          </div>

          <div>
            <label
              htmlFor="cancel-receipt-password"
              className="mb-1 flex items-center gap-1.5 text-xs font-bold text-slate-700"
            >
              <KeyRound className="h-3.5 w-3.5 text-rose-700" />
              {t('namuna10.cancelPasswordLabel')} <span className="text-rose-600">*</span>
            </label>
            <div className="relative">
              <input
                id="cancel-receipt-password"
                type={showPassword ? 'text' : 'password'}
                required
                autoComplete="current-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder={t('namuna10.cancelPasswordPlaceholder')}
                className="w-full rounded-xl border border-line bg-white py-2 pl-3 pr-10 text-xs text-ink focus:border-rose-600 focus:outline-none"
              />
              <button
                type="button"
                onClick={() => setShowPassword((prev) => !prev)}
                className="absolute right-2.5 top-2 text-muted hover:text-ink"
                tabIndex={-1}
                aria-label="Toggle password visibility"
              >
                {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>
          </div>

          <div className="flex items-center justify-end gap-2.5 border-t border-line pt-3">
            <button
              type="button"
              onClick={onClose}
              disabled={mutation.isPending}
              className="rounded-xl border border-line bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50"
            >
              {t('namuna10.keepReceiptBtn')}
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="inline-flex items-center gap-1.5 rounded-xl bg-rose-700 px-4 py-2 text-xs font-extrabold text-white shadow-sm hover:bg-rose-800 disabled:opacity-60"
            >
              {mutation.isPending
                ? t('namuna10.cancellingBtn')
                : t('namuna10.confirmCancelBtn')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
