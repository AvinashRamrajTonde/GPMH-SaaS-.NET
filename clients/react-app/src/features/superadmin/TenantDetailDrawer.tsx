import { useQuery } from '@tanstack/react-query';
import {
  Building2,
  CreditCard,
  Eye,
  FileDown,
  History,
  KeyRound,
  RefreshCw,
  ShieldCheck,
  Users,
  X,
} from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RoleBadge, TenantStatusBadge } from '../../components/StatusBadge';
import type { SuperAdminTenantSummary } from '../../services/sprint2Api';
import { superAdminApi, type TenantStatusAction } from '../../services/superAdminApi';
import { formatDateOnly, formatDateTime, formatInr } from '../../utils/formatters';

export function TenantDetailDrawer({
  tenantId,
  canMutate,
  onClose,
  onImpersonate,
  onStatusAction,
  onResetPassword,
}: {
  tenantId: string;
  canMutate: boolean;
  onClose: () => void;
  onImpersonate: (tenant: SuperAdminTenantSummary) => void;
  onStatusAction: (tenant: SuperAdminTenantSummary, action: TenantStatusAction) => void;
  onResetPassword: (tenant: SuperAdminTenantSummary) => void;
}) {
  const { t, i18n } = useTranslation();
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const detailQuery = useQuery({
    queryKey: ['superadmin', 'tenant-detail', tenantId],
    queryFn: () => superAdminApi.getTenantDetail(tenantId),
  });

  const detail = detailQuery.data;
  const summary = detail?.summary;

  async function handleDownloadInvoice(id: string) {
    setDownloadingId(id);
    try {
      await superAdminApi.downloadInvoicePdf(id);
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-slate-950/75 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
    >
      <div className="flex h-full w-full max-w-2xl flex-col border-l border-slate-700 bg-slate-900 text-xs text-slate-200 shadow-2xl">
        {/* Header */}
        <div className="flex items-start justify-between gap-3 border-b border-slate-800 bg-slate-950/90 px-5 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-500/20 text-amber-300 border border-amber-500/30">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-sm font-extrabold text-white">
                {summary ? `${summary.panchayatNameMr} (${summary.panchayatNameEn})` : t('superadminBilling.drawerTitle')}
              </h2>
              {summary && (
                <p className="mono text-[11px] text-slate-400">
                  LGD: {summary.lgdCode} · {summary.districtName} / {summary.tehsilName}
                </p>
              )}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-5 space-y-5">
          {detailQuery.isLoading && (
            <div className="space-y-3">
              {[1, 2, 3].map((n) => (
                <div key={n} className="h-20 animate-pulse rounded-xl bg-slate-800" />
              ))}
            </div>
          )}

          {detail && summary && (
            <>
              {/* Quick Status & Actions */}
              <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-800 bg-slate-950/70 p-3.5">
                <div className="flex flex-wrap items-center gap-2">
                  <TenantStatusBadge status={summary.effectiveStatus || summary.status} dark />
                  {summary.isTrial && (
                    <span className="rounded bg-sky-500/20 border border-sky-500/30 px-2 py-0.5 text-[10px] font-bold text-sky-300">
                      {t('superadminBilling.trialBadge')}
                    </span>
                  )}
                  <span className="mono text-[11px] text-slate-400">
                    {formatDateOnly(summary.subscriptionExpiresAtUtc, i18n.language)}
                  </span>
                </div>

                <div className="flex flex-wrap gap-1.5">
                  <button
                    type="button"
                    onClick={() => onImpersonate(summary)}
                    className="inline-flex items-center gap-1 rounded-lg bg-amber-500 px-2.5 py-1.5 text-[11px] font-extrabold text-slate-950 hover:bg-amber-400"
                  >
                    <Eye className="h-3.5 w-3.5" />
                    {t('superadminBilling.btnImpersonate')}
                  </button>
                  {canMutate && (
                    <>
                      <button
                        type="button"
                        onClick={() => onStatusAction(summary, 'DirectRenew')}
                        className="inline-flex items-center gap-1 rounded-lg border border-emerald-500/40 bg-emerald-500/15 px-2.5 py-1.5 text-[11px] font-bold text-emerald-300 hover:bg-emerald-500/25"
                      >
                        <RefreshCw className="h-3 w-3" />
                        {t('superadminBilling.btnDirectRenew')}
                      </button>
                      <button
                        type="button"
                        onClick={() => onResetPassword(summary)}
                        className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1.5 text-[11px] font-bold text-slate-200 hover:bg-slate-700"
                      >
                        <KeyRound className="h-3 w-3" />
                        {t('superadminBilling.btnResetPass')}
                      </button>
                    </>
                  )}
                </div>
              </div>

              {/* Metrics Grid */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
                <div className="rounded-xl border border-slate-800 bg-slate-950 p-3">
                  <p className="text-[10px] text-slate-400">{t('superadminBilling.propertiesShort')}</p>
                  <p className="mono text-lg font-extrabold text-sky-400">{summary.propertiesCount}</p>
                </div>
                <div className="rounded-xl border border-slate-800 bg-slate-950 p-3">
                  <p className="text-[10px] text-slate-400">{t('superadminBilling.citizensShort')}</p>
                  <p className="mono text-lg font-extrabold text-white">{summary.citizensCount}</p>
                </div>
                <div className="rounded-xl border border-slate-800 bg-slate-950 p-3">
                  <p className="text-[10px] text-slate-400">{t('superadminBilling.collectedShort')}</p>
                  <p className="mono text-sm font-extrabold text-emerald-400">
                    {formatInr(summary.taxCollected)}
                  </p>
                  <p className="mono text-[10px] text-slate-500">/ {formatInr(summary.taxDemanded)}</p>
                </div>
                <div className="rounded-xl border border-slate-800 bg-slate-950 p-3">
                  <p className="text-[10px] text-slate-400">{t('superadmin.kpiAvgSetup')}</p>
                  <p className="mono text-lg font-extrabold text-amber-400">{summary.setupProgressPct}%</p>
                </div>
              </div>

              {/* Governance & Contact Info */}
              <div className="rounded-xl border border-slate-800 bg-slate-950 p-3.5 grid gap-2 sm:grid-cols-2 text-[11px]">
                <p>
                  <span className="text-slate-400">{t('superadminBilling.drawerSarpanch')}: </span>
                  <strong className="text-white">{detail.sarpanchName || '—'}</strong>
                  {detail.sarpanchMobile ? ` (${detail.sarpanchMobile})` : ''}
                </p>
                <p>
                  <span className="text-slate-400">{t('superadminBilling.drawerGramsevak')}: </span>
                  <strong className="text-white">{detail.gramsevakName || '—'}</strong>
                  {detail.gramsevakMobile ? ` (${detail.gramsevakMobile})` : ''}
                </p>
                <p>
                  <span className="text-slate-400">{t('superadminBilling.drawerUpi')}: </span>
                  <span className="mono text-amber-300">{detail.primaryUpiId || '—'}</span>
                </p>
                <p>
                  <span className="text-slate-400">{t('superadminBilling.drawerAy')}: </span>
                  <span className="mono text-white">{detail.activeAssessmentYear || '—'}</span>
                </p>
                <p>
                  <span className="text-slate-400">{t('superadminBilling.drawerBanks')}: </span>
                  <span className="mono text-white">{detail.bankAccountsCount}</span>
                  {' · '}
                  <span className="text-slate-400">{t('superadminBilling.drawerAssets')}: </span>
                  <span className="mono text-white">{detail.protectedAssetsCount}</span>
                </p>
                <p>
                  <span className="text-slate-400">{t('superadminBilling.drawerReferral')}: </span>
                  <span className="mono text-emerald-300">{summary.referralCodeUsed || '—'}</span>
                </p>
              </div>

              {/* Staff Accounts */}
              <div className="space-y-2">
                <h3 className="font-bold text-white flex items-center gap-1.5">
                  <Users className="h-4 w-4 text-sky-400" />
                  {t('superadminBilling.drawerUsers')} ({detail.users.length})
                </h3>
                {detail.users.length === 0 ? (
                  <p className="text-slate-500">{t('superadminBilling.drawerNoUsers')}</p>
                ) : (
                  <div className="divide-y divide-slate-800 rounded-xl border border-slate-800 bg-slate-950">
                    {detail.users.map((u) => (
                      <div key={u.userId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                        <div>
                          <p className="font-bold text-white">
                            {u.fullNameEn} <span className="mono text-amber-300">({u.username})</span>
                          </p>
                          <p className="mono text-[10px] text-slate-400">
                            {u.phone || '—'} · {u.email || '—'}
                          </p>
                        </div>
                        <RoleBadge role={u.role} dark />
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Subscription Billing History */}
              <div className="space-y-2">
                <h3 className="font-bold text-white flex items-center gap-1.5">
                  <CreditCard className="h-4 w-4 text-emerald-400" />
                  {t('superadminBilling.drawerPayments')} ({detail.subscriptionPayments.length})
                </h3>
                {detail.subscriptionPayments.length === 0 ? (
                  <p className="text-slate-500">{t('superadminBilling.drawerNoPayments')}</p>
                ) : (
                  <div className="divide-y divide-slate-800 rounded-xl border border-slate-800 bg-slate-950">
                    {detail.subscriptionPayments.map((p) => (
                      <div key={p.paymentId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2.5">
                        <div>
                          <p className="font-bold text-white">
                            {p.invoiceNo || p.paymentReference || p.planName} ·{' '}
                            <span className="mono text-emerald-400">{formatInr(p.amount)}</span>
                          </p>
                          <p className="mono text-[10px] text-slate-400">
                            {p.paymentMode} · {p.durationDays}d · {formatDateOnly(p.paymentDateUtc, i18n.language)}
                          </p>
                        </div>
                        <button
                          type="button"
                          disabled={downloadingId === p.paymentId}
                          onClick={() => void handleDownloadInvoice(p.paymentId)}
                          className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-[11px] font-bold text-slate-200 hover:bg-slate-700"
                        >
                          <FileDown className="h-3.5 w-3.5" />
                          {t('superadminBilling.downloadGstPdfBtn')}
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Recent Audit Logs */}
              <div className="space-y-2">
                <h3 className="font-bold text-white flex items-center gap-1.5">
                  <History className="h-4 w-4 text-amber-400" />
                  {t('superadminBilling.drawerAudits')} ({detail.recentAuditLogs.length})
                </h3>
                {detail.recentAuditLogs.length === 0 ? (
                  <p className="text-slate-500">{t('superadminBilling.drawerNoAudits')}</p>
                ) : (
                  <div className="divide-y divide-slate-800 rounded-xl border border-slate-800 bg-slate-950 font-mono text-[11px]">
                    {detail.recentAuditLogs.map((log) => (
                      <div key={log.id} className="px-3 py-2 space-y-0.5">
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-bold text-amber-300">
                            {log.action} · {log.entityName}
                          </span>
                          <span className="text-[10px] text-slate-500">
                            {formatDateTime(log.timestampUtc, i18n.language)}
                          </span>
                        </div>
                        <p className="text-[10px] text-slate-400">
                          Actor: {log.actorId || '—'} ({log.actorRole || '—'}) · IP: {log.ipAddress || '—'}
                        </p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between border-t border-slate-800 bg-slate-950 px-5 py-3">
          <span className="text-[11px] text-slate-500 flex items-center gap-1.5">
            <ShieldCheck className="h-3.5 w-3.5 text-emerald-400" />
            {t('superadminBilling.drawerTitle')}
          </span>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-1.5 font-bold text-slate-200 hover:bg-slate-700"
          >
            OK
          </button>
        </div>
      </div>
    </div>
  );
}

