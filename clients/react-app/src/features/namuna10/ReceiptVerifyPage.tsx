import { useQuery } from '@tanstack/react-query';
import {
  AlertOctagon,
  ArrowLeft,
  BadgeCheck,
  Building2,
  Calendar,
  FileCheck2,
  MapPin,
  ReceiptText,
  ShieldAlert,
  ShieldCheck,
  User,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { ApiProblemError } from '../../services/apiClient';
import { printApi, type VerifiedReceiptPublicDto } from '../../services/printApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';

export function ReceiptVerifyPage() {
  const { t, i18n } = useTranslation();
  const { token } = useParams<{ token: string }>();

  const query = useQuery<VerifiedReceiptPublicDto, ApiProblemError>({
    queryKey: ['public-verify-receipt', token],
    queryFn: () => printApi.verifyPublicReceipt(token ?? ''),
    enabled: Boolean(token),
    retry: false,
  });

  return (
    <div className="mx-auto max-w-2xl space-y-5 py-6">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-50 text-teal-800 border border-teal-200">
            <ReceiptText className="h-5 w-5" />
          </div>
          <div>
            <p className="text-[11px] font-bold tracking-wide text-teal-800 uppercase">
              {t('receiptVerify.eyebrow')}
            </p>
            <h1 className="text-lg font-extrabold text-ink">{t('receiptVerify.title')}</h1>
          </div>
        </div>
        <Link
          to="/login"
          className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-card px-3 py-1.5 text-xs font-semibold text-ink hover:bg-slate-50"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          {t('receiptVerify.backToPortal')}
        </Link>
      </div>

      {query.isLoading && (
        <div className="rounded-2xl border border-line bg-card p-8 text-center">
          <div className="mx-auto h-10 w-10 animate-spin rounded-full border-2 border-teal-700 border-t-transparent" />
          <p className="mt-3 text-sm font-medium text-muted">{t('receiptVerify.verifying')}</p>
        </div>
      )}

      {query.isError && (
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-6">
          <div className="flex items-start gap-3">
            <ShieldAlert className="h-6 w-6 shrink-0 text-rose-600" />
            <div>
              <h2 className="text-base font-bold text-rose-900">
                {t('receiptVerify.invalidTitle')}
              </h2>
              <p className="mt-1 text-xs leading-relaxed text-rose-800">
                {query.error?.problem?.detail || t('receiptVerify.invalidDesc')}
              </p>
            </div>
          </div>
        </div>
      )}

      {query.data && (
        <div
          className={`overflow-hidden rounded-2xl border bg-card shadow-sm ${
            query.data.isCancelled ? 'border-rose-300' : 'border-emerald-200'
          }`}
        >
          {/* Verification Status Banner */}
          {query.data.isCancelled ? (
            <div className="border-b border-rose-300 bg-rose-700 px-6 py-4 text-white">
              <div className="flex items-start gap-3">
                <AlertOctagon className="mt-0.5 h-6 w-6 shrink-0" />
                <div>
                  <h2 className="text-base font-extrabold tracking-tight">
                    {t('receiptVerify.cancelledBannerTitle')}
                  </h2>
                  <p className="mt-0.5 text-xs text-rose-100">
                    {t('receiptVerify.cancelledBannerDesc')}
                  </p>
                  {query.data.cancelReason && (
                    <p className="mt-2 rounded-lg bg-rose-900/50 px-3 py-1.5 text-xs font-semibold text-white">
                      {t('receiptVerify.cancelReasonLabel')}: {query.data.cancelReason}
                      {query.data.cancelledAtUtc
                        ? ` (${new Date(query.data.cancelledAtUtc).toLocaleString(i18n.language)})`
                        : ''}
                    </p>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-emerald-200 bg-emerald-50 px-6 py-4">
              <div className="flex items-center gap-3">
                <BadgeCheck className="h-7 w-7 text-emerald-700" />
                <div>
                  <h2 className="text-base font-extrabold text-emerald-950">
                    {t('receiptVerify.verifiedBadge')}
                  </h2>
                  <p className="text-xs text-emerald-800">
                    {t('receiptVerify.verifiedSubtitle')}
                  </p>
                </div>
              </div>
              <span className="inline-flex items-center gap-1 rounded-full bg-white px-3 py-1 font-mono text-xs font-extrabold text-emerald-900 border border-emerald-200">
                <ShieldCheck className="h-3.5 w-3.5 text-emerald-700" />
                LGD {query.data.lgdCode}
              </span>
            </div>
          )}

          {/* Receipt Summary Grid */}
          <div className="space-y-4 p-6">
            <div className="rounded-xl border border-line bg-slate-50/80 p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-wide text-muted">
                    {t('receiptVerify.gramPanchayatLabel')}
                  </p>
                  <p className="text-base font-extrabold text-ink">
                    {query.data.panchayatNameMr} ({query.data.panchayatNameEn})
                  </p>
                  <p className="mt-0.5 flex items-center gap-1 text-xs text-muted">
                    <MapPin className="h-3.5 w-3.5" />
                    {t('receiptVerify.tehsilDistrict', {
                      tehsil: query.data.tehsilName,
                      district: query.data.districtName,
                    })}
                  </p>
                </div>
                <div className="text-right">
                  <p className="text-[11px] font-bold uppercase tracking-wide text-muted">
                    {t('receiptVerify.receiptNoLabel')}
                  </p>
                  <p
                    className={`font-mono text-base font-extrabold ${
                      query.data.isCancelled ? 'line-through text-rose-700' : 'text-teal-800'
                    }`}
                  >
                    {query.data.receiptNo}
                  </p>
                  <p className="font-mono text-[11px] text-muted">
                    {t('receiptVerify.bookNoLabel')}: {query.data.bookNo}
                  </p>
                </div>
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="rounded-xl border border-line p-3.5">
                <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase text-muted">
                  <User className="h-3.5 w-3.5 text-teal-700" />
                  {t('receiptVerify.primaryOwnerLabel')}
                </p>
                <p className="mt-1 text-sm font-extrabold text-ink">
                  {query.data.primaryOwnerNameMr}
                </p>
                <p className="text-xs text-muted">{query.data.primaryOwnerNameEn}</p>
              </div>

              <div className="rounded-xl border border-line p-3.5">
                <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase text-muted">
                  <Building2 className="h-3.5 w-3.5 text-teal-700" />
                  {t('receiptVerify.propertyWardLabel')}
                </p>
                <p className="mt-1 font-mono text-sm font-extrabold text-ink">
                  {t('receiptVerify.wardPrefix')} {query.data.wardNo} ·{' '}
                  {t('receiptVerify.propPrefix')} {query.data.propertyNo}
                </p>
                <p className="text-xs text-muted">
                  {t('receiptVerify.assessmentYearLabel')}: {query.data.assessmentYearLabel}
                </p>
              </div>

              <div className="rounded-xl border border-line p-3.5">
                <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase text-muted">
                  <Calendar className="h-3.5 w-3.5 text-teal-700" />
                  {t('receiptVerify.receiptDateLabel')}
                </p>
                <p className="mt-1 text-sm font-bold text-ink">
                  {formatDateOnly(query.data.receiptDate, i18n.language)}
                </p>
                <p className="text-xs text-muted">
                  {t('receiptVerify.paymentModeLabel')}: {query.data.paymentMode}
                </p>
              </div>

              <div className="rounded-xl border border-line bg-teal-50/40 p-3.5">
                <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase text-muted">
                  <FileCheck2 className="h-3.5 w-3.5 text-teal-700" />
                  {t('receiptVerify.amountPaidLabel')}
                </p>
                <p
                  className={`mono mt-1 text-lg font-extrabold ${
                    query.data.isCancelled ? 'line-through text-rose-700' : 'text-teal-900'
                  }`}
                >
                  {formatInr(query.data.totalPaid)}
                </p>
                {query.data.discountGiven > 0 && (
                  <p className="text-xs font-semibold text-emerald-700">
                    {t('receiptVerify.rebateLabel')}: {formatInr(query.data.discountGiven)}
                  </p>
                )}
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-[11px] text-slate-600">
              {t('receiptVerify.dpdpPrivacyNote')}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
