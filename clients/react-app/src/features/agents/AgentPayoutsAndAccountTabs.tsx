import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Award,
  Check,
  CheckCircle2,
  Download,
  Eye,
  EyeOff,
  FileDown,
  KeyRound,
  Printer,
  Search,
  ShieldCheck,
  Wallet,
  X,
} from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ProtectedAssetPreview } from '../../components/ProtectedAssetPreview';
import { KycStatusBadge } from '../../components/StatusBadge';
import {
  type AgentCommissionItem,
  type AgentFullProfile,
  type AgentPayoutItem,
  agentPortalApi,
} from '../../services/agentPortalApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { evaluatePasswordStrength, extractProblemValidation } from '../../utils/validation';

function CommissionStatusPill({ status }: { status: string }) {
  const tone =
    status === 'Settled'
      ? 'border-emerald-500/40 bg-emerald-500/20 text-emerald-300'
      : status === 'PartiallySettled'
        ? 'border-sky-500/40 bg-sky-500/20 text-sky-300'
        : status === 'Requested'
          ? 'border-amber-500/40 bg-amber-500/20 text-amber-300'
          : status === 'ClawedBack'
            ? 'border-rose-500/40 bg-rose-500/20 text-rose-300'
            : 'border-indigo-500/40 bg-indigo-500/20 text-indigo-300';
  return (
    <span className={`inline-flex rounded-full border px-2 py-0.5 text-[10.5px] font-bold ${tone}`}>
      {status}
    </span>
  );
}

export function AgentCommissionsTab() {
  const { t, i18n } = useTranslation();
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [search, setSearch] = useState('');

  const commissionsQuery = useQuery({
    queryKey: ['agent', 'commissions', statusFilter, search],
    queryFn: () =>
      agentPortalApi.listCommissions({
        status: statusFilter === 'ALL' ? undefined : statusFilter,
        search: search.trim() || undefined,
      }),
  });

  const rows: AgentCommissionItem[] = commissionsQuery.data ?? [];

  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 flex flex-wrap items-center justify-between gap-3 no-print">
        <div className="flex flex-wrap items-center gap-2.5 flex-1 min-w-[260px]">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-500" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('agentPortal.searchCommPlaceholder')}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 pl-9 pr-3 py-1.5 text-xs text-slate-100 placeholder:text-slate-500 focus:border-emerald-500"
            />
          </div>

          <select
            aria-label={t('agentPortal.filterCommStatus')}
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-xs text-slate-200"
          >
            <option value="ALL">{t('agentPortal.allStatuses')}</option>
            <option value="Accrued">Accrued</option>
            <option value="Requested">Requested</option>
            <option value="PartiallySettled">PartiallySettled</option>
            <option value="Settled">Settled</option>
            <option value="ClawedBack">ClawedBack</option>
          </select>
        </div>

        <button
          type="button"
          onClick={() => window.print()}
          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-3 py-1.5 text-xs font-semibold text-slate-200 hover:bg-slate-700"
        >
          <Printer className="h-3.5 w-3.5" />
          {t('agentPortal.printLedgerBtn')}
        </button>
      </div>

      {commissionsQuery.isLoading ? (
        <div className="space-y-2">
          {[1, 2, 3].map((n) => (
            <div key={n} className="h-12 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      ) : rows.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-900 p-8 text-center text-xs text-slate-400">
          {t('agentPortal.noCommissionsYet')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-950 text-slate-400">
              <tr>
                <th className="px-4 py-3">{t('agentPortal.colDate')}</th>
                <th className="px-4 py-3">{t('agentPortal.colGpName')}</th>
                <th className="px-4 py-3">{t('agentPortal.colType')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colSalesCollected')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colRateSnapshot')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colAccrued')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colSettled')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colRemaining')}</th>
                <th className="px-4 py-3">{t('agentPortal.colStatus')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/70">
              {rows.map((c) => (
                <tr key={c.commissionId} className="hover:bg-slate-800/40">
                  <td className="px-4 py-3 mono text-slate-400">
                    {formatDateOnly(c.createdAtUtc, i18n.language)}
                  </td>
                  <td className="px-4 py-3">
                    <p className="font-bold text-white">{c.panchayatNameMr}</p>
                    <p className="mono text-[11px] text-amber-300">LGD {c.lgdCode}</p>
                  </td>
                  <td className="px-4 py-3">
                    <span className="rounded bg-slate-800 px-2 py-0.5 text-[11px] font-semibold text-slate-200">
                      {c.commissionType === 'Renewal'
                        ? t('agentPortal.commTypeRenewal')
                        : t('agentPortal.commTypeNew')}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right mono text-slate-300">
                    {formatInr(c.collectedAmount)}
                  </td>
                  <td className="px-4 py-3 text-right mono font-bold text-sky-300">
                    {c.ratePctSnapshot}%
                  </td>
                  <td className="px-4 py-3 text-right mono font-bold text-emerald-400">
                    {formatInr(c.commissionAmount)}
                  </td>
                  <td className="px-4 py-3 text-right mono text-slate-300">
                    {formatInr(c.settledAmount)}
                  </td>
                  <td className="px-4 py-3 text-right mono font-bold text-amber-300">
                    {formatInr(c.remainingPayable)}
                  </td>
                  <td className="px-4 py-3">
                    <CommissionStatusPill status={c.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function AgentPayoutsTab({
  pendingPayableCommission,
  paidCommission,
}: {
  pendingPayableCommission: number;
  paidCommission: number;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();

  const payoutsQuery = useQuery({
    queryKey: ['agent', 'payouts'],
    queryFn: agentPortalApi.listPayouts,
  });

  const [modalOpen, setModalOpen] = useState(false);
  const [amount, setAmount] = useState(() =>
    pendingPayableCommission > 0 ? String(pendingPayableCommission) : '',
  );
  const [paymentMode, setPaymentMode] = useState('UPI');
  const [remarks, setRemarks] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalError, setModalError] = useState<string | null>(null);
  const [successVoucher, setSuccessVoucher] = useState<string | null>(null);
  const [busyDownloadId, setBusyDownloadId] = useState<string | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  function openModal() {
    setModalError(null);
    setAmount(pendingPayableCommission > 0 ? String(pendingPayableCommission) : '');
    setRemarks('');
    setModalOpen(true);
  }

  async function handleRequestSubmit(e: FormEvent) {
    e.preventDefault();
    setModalError(null);
    const num = Number(amount);
    if (!Number.isFinite(num) || num <= 0) {
      setModalError(t('validation.required'));
      return;
    }

    setSubmitting(true);
    try {
      const created = await agentPortalApi.requestPayout({
        payoutAmount: num,
        paymentMode,
        remarks: remarks.trim() || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['agent'] });
      setModalOpen(false);
      setSuccessVoucher(created.voucherNo);
    } catch (err) {
      setModalError(extractProblemValidation(err, 'Withdrawal request failed.').summary);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDownloadVoucher(p: AgentPayoutItem) {
    setDownloadError(null);
    setBusyDownloadId(p.payoutId);
    try {
      await agentPortalApi.downloadVoucherPdf(p.payoutId, p.voucherNo);
    } catch (err) {
      setDownloadError(extractProblemValidation(err, 'Unable to download voucher PDF.').summary);
    } finally {
      setBusyDownloadId(null);
    }
  }

  async function handleDownloadProof(p: AgentPayoutItem) {
    if (!p.receiptAssetKey) return;
    setDownloadError(null);
    setBusyDownloadId(`proof-${p.payoutId}`);
    try {
      await agentPortalApi.downloadProtectedAsset(
        p.receiptAssetKey,
        `${p.voucherNo}-proof`,
      );
    } catch (err) {
      setDownloadError(extractProblemValidation(err, 'Unable to download bank proof.').summary);
    } finally {
      setBusyDownloadId(null);
    }
  }

  const payouts = payoutsQuery.data ?? [];

  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-800 bg-slate-900 p-5 flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-wrap items-center gap-6 text-xs">
          <div>
            <p className="text-slate-400">{t('agentPortal.availableBalanceLabel')}</p>
            <p className="mono text-xl font-extrabold text-amber-300">
              {formatInr(pendingPayableCommission)}
            </p>
          </div>
          <div>
            <p className="text-slate-400">{t('agentPortal.kpiPaidCommission')}</p>
            <p className="mono text-xl font-extrabold text-emerald-400">
              {formatInr(paidCommission)}
            </p>
          </div>
        </div>

        <button
          type="button"
          disabled={pendingPayableCommission <= 0}
          onClick={openModal}
          className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-4 py-2.5 text-xs font-extrabold text-white hover:bg-emerald-500 disabled:opacity-40 transition"
        >
          <Wallet className="h-4 w-4" />
          {t('agentPortal.requestWithdrawalBtn')}
        </button>
      </div>

      {successVoucher && (
        <div className="rounded-xl border border-emerald-500/40 bg-emerald-950/40 p-3.5 text-xs font-bold text-emerald-200 flex items-center gap-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{t('agentPortal.withdrawalRequestedMsg', { voucher: successVoucher })}</span>
        </div>
      )}

      {downloadError && (
        <div className="rounded-xl border border-rose-500/40 bg-rose-950/50 p-3 text-xs text-rose-200">
          {downloadError}
        </div>
      )}

      {payoutsQuery.isLoading ? (
        <div className="space-y-2">
          {[1, 2].map((n) => (
            <div key={n} className="h-12 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      ) : payouts.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-900 p-8 text-center text-xs text-slate-400">
          {t('agentPortal.noPayoutsYet')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-950 text-slate-400">
              <tr>
                <th className="px-4 py-3">{t('agentPortal.colVoucherNo')}</th>
                <th className="px-4 py-3">{t('agentPortal.colPaymentDate')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colAmount')}</th>
                <th className="px-4 py-3">{t('agentPortal.colMode')}</th>
                <th className="px-4 py-3">{t('agentPortal.colUtrRef')}</th>
                <th className="px-4 py-3">{t('agentPortal.colStatus')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colReceipt')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/70">
              {payouts.map((p) => (
                <tr key={p.payoutId} className="hover:bg-slate-800/40">
                  <td className="px-4 py-3 mono font-bold text-amber-300">{p.voucherNo}</td>
                  <td className="px-4 py-3 mono text-slate-300">
                    {formatDateOnly(p.paidAtUtc || p.paymentDate, i18n.language)}
                  </td>
                  <td className="px-4 py-3 text-right mono font-bold text-emerald-400">
                    {formatInr(p.payoutAmount)}
                  </td>
                  <td className="px-4 py-3 mono text-slate-300">{p.paymentMode}</td>
                  <td className="px-4 py-3 mono text-slate-300">{p.utrReference || '—'}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex rounded-full border px-2 py-0.5 text-[10.5px] font-bold ${
                        p.status === 'Disbursed'
                          ? 'border-emerald-500/40 bg-emerald-500/20 text-emerald-300'
                          : p.status === 'Rejected'
                            ? 'border-rose-500/40 bg-rose-500/20 text-rose-300'
                            : 'border-amber-500/40 bg-amber-500/20 text-amber-300'
                      }`}
                    >
                      {p.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="inline-flex items-center gap-2">
                      {p.status === 'Disbursed' && (
                        <button
                          type="button"
                          disabled={busyDownloadId === p.payoutId}
                          onClick={() => void handleDownloadVoucher(p)}
                          className="inline-flex items-center gap-1 rounded-lg border border-emerald-500/40 bg-emerald-500/15 px-2.5 py-1 text-[11px] font-bold text-emerald-300 hover:bg-emerald-500/25 disabled:opacity-50"
                        >
                          <FileDown className="h-3.5 w-3.5" />
                          {t('agentPortal.downloadVoucherBtn')}
                        </button>
                      )}
                      {p.receiptAssetKey && (
                        <button
                          type="button"
                          disabled={busyDownloadId === `proof-${p.payoutId}`}
                          onClick={() => void handleDownloadProof(p)}
                          className="inline-flex items-center gap-1 rounded-lg border border-sky-500/40 bg-sky-500/15 px-2.5 py-1 text-[11px] font-bold text-sky-300 hover:bg-sky-500/25 disabled:opacity-50"
                        >
                          <Download className="h-3.5 w-3.5" />
                          {t('agentPortal.downloadProofBtn')}
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-xs p-4"
          role="dialog"
          aria-modal="true"
        >
          <div className="w-full max-w-md rounded-2xl border border-emerald-500/40 bg-slate-900 p-6 shadow-2xl space-y-4 text-xs">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 className="text-sm font-extrabold text-white">
                {t('agentPortal.requestModalTitle')}
              </h3>
              <button
                type="button"
                aria-label={t('superadmin.cancelBtn')}
                onClick={() => setModalOpen(false)}
                className="rounded-lg p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {modalError && (
              <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200">
                {modalError}
              </div>
            )}

            <form onSubmit={(e) => void handleRequestSubmit(e)} className="space-y-4">
              <div className="rounded-xl border border-slate-800 bg-slate-950 p-3.5 flex items-center justify-between">
                <span className="text-slate-400">{t('agentPortal.availableBalanceLabel')}</span>
                <span className="mono text-base font-extrabold text-amber-300">
                  {formatInr(pendingPayableCommission)}
                </span>
              </div>

              <label className="block space-y-1 font-bold text-slate-300">
                <span>
                  {t('agentPortal.withdrawalAmountLabel')} <span className="text-rose-400">*</span>
                </span>
                <input
                  type="number"
                  step="0.01"
                  min="1"
                  max={pendingPayableCommission}
                  required
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-amber-300 font-bold focus:border-emerald-500 focus:outline-none"
                />
              </label>

              <label className="block space-y-1 font-bold text-slate-300">
                <span>
                  {t('agentPortal.paymentModeLabel')} <span className="text-rose-400">*</span>
                </span>
                <select
                  value={paymentMode}
                  onChange={(e) => setPaymentMode(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100"
                >
                  <option value="UPI">UPI</option>
                  <option value="NEFT_RTGS">NEFT / RTGS</option>
                  <option value="IMPS">IMPS</option>
                  <option value="BANK_TRANSFER">BANK_TRANSFER</option>
                </select>
              </label>

              <label className="block space-y-1 font-bold text-slate-300">
                <span>{t('agentPortal.remarksLabel')}</span>
                <input
                  type="text"
                  value={remarks}
                  onChange={(e) => setRemarks(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
                />
              </label>

              <div className="flex justify-end gap-2 pt-2 border-t border-slate-800">
                <button
                  type="button"
                  onClick={() => setModalOpen(false)}
                  className="rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 font-bold text-slate-300 hover:bg-slate-700"
                >
                  {t('superadmin.cancelBtn')}
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="rounded-lg bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500 disabled:opacity-50"
                >
                  {t('agentPortal.submitWithdrawalBtn')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

function AgentProfileForm({
  profile,
  onSaved,
}: {
  profile: AgentFullProfile;
  onSaved: () => Promise<void>;
}) {
  const { t, i18n } = useTranslation();

  const [email, setEmail] = useState(profile.email);
  const [villageOrCity, setVillageOrCity] = useState(profile.villageOrCity);
  const [district, setDistrict] = useState(profile.district);
  const [talukasCsv, setTalukasCsv] = useState(profile.talukasCsv);
  const [address, setAddress] = useState(profile.address);
  const [bankName, setBankName] = useState(profile.bankName ?? '');
  const [bankBranch, setBankBranch] = useState(profile.bankBranch ?? '');
  const [accountNumber, setAccountNumber] = useState(profile.accountNumber ?? '');
  const [ifscCode, setIfscCode] = useState(profile.ifscCode ?? '');
  const [upiId, setUpiId] = useState(profile.upiId ?? '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedNotice, setSavedNotice] = useState(false);

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSavedNotice(false);
    setSaving(true);
    try {
      await agentPortalApi.updateProfile({
        email: email.trim(),
        address: address.trim(),
        villageOrCity: villageOrCity.trim(),
        district: district.trim(),
        talukasCsv: talukasCsv.trim(),
        bankName: bankName.trim() || null,
        bankBranch: bankBranch.trim() || null,
        accountNumber: accountNumber.trim() || null,
        ifscCode: ifscCode.trim().toUpperCase() || null,
        upiId: upiId.trim() || null,
      });
      await onSaved();
      setSavedNotice(true);
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to update profile.').summary);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="grid gap-6 lg:grid-cols-3 text-xs">
      <div className="space-y-4">
        <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-4">
          <div className="text-center space-y-2 border-b border-slate-800 pb-4">
            <h3 className="text-base font-extrabold text-white">{profile.fullName}</h3>
            <div className="flex items-center justify-center gap-2">
              <span className="rounded-full border border-emerald-500/40 bg-emerald-500/15 px-3 py-0.5 mono font-bold text-emerald-300">
                {profile.agentCode}
              </span>
              <KycStatusBadge status={profile.kycStatus} dark />
            </div>
          </div>

          <div className="space-y-2">
            <div className="flex justify-between py-1 border-b border-slate-800/70">
              <span className="text-slate-400">{t('agent.mobile')}</span>
              <span className="mono font-bold text-amber-300">{profile.mobileNo}</span>
            </div>
            <div className="flex justify-between py-1 border-b border-slate-800/70">
              <span className="text-slate-400">{t('agentPortal.approvedSinceLabel')}</span>
              <span className="mono text-slate-200">
                {formatDateOnly(profile.approvedAtUtc || profile.createdAtUtc, i18n.language)}
              </span>
            </div>
          </div>

          <div className="rounded-xl border border-emerald-500/30 bg-emerald-950/30 p-3.5 space-y-2">
            <p className="font-bold text-emerald-300 flex items-center gap-1.5">
              <ShieldCheck className="h-4 w-4" />
              {t('agentPortal.profileIdentityTitle')}
            </p>
            <div className="flex justify-between mono text-[11px]">
              <span className="text-slate-400">{t('agent.aadhaarLabel')}</span>
              <span className="font-bold text-amber-300">{profile.aadhaarMasked}</span>
            </div>
            <div className="flex justify-between mono text-[11px]">
              <span className="text-slate-400">{t('agent.panLabel')}</span>
              <span className="font-bold text-amber-300">{profile.panMasked}</span>
            </div>
            <p className="text-[10.5px] text-emerald-200/80">{t('agentPortal.dpdpMaskedHint')}</p>

            {profile.aadhaarDocumentKey && (
              <div className="pt-2 space-y-1">
                <p className="font-semibold text-slate-300">{t('superadminAgents.aadhaarDocLabel')}</p>
                <ProtectedAssetPreview
                  assetKey={profile.aadhaarDocumentKey}
                  alt={t('superadminAgents.aadhaarDocLabel')}
                />
                <button
                  type="button"
                  onClick={() =>
                    void agentPortalApi.downloadProtectedAsset(
                      profile.aadhaarDocumentKey!,
                      `${profile.agentCode}-aadhaar`,
                    )
                  }
                  className="text-[11px] font-bold text-sky-300 hover:underline"
                >
                  {t('superadminAgents.downloadRawBtn')}
                </button>
              </div>
            )}
            {profile.panDocumentKey && (
              <div className="pt-2 space-y-1">
                <p className="font-semibold text-slate-300">{t('superadminAgents.panDocLabel')}</p>
                <ProtectedAssetPreview
                  assetKey={profile.panDocumentKey}
                  alt={t('superadminAgents.panDocLabel')}
                />
                <button
                  type="button"
                  onClick={() =>
                    void agentPortalApi.downloadProtectedAsset(
                      profile.panDocumentKey!,
                      `${profile.agentCode}-pan`,
                    )
                  }
                  className="text-[11px] font-bold text-sky-300 hover:underline"
                >
                  {t('superadminAgents.downloadRawBtn')}
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="lg:col-span-2 space-y-5">
        <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-3">
          <h3 className="text-sm font-extrabold text-white flex items-center gap-2">
            <Award className="h-4 w-4 text-amber-400" />
            {t('agentPortal.commissionSlabsTitle')}
          </h3>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="rounded-xl border border-emerald-500/30 bg-slate-950 p-4">
              <p className="text-slate-400">{t('agent.newGpLabel')}</p>
              <p className="mono text-2xl font-extrabold text-emerald-400 mt-1">
                {profile.approvedNewGpRatePct.toFixed(2)}%
              </p>
            </div>
            <div className="rounded-xl border border-sky-500/30 bg-slate-950 p-4">
              <p className="text-slate-400">{t('agent.renewalLabel')}</p>
              <p className="mono text-2xl font-extrabold text-sky-400 mt-1">
                {profile.approvedRenewalRatePct.toFixed(2)}%
              </p>
            </div>
          </div>
        </div>

        <form
          onSubmit={(e) => void handleSave(e)}
          className="rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-4"
        >
          <h3 className="text-sm font-extrabold text-white">{t('agentPortal.profileFormTitle')}</h3>

          {error && (
            <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200">
              {error}
            </div>
          )}
          {savedNotice && (
            <div className="rounded-xl border border-emerald-500/40 bg-emerald-950/40 p-3 font-bold text-emerald-200 flex items-center gap-2">
              <CheckCircle2 className="h-4 w-4 text-emerald-400" />
              {t('agentPortal.profileSavedMsg')}
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.email')}</span>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.villageOrCityLabel')}</span>
              <input
                type="text"
                required
                value={villageOrCity}
                onChange={(e) => setVillageOrCity(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.district')}</span>
              <input
                type="text"
                required
                value={district}
                onChange={(e) => setDistrict(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.talukasCsv')}</span>
              <input
                type="text"
                required
                value={talukasCsv}
                onChange={(e) => setTalukasCsv(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="sm:col-span-2 space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.addressLabel')}</span>
              <input
                type="text"
                required
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.bankName')}</span>
              <input
                type="text"
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.branchName')}</span>
              <input
                type="text"
                value={bankBranch}
                onChange={(e) => setBankBranch(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.accountNo')}</span>
              <input
                type="text"
                value={accountNumber}
                onChange={(e) => setAccountNumber(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.ifscCode')}</span>
              <input
                type="text"
                value={ifscCode}
                onChange={(e) => setIfscCode(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono uppercase text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="sm:col-span-2 space-y-1 font-bold text-slate-300">
              <span>{t('agent.upiIdLabel')}</span>
              <input
                type="text"
                value={upiId}
                onChange={(e) => setUpiId(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300 focus:border-emerald-500 focus:outline-none"
              />
            </label>
          </div>

          <div className="flex justify-end pt-2">
            <button
              type="submit"
              disabled={saving}
              className="rounded-xl bg-emerald-600 px-5 py-2.5 font-extrabold text-white hover:bg-emerald-500 disabled:opacity-50"
            >
              {saving ? t('agentPortal.savingProfileBtn') : t('agentPortal.saveProfileBtn')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function AgentProfileTab({ profile }: { profile: AgentFullProfile }) {
  const queryClient = useQueryClient();
  return (
    <AgentProfileForm
      key={`${profile.agentId}-${profile.email}-${profile.upiId ?? ''}-${profile.accountNumber ?? ''}`}
      profile={profile}
      onSaved={() => queryClient.invalidateQueries({ queryKey: ['agent'] })}
    />
  );
}

export function AgentChangePasswordTab() {
  const { t } = useTranslation();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const strength = evaluatePasswordStrength(newPassword);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    if (newPassword !== confirmPassword) {
      setError(t('agentPortal.passwordMismatchErr'));
      return;
    }

    setSubmitting(true);
    try {
      await agentPortalApi.changePassword({ currentPassword, newPassword });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setSuccess(true);
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to update password.').summary);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-lg rounded-2xl border border-slate-800 bg-slate-900 p-6 space-y-5 text-xs">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-400">
          <KeyRound className="h-5 w-5" />
        </div>
        <h2 className="text-base font-extrabold text-white">
          {t('agentPortal.changePasswordTitle')}
        </h2>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200">
          {error}
        </div>
      )}
      {success && (
        <div className="rounded-xl border border-emerald-500/40 bg-emerald-950/40 p-3 font-bold text-emerald-200 flex items-center gap-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          {t('agentPortal.passwordChangedMsg')}
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('agentPortal.currentPasswordLabel')}</span>
          <input
            type={showPass ? 'text' : 'password'}
            required
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
          />
        </label>

        <div className="space-y-1.5">
          <label className="block font-bold text-slate-300">
            {t('agentPortal.newPasswordLabel')}
          </label>
          <div className="relative">
            <input
              type={showPass ? 'text' : 'password'}
              required
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 pr-9 mono text-white focus:border-emerald-500 focus:outline-none"
            />
            <button
              type="button"
              aria-label={showPass ? t('login.hidePassword') : t('login.showPassword')}
              onClick={() => setShowPass((v) => !v)}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white"
            >
              {showPass ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </div>
          <div className="flex flex-wrap gap-1.5 pt-1">
            {[
              { ok: strength.hasMinLen, label: t('validation.passwordRuleLength') },
              { ok: strength.hasUpper, label: t('validation.passwordRuleUpper') },
              { ok: strength.hasLower, label: t('validation.passwordRuleLower') },
              { ok: strength.hasDigit, label: t('validation.passwordRuleDigit') },
            ].map((rule) => (
              <span
                key={rule.label}
                className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10.5px] font-semibold ${
                  rule.ok ? 'bg-emerald-500/20 text-emerald-300' : 'bg-slate-800 text-slate-400'
                }`}
              >
                {rule.ok ? <Check className="h-3 w-3" /> : <X className="h-3 w-3" />}
                {rule.label}
              </span>
            ))}
          </div>
        </div>

        <label className="block space-y-1 font-bold text-slate-300">
          <span>{t('agentPortal.confirmPasswordLabel')}</span>
          <input
            type={showPass ? 'text' : 'password'}
            required
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
          />
        </label>

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-xl bg-emerald-600 px-4 py-2.5 font-extrabold text-white hover:bg-emerald-500 disabled:opacity-50"
        >
          {submitting ? t('agentPortal.changingPasswordBtn') : t('agentPortal.changePasswordBtn')}
        </button>
      </form>
    </div>
  );
}
