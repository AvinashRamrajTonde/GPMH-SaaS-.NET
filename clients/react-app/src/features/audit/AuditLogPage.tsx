import { useQuery } from '@tanstack/react-query';
import { History, Search, ShieldAlert } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { sprint2Api } from '../../services/sprint2Api';
import { formatDateTime } from '../../utils/formatters';

const ENTITY_TYPES = ['Property', 'GramPanchayat', 'AssessmentYear', 'BankAccount', 'Tenant'];

export function AuditLogPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const params = useParams<{ entityName?: string; entityId?: string }>();

  const [entityName, setEntityName] = useState(params.entityName || 'Property');
  const [entityId, setEntityId] = useState(params.entityId || '');
  const [submittedKey, setSubmittedKey] = useState<{ name: string; id: string } | null>(
    params.entityName && params.entityId
      ? { name: params.entityName, id: params.entityId }
      : null,
  );

  const canQueryAudit = Boolean(
    accessToken &&
    user &&
    ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'Auditor', 'Support_Admin'].includes(user.role),
  );

  const propertiesQuery = useQuery({
    queryKey: ['properties', user?.activeTenantId],
    queryFn: sprint2Api.listProperties,
    enabled: Boolean(accessToken && user?.activeTenantId),
  });

  const auditQuery = useQuery({
    queryKey: ['audit', submittedKey?.name, submittedKey?.id],
    queryFn: () => sprint2Api.getAuditTrail(submittedKey!.name, submittedKey!.id),
    enabled: Boolean(canQueryAudit && submittedKey?.name && submittedKey?.id),
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!entityId.trim()) return;
    setSubmittedKey({ name: entityName, id: entityId.trim() });
  }

  if (!canQueryAudit) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <div className="flex items-center gap-2 font-bold text-sm">
          <History className="h-5 w-5 text-brand-700" />
          <h1>{t('audit.title')}</h1>
        </div>
        <p>{t('audit.requirePrivilege')}</p>
        <Link
          to="/login"
          className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800"
        >
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex items-center gap-2.5">
          <History className="h-5 w-5 text-brand-700" />
          <div>
            <h1 className="text-xl font-extrabold text-ink">{t('audit.title')}</h1>
            <p className="text-xs text-muted">{t('audit.subtitle')}</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="grid gap-3 sm:grid-cols-[200px_1fr_auto] items-end text-xs">
          <label className="space-y-1 font-bold text-slate-700">
            <span>{t('audit.entityNameLabel')}</span>
            <select
              value={entityName}
              onChange={(e) => setEntityName(e.target.value)}
              className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2"
            >
              {ENTITY_TYPES.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1 font-bold text-slate-700">
            <span>{t('audit.entityIdLabel')}</span>
            <input
              type="text"
              required
              value={entityId}
              onChange={(e) => setEntityId(e.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
              className="w-full rounded-xl border border-slate-300 bg-surface px-3 py-2 mono"
            />
          </label>

          <button
            type="submit"
            className="inline-flex items-center justify-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800"
          >
            <Search className="h-4 w-4" />
            {t('audit.fetchBtn')}
          </button>
        </form>

        {entityName === 'Property' && (propertiesQuery.data?.length ?? 0) > 0 && (
          <div className="flex flex-wrap items-center gap-2 text-[11px]">
            <span className="font-semibold text-muted">Quick Pick Property:</span>
            {propertiesQuery.data!.slice(0, 5).map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => {
                  setEntityName('Property');
                  setEntityId(p.id);
                  setSubmittedKey({ name: 'Property', id: p.id });
                }}
                className="rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 mono font-bold text-brand-800 hover:bg-brand-100"
              >
                {p.wardNo}/{p.propertyNo}
              </button>
            ))}
          </div>
        )}

        {auditQuery.isFetching && (
          <div className="h-24 animate-pulse rounded-xl bg-slate-200" />
        )}

        {auditQuery.error instanceof ApiProblemError && (
          <div className="flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs text-rose-800">
            <ShieldAlert className="h-4 w-4 shrink-0" />
            <span>{auditQuery.error.message}</span>
          </div>
        )}

        {auditQuery.data && auditQuery.data.length === 0 && (
          <p className="rounded-xl border border-dashed border-slate-300 bg-surface p-6 text-center text-xs text-muted">
            {t('audit.emptyState')}
          </p>
        )}

        {auditQuery.data && auditQuery.data.length > 0 && (
          <div className="space-y-3">
            {auditQuery.data.map((entry) => (
              <div key={entry.id} className="rounded-xl border border-line bg-surface p-4 text-xs space-y-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="rounded bg-brand-700 px-2 py-0.5 mono text-[10.5px] font-bold text-white">
                      {entry.action}
                    </span>
                    <span className="font-bold text-ink">
                      {t('audit.actorLabel')}: {entry.actorRole || 'System'} ({entry.actorId?.slice(0, 8) || '—'})
                    </span>
                  </div>
                  <span className="mono text-[11px] text-muted">
                    {formatDateTime(entry.timestampUtc, i18n.language)}
                  </span>
                </div>

                {entry.changedColumnsJson && (
                  <p className="mono text-[11px] text-slate-700">
                    <strong>{t('audit.changedCols')}:</strong> {entry.changedColumnsJson}
                  </p>
                )}

                <div className="grid gap-2 sm:grid-cols-2">
                  {entry.oldValuesJson && (
                    <div className="rounded-lg border border-rose-200 bg-rose-50/50 p-2">
                      <p className="font-bold text-rose-900 mb-1">{t('audit.oldValues')}</p>
                      <pre className="mono text-[10.5px] whitespace-pre-wrap break-all text-slate-800">
                        {entry.oldValuesJson}
                      </pre>
                    </div>
                  )}
                  {entry.newValuesJson && (
                    <div className="rounded-lg border border-emerald-200 bg-emerald-50/50 p-2">
                      <p className="font-bold text-emerald-900 mb-1">{t('audit.newValues')}</p>
                      <pre className="mono text-[10.5px] whitespace-pre-wrap break-all text-slate-800">
                        {entry.newValuesJson}
                      </pre>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

