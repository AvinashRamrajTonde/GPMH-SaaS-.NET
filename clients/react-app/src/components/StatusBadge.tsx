import { useTranslation } from 'react-i18next';
import {
  type CanonicalKycStatus,
  type CanonicalRole,
  normalizeKycStatus,
  normalizeRole,
} from '../services/sprint2Api';

export function RoleBadge({
  role,
  dark = false,
}: {
  role: CanonicalRole | unknown;
  dark?: boolean;
}) {
  const { t } = useTranslation();
  const normalized = normalizeRole(role);
  const base =
    normalized === 'SuperAdmin'
      ? dark
        ? 'bg-amber-500/20 text-amber-300 border-amber-500/40'
        : 'bg-amber-100 text-amber-900 border-amber-300'
      : normalized === 'Field_Agent'
        ? dark
          ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40'
          : 'bg-teal-100 text-teal-900 border-teal-300'
        : dark
          ? 'bg-slate-800 text-slate-200 border-slate-700'
          : 'bg-brand-100 text-brand-900 border-brand-200';

  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-bold ${base}`}>
      {t(`roles.${normalized}`)}
    </span>
  );
}

export function KycStatusBadge({
  status,
  dark = false,
}: {
  status: CanonicalKycStatus | unknown;
  dark?: boolean;
}) {
  const { t } = useTranslation();
  const normalized = normalizeKycStatus(status);
  const styleMap: Record<CanonicalKycStatus, string> = {
    Approved: dark
      ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40'
      : 'bg-emerald-100 text-emerald-800 border-emerald-300',
    PendingApproval: dark
      ? 'bg-amber-500/20 text-amber-300 border-amber-500/40'
      : 'bg-amber-100 text-amber-900 border-amber-300',
    Suspended: dark
      ? 'bg-orange-500/20 text-orange-300 border-orange-500/40'
      : 'bg-orange-100 text-orange-900 border-orange-300',
    Rejected: dark
      ? 'bg-rose-500/20 text-rose-300 border-rose-500/40'
      : 'bg-rose-100 text-rose-800 border-rose-300',
  };

  return (
    <span
      className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-bold ${styleMap[normalized]}`}
    >
      {t(`kycStatus.${normalized}`)}
    </span>
  );
}

export function TenantStatusBadge({
  status,
  dark = false,
}: {
  status: string;
  dark?: boolean;
}) {
  const { t } = useTranslation();
  const knownKey = ['PendingApproval', 'Trial', 'Active', 'Suspended', 'Expired'].includes(status)
    ? status
    : 'PendingApproval';

  const classes =
    knownKey === 'Active'
      ? dark
        ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30'
        : 'bg-emerald-100 text-emerald-800 border-emerald-200'
      : knownKey === 'Trial'
        ? dark
          ? 'bg-sky-500/20 text-sky-300 border-sky-500/30'
          : 'bg-sky-100 text-sky-800 border-sky-200'
        : knownKey === 'PendingApproval'
          ? dark
            ? 'bg-amber-500/20 text-amber-300 border-amber-500/30'
            : 'bg-amber-100 text-amber-900 border-amber-200'
          : dark
            ? 'bg-rose-500/20 text-rose-300 border-rose-500/30'
            : 'bg-rose-100 text-rose-800 border-rose-200';

  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-bold ${classes}`}>
      {t(`tenantStatus.${knownKey}`)}
    </span>
  );
}

