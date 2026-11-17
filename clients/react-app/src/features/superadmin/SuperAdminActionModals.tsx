import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertTriangle,
  CheckCircle2,
  Download,
  Eye,
  KeyRound,
  RefreshCw,
  ShieldAlert,
  Trash2,
  X,
} from 'lucide-react';
import { ProtectedAssetPreview } from '../../components/ProtectedAssetPreview';
import type { SuperAdminTenantSummary } from '../../services/sprint2Api';
import {
  superAdminApi,
  type SupportImpersonationResult,
  type TenantPaymentSubmission,
  type TenantStatusAction,
} from '../../services/superAdminApi';
import { formatInr } from '../../utils/formatters';
import {
  evaluatePasswordStrength,
  extractProblemValidation,
  generateStrongPassword,
} from '../../utils/validation';

interface ModalShellProps {
  title: string;
  onClose: () => void;
  maxWidthClass?: string;
  children: React.ReactNode;
}

function ModalShell({ title, onClose, maxWidthClass = 'max-w-lg', children }: ModalShellProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/85 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
    >
      <div
        className={`w-full ${maxWidthClass} rounded-2xl border border-slate-700 bg-slate-900 p-6 shadow-2xl space-y-4 text-xs text-slate-200 max-h-[90vh] overflow-y-auto`}
      >
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <h3 className="text-sm font-extrabold text-white">{title}</h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function ImpersonateModal({
  tenant,
  onClose,
  onStarted,
}: {
  tenant: SuperAdminTenantSummary;
  onClose: () => void;
  onStarted: (result: SupportImpersonationResult) => void;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = reason.trim();
    if (trimmed.length < 10) {
      setError(t('impersonation.errReasonShort'));
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const result = await superAdminApi.impersonateTenant(tenant.tenantId, trimmed);
      onStarted(result);
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to start impersonation.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ModalShell title={t('impersonation.modalTitle')} onClose={onClose}>
      <div className="rounded-xl border border-amber-500/40 bg-amber-950/30 p-3.5 text-amber-200 space-y-1.5">
        <p className="font-bold flex items-center gap-1.5">
          <Eye className="h-4 w-4 text-amber-400" />
          <span>
            {tenant.panchayatNameMr} ({tenant.panchayatNameEn}) · LGD {tenant.lgdCode}
          </span>
        </p>
        <p className="text-[11px] leading-relaxed text-amber-100/90">
          {t('impersonation.modalNotice', {
            gp: tenant.panchayatNameEn,
            lgd: tenant.lgdCode,
          })}
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
          {error}
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <label className="block space-y-1.5 font-bold text-slate-300">
          <span>{t('impersonation.reasonInputLabel')}</span>
          <textarea
            rows={3}
            required
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t('impersonation.reasonPlaceholder')}
            className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-white placeholder:text-slate-500 focus:border-amber-400 focus:outline-none"
          />
        </label>

        <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-semibold text-slate-300 hover:bg-slate-700"
          >
            {t('superadmin.cancelBtn')}
          </button>
          <button
            type="submit"
            disabled={busy}
            className="inline-flex items-center gap-1.5 rounded-lg bg-amber-500 px-4 py-2 font-extrabold text-slate-950 hover:bg-amber-400 disabled:opacity-60"
          >
            <Eye className="h-3.5 w-3.5" />
            {busy ? t('impersonation.startingBtn') : t('impersonation.startBtn')}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

export function StatusActionModal({
  tenant,
  action,
  onClose,
  onCompleted,
}: {
  tenant: SuperAdminTenantSummary;
  action: TenantStatusAction;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [trialDays, setTrialDays] = useState(14);
  const [durationDays, setDurationDays] = useState(365);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await superAdminApi.updateTenantStatus(tenant.tenantId, {
        action,
        trialDays: action === 'GrantTrial' ? trialDays : undefined,
        durationDays: action === 'DirectRenew' || action === 'Activate' ? durationDays : undefined,
        reason: reason.trim() || undefined,
      });
      onCompleted();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to update GP license status.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ModalShell title={t('superadminBilling.statusModalTitle')} onClose={onClose}>
      <div className="rounded-xl border border-slate-800 bg-slate-950 p-3 space-y-1">
        <p className="font-bold text-white">
          {tenant.panchayatNameMr} ({tenant.panchayatNameEn})
        </p>
        <p className="mono text-[11px] text-amber-300">
          LGD {tenant.lgdCode} · Action: {action}
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
          {error}
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        {action === 'GrantTrial' && (
          <label className="block space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.trialDaysLabel')}</span>
            <input
              type="number"
              min={1}
              max={90}
              required
              value={trialDays}
              onChange={(e) => setTrialDays(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>
        )}

        {(action === 'DirectRenew' || action === 'Activate') && (
          <label className="block space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.durationDaysLabel')}</span>
            <select
              value={durationDays}
              onChange={(e) => setDurationDays(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            >
              <option value={365}>{t('superadmin.daysOption', { days: 365 })}</option>
              <option value={730}>{t('superadmin.daysOption', { days: 730 })}</option>
              <option value={1095}>{t('superadmin.daysOption', { days: 1095 })}</option>
            </select>
          </label>
        )}

        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('superadminBilling.statusReasonLabel')}</span>
          <textarea
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
          />
        </label>

        <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-semibold text-slate-300 hover:bg-slate-700"
          >
            {t('superadmin.cancelBtn')}
          </button>
          <button
            type="submit"
            disabled={busy}
            className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500 disabled:opacity-60"
          >
            <RefreshCw className="h-3.5 w-3.5" />
            {busy ? '...' : t('superadminBilling.confirmStatusBtn')}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

export function ResetPasswordModal({
  tenant,
  onClose,
}: {
  tenant: SuperAdminTenantSummary;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [newPassword, setNewPassword] = useState(() => generateStrongPassword());
  const [targetUsername, setTargetUsername] = useState(tenant.adminUsername ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resetDoneUser, setResetDoneUser] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!evaluatePasswordStrength(newPassword).isValid) {
      setError(t('validation.passwordRules'));
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const res = await superAdminApi.resetAdminPassword(
        tenant.tenantId,
        newPassword,
        targetUsername,
      );
      setResetDoneUser(res.username);
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to reset admin password.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ModalShell title={t('superadminBilling.resetPassModalTitle')} onClose={onClose}>
      <div className="rounded-xl border border-slate-800 bg-slate-950 p-3 space-y-1">
        <p className="font-bold text-white">
          {tenant.panchayatNameMr} ({tenant.panchayatNameEn})
        </p>
        <p className="mono text-[11px] text-amber-300">LGD {tenant.lgdCode}</p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
          {error}
        </div>
      )}

      {resetDoneUser ? (
        <div className="space-y-4">
          <div className="rounded-xl border border-emerald-500/40 bg-emerald-950/40 p-4 text-emerald-200 space-y-2">
            <p className="font-bold flex items-center gap-1.5">
              <CheckCircle2 className="h-4 w-4 text-emerald-400" />
              {t('superadminBilling.resetSuccessMsg', { username: resetDoneUser })}
            </p>
            <p className="mono text-sm font-extrabold text-white bg-slate-950 px-3 py-2 rounded-lg border border-slate-800">
              {newPassword}
            </p>
          </div>
          <div className="flex justify-end">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500"
            >
              OK
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
          <label className="block space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.targetUsernameOptional')}</span>
            <input
              type="text"
              value={targetUsername}
              onChange={(e) => setTargetUsername(e.target.value)}
              placeholder="e.g. gs_171391"
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>

          <div className="space-y-1">
            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-300">{t('superadminBilling.newPasswordLabel')}</span>
              <button
                type="button"
                onClick={() => setNewPassword(generateStrongPassword())}
                className="text-[11px] font-bold text-amber-300 hover:underline inline-flex items-center gap-1"
              >
                <KeyRound className="h-3 w-3" />
                {t('superadmin.generatePassBtn')}
              </button>
            </div>
            <input
              type="text"
              required
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono font-bold text-emerald-300"
            />
          </div>

          <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-semibold text-slate-300 hover:bg-slate-700"
            >
              {t('superadmin.cancelBtn')}
            </button>
            <button
              type="submit"
              disabled={busy}
              className="inline-flex items-center gap-1.5 rounded-lg bg-amber-500 px-4 py-2 font-extrabold text-slate-950 hover:bg-amber-400 disabled:opacity-60"
            >
              <KeyRound className="h-3.5 w-3.5" />
              {busy ? '...' : t('superadminBilling.confirmResetBtn')}
            </button>
          </div>
        </form>
      )}
    </ModalShell>
  );
}

export function ApprovePaymentModal({
  submission,
  onClose,
  onCompleted,
}: {
  submission: TenantPaymentSubmission;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [durationDays, setDurationDays] = useState(365);
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await superAdminApi.approvePayment(submission.submissionId, durationDays, notes);
      onCompleted();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to approve payment submission.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ModalShell title={t('superadminBilling.approveModalTitle')} onClose={onClose}>
      <div className="rounded-xl border border-slate-800 bg-slate-950 p-3 space-y-1">
        <div className="flex items-center justify-between gap-2">
          <p className="font-bold text-white">
            {submission.panchayatNameMr} ({submission.lgdCode})
          </p>
          <span className="mono font-extrabold text-emerald-400">
            {formatInr(submission.amount)}
          </span>
        </div>
        <p className="mono text-[11px] text-amber-300">
          UTR: {submission.utrReference} · {submission.paymentMode}
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
          {error}
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('superadminBilling.durationDaysLabel')}</span>
          <input
            type="number"
            min={30}
            max={1825}
            required
            value={durationDays}
            onChange={(e) => setDurationDays(Number(e.target.value))}
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
          />
        </label>

        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('superadminBilling.notesOptionalLabel')}</span>
          <textarea
            rows={2}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
          />
        </label>

        <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-semibold text-slate-300 hover:bg-slate-700"
          >
            {t('superadmin.cancelBtn')}
          </button>
          <button
            type="submit"
            disabled={busy}
            className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500 disabled:opacity-60"
          >
            <CheckCircle2 className="h-3.5 w-3.5" />
            {busy ? '...' : t('superadminBilling.confirmApproveBtn')}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

export function RejectPaymentModal({
  submission,
  onClose,
  onCompleted,
}: {
  submission: TenantPaymentSubmission;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (reason.trim().length < 5) {
      setError(t('superadminBilling.errRejectReasonShort'));
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await superAdminApi.rejectPayment(submission.submissionId, reason.trim());
      onCompleted();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to reject payment submission.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ModalShell title={t('superadminBilling.rejectModalTitle')} onClose={onClose}>
      <div className="rounded-xl border border-slate-800 bg-slate-950 p-3 space-y-1">
        <p className="font-bold text-white">
          {submission.panchayatNameMr} ({submission.lgdCode})
        </p>
        <p className="mono text-[11px] text-amber-300">
          UTR: {submission.utrReference} · {formatInr(submission.amount)}
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
          {error}
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('superadminBilling.rejectReasonLabel')}</span>
          <textarea
            rows={3}
            required
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t('superadminBilling.rejectReasonPlaceholder')}
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
          />
        </label>

        <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-semibold text-slate-300 hover:bg-slate-700"
          >
            {t('superadmin.cancelBtn')}
          </button>
          <button
            type="submit"
            disabled={busy}
            className="inline-flex items-center gap-1.5 rounded-lg bg-rose-600 px-4 py-2 font-bold text-white hover:bg-rose-500 disabled:opacity-60"
          >
            <ShieldAlert className="h-3.5 w-3.5" />
            {busy ? '...' : t('superadminBilling.confirmRejectBtn')}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

export function PermanentDeleteModal({
  tenant,
  onClose,
  onCompleted,
}: {
  tenant: SuperAdminTenantSummary;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [confirmLgd, setConfirmLgd] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (confirmLgd.trim() !== tenant.lgdCode.trim()) {
      setError(t('superadminBilling.confirmLgdMismatch'));
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await superAdminApi.permanentDeleteTenant(tenant.tenantId, confirmLgd.trim());
      onCompleted();
    } catch (err) {
      setError(extractProblemValidation(err, 'Permanent deletion failed.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ModalShell title={t('superadminBilling.permDeleteTitle')} onClose={onClose}>
      <div className="rounded-xl border border-rose-500/40 bg-rose-950/50 p-3.5 text-rose-200 flex items-start gap-2.5">
        <AlertTriangle className="h-5 w-5 shrink-0 text-rose-400" />
        <p className="text-[11px] leading-relaxed">
          {t('superadminBilling.permDeleteWarning', {
            gp: tenant.panchayatNameEn,
            lgd: tenant.lgdCode,
          })}
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
          {error}
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('superadminBilling.confirmLgdLabel', { lgd: tenant.lgdCode })}</span>
          <input
            type="text"
            required
            value={confirmLgd}
            onChange={(e) => setConfirmLgd(e.target.value)}
            placeholder={tenant.lgdCode}
            className="w-full rounded-lg border border-rose-500/40 bg-slate-950 px-3 py-2 mono font-bold text-rose-300"
          />
        </label>

        <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-semibold text-slate-300 hover:bg-slate-700"
          >
            {t('superadmin.cancelBtn')}
          </button>
          <button
            type="submit"
            disabled={busy || confirmLgd.trim() !== tenant.lgdCode.trim()}
            className="inline-flex items-center gap-1.5 rounded-lg bg-rose-600 px-4 py-2 font-bold text-white hover:bg-rose-500 disabled:opacity-50"
          >
            <Trash2 className="h-3.5 w-3.5" />
            {busy ? '...' : t('superadminBilling.permanentDeleteBtn')}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

export function SlipPreviewModal({
  assetKey,
  utrReference,
  onClose,
}: {
  assetKey: string;
  utrReference: string;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [downloading, setDownloading] = useState(false);

  async function handleDownload() {
    setDownloading(true);
    try {
      await superAdminApi.downloadProtectedAsset(assetKey, `payment-slip-${utrReference}`);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <ModalShell title={t('superadminBilling.slipPreviewTitle')} onClose={onClose} maxWidthClass="max-w-2xl">
      <div className="space-y-3">
        <p className="mono text-[11px] text-amber-300">
          UTR: {utrReference} · Key: {assetKey}
        </p>
        <div className="rounded-xl border border-slate-700 bg-slate-950 p-3 flex justify-center min-h-[200px]">
          <ProtectedAssetPreview assetKey={assetKey} alt={`Slip ${utrReference}`} />
        </div>
        <p className="text-[11px] text-slate-400">
          {t('superadminBilling.slipPreviewFallbackHint')}
        </p>
      </div>
      <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
        <button
          type="button"
          onClick={() => void handleDownload()}
          disabled={downloading}
          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 font-bold text-slate-200 hover:bg-slate-700"
        >
          <Download className="h-3.5 w-3.5" />
          {t('superadminBilling.downloadSlipBtn')}
        </button>
        <button
          type="button"
          onClick={onClose}
          className="rounded-lg bg-amber-500 px-4 py-2 font-bold text-slate-950 hover:bg-amber-400"
        >
          OK
        </button>
      </div>
    </ModalShell>
  );
}
