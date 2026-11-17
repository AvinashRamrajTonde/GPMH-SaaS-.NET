import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, CreditCard, Eye, FileDown, Search, ShieldAlert } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiProblemError } from '../../services/apiClient';
import { superAdminApi, type TenantPaymentSubmission } from '../../services/superAdminApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import {
  ApprovePaymentModal,
  RejectPaymentModal,
  SlipPreviewModal,
} from './SuperAdminActionModals';

export function PaymentApprovalsTab({
  canMutate,
}: {
  canMutate: boolean;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();

  const [statusFilter, setStatusFilter] = useState<'ALL' | 'Pending' | 'Approved' | 'Rejected'>(
    'Pending',
  );
  const [search, setSearch] = useState('');
  const [slipModal, setSlipModal] = useState<{ assetKey: string; utrReference: string } | null>(null);
  const [approveTarget, setApproveTarget] = useState<TenantPaymentSubmission | null>(null);
  const [rejectTarget, setRejectTarget] = useState<TenantPaymentSubmission | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const paymentsQuery = useQuery({
    queryKey: ['superadmin', 'payments', statusFilter, search],
    queryFn: () =>
      superAdminApi.listPayments({
        status: statusFilter === 'ALL' ? undefined : statusFilter,
        search: search.trim() || undefined,
      }),
  });

  const items = paymentsQuery.data ?? [];
  const pendingCount = items.filter((p) => p.status === 'Pending').length;

  async function refreshAll() {
    await queryClient.invalidateQueries({ queryKey: ['superadmin'] });
  }

  async function handleDownloadInvoice(id: string) {
    setDownloadingId(id);
    try {
      await superAdminApi.downloadInvoicePdf(id);
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-sm space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <CreditCard className="h-4 w-4 text-amber-400" />
            <h2 className="text-sm font-extrabold text-white">
              {t('superadminBilling.approvalsTitle')}
            </h2>
            {pendingCount > 0 && (
              <span className="rounded-full bg-amber-500/20 border border-amber-500/40 px-2.5 py-0.5 mono text-[11px] font-bold text-amber-300">
                {t('superadminBilling.pendingBadge', { count: pendingCount })}
              </span>
            )}
          </div>
          <p className="mt-0.5 text-xs text-slate-400">
            {t('superadminBilling.approvalsSubtitle')}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2 text-xs">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-500" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('superadminBilling.utrLabel')}
              className="rounded-lg border border-slate-700 bg-slate-950 pl-8 pr-3 py-1.5 text-slate-100 placeholder:text-slate-500 focus:border-amber-400"
            />
          </div>
          <select
            value={statusFilter}
            onChange={(e) =>
              setStatusFilter(e.target.value as 'ALL' | 'Pending' | 'Approved' | 'Rejected')
            }
            className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-slate-200"
          >
            <option value="ALL">{t('superadmin.allStatuses')}</option>
            <option value="Pending">{t('tenantStatus.PendingApproval')}</option>
            <option value="Approved">{t('kycStatus.Approved')}</option>
            <option value="Rejected">{t('kycStatus.Rejected')}</option>
          </select>
        </div>
      </div>

      {paymentsQuery.isLoading && (
        <div className="grid gap-3 md:grid-cols-2">
          {[1, 2].map((n) => (
            <div key={n} className="h-44 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      )}

      {paymentsQuery.error instanceof ApiProblemError && (
        <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-xs text-rose-200">
          {paymentsQuery.error.message}
        </div>
      )}

      {!paymentsQuery.isLoading && items.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/60 p-8 text-center text-xs text-slate-400">
          {t('superadminBilling.noPaymentsMatch')}
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {items.map((item) => {
            const statusClass =
              item.status === 'Approved'
                ? 'border-emerald-500/40 bg-emerald-500/15 text-emerald-300'
                : item.status === 'Rejected'
                  ? 'border-rose-500/40 bg-rose-500/15 text-rose-300'
                  : 'border-amber-500/40 bg-amber-500/15 text-amber-300';

            return (
              <div
                key={item.submissionId}
                className="rounded-xl border border-slate-800 bg-slate-950/80 p-4 space-y-3 text-xs"
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-extrabold text-white text-sm">
                      {item.panchayatNameMr} ({item.panchayatNameEn})
                    </p>
                    <p className="mono text-[11px] text-slate-400">
                      LGD {item.lgdCode} · {item.districtName} · {item.planName}
                    </p>
                  </div>
                  <div className="text-right space-y-1">
                    <span className="inline-block rounded-md border border-emerald-500/30 bg-emerald-500/15 px-2.5 py-0.5 mono font-extrabold text-emerald-300">
                      {formatInr(item.amount)}
                    </span>
                    <div>
                      <span
                        className={`inline-block rounded-full border px-2 py-0.5 text-[10px] font-bold ${statusClass}`}
                      >
                        {item.status}
                      </span>
                    </div>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-2 rounded-lg border border-slate-800 bg-slate-900/90 p-2.5 mono text-[11px]">
                  <div>
                    <span className="block text-[10px] text-slate-500">
                      {t('superadminBilling.utrLabel')}
                    </span>
                    <strong className="text-amber-300">{item.utrReference}</strong>
                  </div>
                  <div>
                    <span className="block text-[10px] text-slate-500">
                      {t('superadminBilling.dateLabel')}
                    </span>
                    <strong className="text-slate-200">
                      {formatDateOnly(item.paymentDate, i18n.language)}
                    </strong>
                  </div>
                  <div>
                    <span className="block text-[10px] text-slate-500">
                      {t('superadminBilling.modeLabel')}
                    </span>
                    <strong className="text-slate-200">{item.paymentMode}</strong>
                  </div>
                  <div>
                    <span className="block text-[10px] text-slate-500">
                      {t('superadminBilling.submittedByLabel')}
                    </span>
                    <strong className="text-slate-200">{item.submittedByUsername}</strong>
                  </div>
                </div>

                {item.invoiceNo && (
                  <p className="mono text-[11px] text-emerald-300">
                    {t('superadminBilling.invoiceNoLabel')}: <strong>{item.invoiceNo}</strong>
                  </p>
                )}

                {item.rejectionReason && (
                  <p className="rounded-lg border border-rose-500/30 bg-rose-950/40 px-2.5 py-1.5 text-[11px] text-rose-200">
                    {item.rejectionReason}
                  </p>
                )}

                <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-800 pt-2.5">
                  <div className="flex flex-wrap items-center gap-2">
                    {item.slipAssetKey && (
                      <button
                        type="button"
                        onClick={() =>
                          setSlipModal({
                            assetKey: item.slipAssetKey!,
                            utrReference: item.utrReference,
                          })
                        }
                        className="inline-flex items-center gap-1 text-xs font-bold text-sky-400 hover:underline"
                      >
                        <Eye className="h-3.5 w-3.5" />
                        {t('superadminBilling.viewSlipBtn')}
                      </button>
                    )}

                    {item.status === 'Approved' && (
                      <button
                        type="button"
                        disabled={downloadingId === item.submissionId}
                        onClick={() => void handleDownloadInvoice(item.submissionId)}
                        className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-[11px] font-bold text-slate-200 hover:bg-slate-700"
                      >
                        <FileDown className="h-3.5 w-3.5" />
                        {t('superadminBilling.downloadGstPdfBtn')}
                      </button>
                    )}
                  </div>

                  {item.status === 'Pending' && canMutate && (
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => setRejectTarget(item)}
                        className="inline-flex items-center gap-1 rounded-lg border border-rose-800 bg-rose-950/60 px-3 py-1.5 text-[11px] font-bold text-rose-300 hover:bg-rose-900/60"
                      >
                        <ShieldAlert className="h-3.5 w-3.5" />
                        {t('superadminBilling.rejectBtn')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setApproveTarget(item)}
                        className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3.5 py-1.5 text-[11px] font-bold text-white hover:bg-emerald-500"
                      >
                        <CheckCircle2 className="h-3.5 w-3.5" />
                        {t('superadminBilling.approve365Btn')}
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {slipModal && (
        <SlipPreviewModal
          assetKey={slipModal.assetKey}
          utrReference={slipModal.utrReference}
          onClose={() => setSlipModal(null)}
        />
      )}

      {approveTarget && (
        <ApprovePaymentModal
          key={approveTarget.submissionId}
          submission={approveTarget}
          onClose={() => setApproveTarget(null)}
          onCompleted={() => {
            setApproveTarget(null);
            void refreshAll();
          }}
        />
      )}

      {rejectTarget && (
        <RejectPaymentModal
          key={rejectTarget.submissionId}
          submission={rejectTarget}
          onClose={() => setRejectTarget(null)}
          onCompleted={() => {
            setRejectTarget(null);
            void refreshAll();
          }}
        />
      )}
    </section>
  );
}

