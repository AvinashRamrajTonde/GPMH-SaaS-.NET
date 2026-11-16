import { useQuery } from '@tanstack/react-query';
import { Building2, CheckCircle2, MapPin, ShieldAlert, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { ApiProblemError } from '../../services/apiClient';
import { printApi } from '../../services/printApi';
import { formatDateTime, formatInr } from '../../utils/formatters';

export function ExtractVerifyPage() {
  const { token } = useParams<{ token: string }>();
  const { t, i18n } = useTranslation();

  const verifyQuery = useQuery({
    queryKey: ['public-verify-extract', token],
    queryFn: () => printApi.verifyPublicExtract(token!),
    enabled: Boolean(token),
    retry: false,
  });

  if (verifyQuery.isLoading) {
    return (
      <div className="mx-auto max-w-2xl p-6">
        <div className="h-64 animate-pulse rounded-2xl bg-slate-200" />
      </div>
    );
  }

  if (verifyQuery.isError || !verifyQuery.data) {
    const detail =
      verifyQuery.error instanceof ApiProblemError
        ? verifyQuery.error.problem.detail || verifyQuery.error.message
        : t('print.verifyInvalidDesc');

    return (
      <div className="mx-auto max-w-xl p-6">
        <div className="rounded-2xl border-2 border-rose-300 bg-rose-50 p-6 text-xs text-rose-950 space-y-3 shadow-xs">
          <div className="flex items-center gap-2 text-rose-700">
            <ShieldAlert className="h-6 w-6" />
            <h1 className="text-base font-extrabold">{t('print.verifyInvalidTitle')}</h1>
          </div>
          <p>{detail}</p>
          <p className="text-[11px] text-rose-800">{t('print.verifySecurityNote')}</p>
          <Link
            to="/login"
            className="inline-block rounded-lg bg-slate-900 px-3.5 py-2 font-bold text-white hover:bg-slate-800"
          >
            {t('nav.login')}
          </Link>
        </div>
      </div>
    );
  }

  const v = verifyQuery.data;

  return (
    <div className="mx-auto max-w-2xl p-4 sm:p-6 text-xs space-y-4">
      <div className="rounded-2xl border-2 border-emerald-500 bg-white p-6 shadow-md space-y-5">
        <div className="flex flex-wrap items-start justify-between gap-4 border-b border-emerald-100 pb-4">
          <div className="space-y-1">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-[11px] font-extrabold text-emerald-900">
              <ShieldCheck className="h-4 w-4 text-emerald-700" />
              {t('print.verifyAuthenticBadge')}
            </div>
            <h1 className="text-lg font-extrabold text-ink pt-1">
              {v.panchayatNameMr} ({v.panchayatNameEn})
            </h1>
            <p className="inline-flex items-center gap-1 text-muted">
              <MapPin className="h-3.5 w-3.5 text-brand-700" />
              {v.tehsilName}, {v.districtName} • LGD {v.lgdCode}
            </p>
          </div>

          <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-3.5 py-2 text-right">
            <span className="block text-[10px] font-semibold text-emerald-800">
              {t('print.assessmentYear')}
            </span>
            <span className="text-sm font-extrabold text-emerald-950">{v.assessmentYearLabel}</span>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-xl border border-line bg-surface p-3.5 space-y-1">
            <span className="text-[10px] font-bold uppercase text-muted">
              {t('properties.colWardProp')}
            </span>
            <p className="text-sm font-extrabold text-ink flex items-center gap-1.5">
              <Building2 className="h-4 w-4 text-brand-700" />
              {t('properties.wardNo')} {v.wardNo} / {v.propertyNo}
            </p>
            <p className="text-[11px] text-emerald-800 font-semibold">{v.status}</p>
          </div>

          <div className="rounded-xl border border-line bg-surface p-3.5 space-y-1">
            <span className="text-[10px] font-bold uppercase text-muted">
              {t('properties.colOwner')}
            </span>
            <p className="text-sm font-extrabold text-ink">{v.primaryOwnerNameMr}</p>
            <p className="text-[11px] text-muted">{v.primaryOwnerNameEn}</p>
          </div>

          <div className="rounded-xl border border-line bg-surface p-3.5 space-y-1">
            <span className="text-[10px] font-bold uppercase text-muted">
              {t('properties.totalArea')}
            </span>
            <p className="font-bold text-ink">
              {v.totalAreaSqFt.toFixed(2)} {t('dashboard.sqFtUnit')} ({v.totalAreaSqM.toFixed(2)}{' '}
              {t('dashboard.sqMUnit')})
            </p>
            <p className="text-[11px] text-muted">
              {t('properties.builtUpArea')}: {v.builtUpAreaSqFt.toFixed(2)} ·{' '}
              {t('properties.openPlotArea')}: {v.openPlotAreaSqFt.toFixed(2)}
            </p>
          </div>

          <div className="rounded-xl border border-line bg-surface p-3.5 space-y-1">
            <span className="text-[10px] font-bold uppercase text-muted">
              {t('properties.colAnnualTax')}
            </span>
            <p className="text-base font-extrabold text-brand-800">
              {v.isTaxExempt ? t('properties.exemptBadge') : formatInr(v.totalAnnualTax)}
            </p>
            <p className="inline-flex items-center gap-1 text-[10px] font-semibold text-emerald-700">
              <CheckCircle2 className="h-3 w-3" />
              {t('print.dpdpVerifiedFooter')}
            </p>
          </div>
        </div>

        <div className="border-t border-line pt-3 flex flex-wrap items-center justify-between gap-2 text-[11px] text-muted">
          <span>
            {t('print.issuedTimestamp')}: {formatDateTime(v.issuedAtUtc, i18n.language)}
          </span>
          <span className="font-mono text-[10px]">HMAC-SHA256</span>
        </div>
      </div>
    </div>
  );
}

