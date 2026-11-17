import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Download,
  History,
  RotateCcw,
  Save,
  Settings,
  Trash2,
} from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { QrMatrixSvg } from '../../components/QrMatrixSvg';
import type { SuperAdminTenantSummary } from '../../services/sprint2Api';
import {
  superAdminApi,
  type PlatformPaymentAccount,
  type UpdatePlatformSettingsPayload,
} from '../../services/superAdminApi';
import { formatDateOnly, formatDateTime, formatInr } from '../../utils/formatters';
import { extractProblemValidation } from '../../utils/validation';
import { PermanentDeleteModal } from './SuperAdminActionModals';

export function TrashTab({
  canMutate,
}: {
  canMutate: boolean;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();

  const [permDeleteTarget, setPermDeleteTarget] = useState<SuperAdminTenantSummary | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const trashQuery = useQuery({
    queryKey: ['superadmin', 'tenants', 'trash'],
    queryFn: async () => {
      const all = await superAdminApi.listTenants({ includeDeleted: true });
      return all.filter((x) => x.isDeleted);
    },
  });

  async function refreshAll() {
    await queryClient.invalidateQueries({ queryKey: ['superadmin'] });
  }

  async function handleRestore(id: string) {
    setError(null);
    setBusyId(id);
    try {
      await superAdminApi.restoreTenant(id);
      await refreshAll();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to restore GP.').summary);
    } finally {
      setBusyId(null);
    }
  }

  const deletedTenants = trashQuery.data ?? [];

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-sm space-y-4">
      <div>
        <h2 className="text-sm font-extrabold text-white flex items-center gap-2">
          <Trash2 className="h-4 w-4 text-rose-400" />
          {t('superadminBilling.trashTitle')}
        </h2>
        <p className="mt-0.5 text-xs text-slate-400">{t('superadminBilling.trashSubtitle')}</p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 p-3 text-xs text-rose-200">
          {error}
        </div>
      )}

      {!trashQuery.isLoading && deletedTenants.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/60 p-8 text-center text-xs text-slate-400">
          {t('superadminBilling.trashEmpty')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-800">
          <table className="min-w-full border-collapse text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-950 text-[11px] font-bold uppercase text-slate-400">
              <tr>
                <th className="px-4 py-3">LGD</th>
                <th className="px-4 py-3">{t('onboarding.panchayat')}</th>
                <th className="px-4 py-3">{t('onboarding.district')}</th>
                <th className="px-4 py-3">{t('superadminBilling.deletedAtCol')}</th>
                <th className="px-4 py-3 text-right">{t('superadminBilling.actionsCol')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {deletedTenants.map((item) => (
                <tr key={item.tenantId} className="hover:bg-slate-800/40">
                  <td className="px-4 py-3 mono font-bold text-amber-300">{item.lgdCode}</td>
                  <td className="px-4 py-3">
                    <p className="font-bold text-white">{item.panchayatNameMr}</p>
                    <p className="text-[11px] text-slate-400">{item.panchayatNameEn}</p>
                  </td>
                  <td className="px-4 py-3 text-slate-300">
                    {item.districtName} · {item.tehsilName}
                  </td>
                  <td className="px-4 py-3 mono text-[11px] text-slate-400">
                    {formatDateOnly(item.deletedAtUtc, i18n.language)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    {canMutate && (
                      <div className="inline-flex flex-wrap gap-2">
                        <button
                          type="button"
                          disabled={busyId === item.tenantId}
                          onClick={() => void handleRestore(item.tenantId)}
                          className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-[11px] font-bold text-white hover:bg-emerald-500 disabled:opacity-50"
                        >
                          <RotateCcw className="h-3.5 w-3.5" />
                          {t('superadminBilling.restoreBtn')}
                        </button>
                        <button
                          type="button"
                          onClick={() => setPermDeleteTarget(item)}
                          className="inline-flex items-center gap-1 rounded-lg border border-rose-700 bg-rose-950/60 px-3 py-1.5 text-[11px] font-bold text-rose-300 hover:bg-rose-900/60"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                          {t('superadminBilling.permanentDeleteBtn')}
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {permDeleteTarget && (
        <PermanentDeleteModal
          key={permDeleteTarget.tenantId}
          tenant={permDeleteTarget}
          onClose={() => setPermDeleteTarget(null)}
          onCompleted={() => {
            setPermDeleteTarget(null);
            void refreshAll();
          }}
        />
      )}
    </section>
  );
}

export function AuditLogsTab({ tenants }: { tenants: SuperAdminTenantSummary[] }) {
  const { t, i18n } = useTranslation();

  const [tenantId, setTenantId] = useState('');
  const [action, setAction] = useState('');
  const [entityName, setEntityName] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [search, setSearch] = useState('');
  const [exporting, setExporting] = useState(false);

  const logsQuery = useQuery({
    queryKey: ['superadmin', 'audit-logs', tenantId, action, entityName, fromDate, toDate, search],
    queryFn: () =>
      superAdminApi.listAuditLogs({
        tenantId: tenantId || undefined,
        action: action.trim() || undefined,
        entityName: entityName.trim() || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        search: search.trim() || undefined,
        limit: 120,
      }),
  });

  async function handleExportCsv() {
    setExporting(true);
    try {
      await superAdminApi.exportAuditLogsCsv({
        tenantId: tenantId || undefined,
        action: action.trim() || undefined,
        entityName: entityName.trim() || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        search: search.trim() || undefined,
      });
    } finally {
      setExporting(false);
    }
  }

  const rows = logsQuery.data ?? [];

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-sm space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-sm font-extrabold text-white flex items-center gap-2">
            <History className="h-4 w-4 text-amber-400" />
            {t('superadminBilling.auditTitle')}
          </h2>
          <p className="mt-0.5 text-xs text-slate-400">{t('superadminBilling.auditSubtitle')}</p>
        </div>
        <button
          type="button"
          disabled={exporting}
          onClick={() => void handleExportCsv()}
          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-800 px-3.5 py-2 text-xs font-bold text-slate-200 hover:bg-slate-700"
        >
          <Download className="h-3.5 w-3.5" />
          {t('superadminBilling.exportCsvBtn')}
        </button>
      </div>

      {/* Filters */}
      <div className="grid gap-2.5 sm:grid-cols-3 lg:grid-cols-6 text-xs">
        <select
          value={tenantId}
          onChange={(e) => setTenantId(e.target.value)}
          className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-2 text-slate-200"
        >
          <option value="">{t('superadminBilling.allTenantsOption')}</option>
          {tenants.map((gp) => (
            <option key={gp.tenantId} value={gp.tenantId}>
              {gp.panchayatNameEn} ({gp.lgdCode})
            </option>
          ))}
        </select>
        <input
          type="text"
          value={action}
          onChange={(e) => setAction(e.target.value)}
          placeholder={t('superadminBilling.actionFilterPlaceholder')}
          className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-2 text-slate-100 placeholder:text-slate-500"
        />
        <input
          type="text"
          value={entityName}
          onChange={(e) => setEntityName(e.target.value)}
          placeholder={t('superadminBilling.entityFilterPlaceholder')}
          className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-2 text-slate-100 placeholder:text-slate-500"
        />
        <input
          type="date"
          aria-label={t('superadminBilling.fromDateLabel')}
          value={fromDate}
          onChange={(e) => setFromDate(e.target.value)}
          className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-2 mono text-slate-200"
        />
        <input
          type="date"
          aria-label={t('superadminBilling.toDateLabel')}
          value={toDate}
          onChange={(e) => setToDate(e.target.value)}
          className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-2 mono text-slate-200"
        />
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('superadminBilling.searchAuditPlaceholder')}
          className="rounded-lg border border-slate-700 bg-slate-950 px-2.5 py-2 text-slate-100 placeholder:text-slate-500"
        />
      </div>

      {!logsQuery.isLoading && rows.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/60 p-8 text-center text-xs text-slate-400">
          {t('superadminBilling.noAuditLogs')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-800">
          <table className="min-w-full border-collapse text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-950 text-[11px] font-bold uppercase text-slate-400">
              <tr>
                <th className="px-3 py-2.5">{t('superadminBilling.auditTimestampCol')}</th>
                <th className="px-3 py-2.5">{t('superadminBilling.auditTenantCol')}</th>
                <th className="px-3 py-2.5">{t('superadminBilling.auditActorCol')}</th>
                <th className="px-3 py-2.5">{t('superadminBilling.auditActionCol')}</th>
                <th className="px-3 py-2.5">{t('superadminBilling.auditDetailsCol')}</th>
                <th className="px-3 py-2.5">{t('superadminBilling.auditIpCol')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800 font-mono text-[11px]">
              {rows.map((log) => (
                <tr key={log.id} className="hover:bg-slate-800/40">
                  <td className="px-3 py-2 text-slate-400 whitespace-nowrap">
                    {formatDateTime(log.timestampUtc, i18n.language)}
                  </td>
                  <td className="px-3 py-2 font-sans text-emerald-300">
                    {log.tenantNameEn || 'STATE'} {log.lgdCode ? `(${log.lgdCode})` : ''}
                  </td>
                  <td className="px-3 py-2 text-slate-200">
                    {log.actorId || '—'}
                    {log.actorRole ? (
                      <span className="ml-1 text-[10px] text-slate-400">({log.actorRole})</span>
                    ) : null}
                  </td>
                  <td className="px-3 py-2">
                    <span className="rounded bg-amber-500/20 px-1.5 py-0.5 text-[10px] font-bold text-amber-300">
                      {log.action}
                    </span>
                    <span className="ml-1.5 text-[10px] text-slate-400">{log.entityName}</span>
                  </td>
                  <td className="px-3 py-2 max-w-xs truncate text-slate-400" title={log.newValuesJson || ''}>
                    {log.newValuesJson || log.entityId}
                  </td>
                  <td className="px-3 py-2 text-slate-500">{log.ipAddress || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function PlatformSettingsForm({
  settings,
  canMutate,
  onSaved,
}: {
  settings: PlatformPaymentAccount;
  canMutate: boolean;
  onSaved: () => void;
}) {
  const { t } = useTranslation();
  const [form, setForm] = useState<UpdatePlatformSettingsPayload>({
    accountName: settings.accountName,
    bankName: settings.bankName,
    branchName: settings.branchName,
    accountNumber: settings.accountNumber,
    ifscCode: settings.ifscCode,
    upiId: settings.upiId,
    annualFee: settings.annualFee,
    gstPercent: settings.gstPercent,
    gstin: settings.gstin ?? '',
    supportPhone: settings.supportPhone ?? '',
    supportEmail: settings.supportEmail ?? '',
  });
  const [busy, setBusy] = useState(false);
  const [savedNotice, setSavedNotice] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const upiPreviewUri = `upi://pay?pa=${encodeURIComponent(form.upiId.trim())}&pn=${encodeURIComponent(form.accountName.trim())}&am=${form.annualFee.toFixed(2)}&cu=INR`;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSavedNotice(false);
    setBusy(true);
    try {
      await superAdminApi.updatePlatformSettings({
        ...form,
        gstin: form.gstin?.trim() || null,
        supportPhone: form.supportPhone?.trim() || null,
        supportEmail: form.supportEmail?.trim() || null,
      });
      setSavedNotice(true);
      onSaved();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to update platform settings.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={(e) => void handleSubmit(e)} className="grid gap-6 lg:grid-cols-3 text-xs">
      <div className="lg:col-span-2 space-y-4">
        {error && (
          <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200">
            {error}
          </div>
        )}
        {savedNotice && (
          <div className="rounded-lg border border-emerald-500/40 bg-emerald-950/40 p-3 text-emerald-200 flex items-center gap-2 font-bold">
            <CheckCircle2 className="h-4 w-4 text-emerald-400" />
            {t('superadminBilling.settingsSavedMsg')}
          </div>
        )}

        <div className="grid gap-3 sm:grid-cols-3">
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.annualFeeLabel')}</span>
            <input
              type="number"
              min={1}
              step="0.01"
              disabled={!canMutate}
              value={form.annualFee}
              onChange={(e) => setForm((p) => ({ ...p, annualFee: Number(e.target.value) }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono font-bold text-emerald-400"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.gstPercentLabel')}</span>
            <input
              type="number"
              min={0}
              max={50}
              step="0.5"
              disabled={!canMutate}
              value={form.gstPercent}
              onChange={(e) => setForm((p) => ({ ...p, gstPercent: Number(e.target.value) }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.gstinLabel')}</span>
            <input
              type="text"
              disabled={!canMutate}
              value={form.gstin || ''}
              onChange={(e) => setForm((p) => ({ ...p, gstin: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.accountNameLabel')}</span>
            <input
              type="text"
              required
              disabled={!canMutate}
              value={form.accountName}
              onChange={(e) => setForm((p) => ({ ...p, accountName: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.upiIdLabel')}</span>
            <input
              type="text"
              required
              disabled={!canMutate}
              value={form.upiId}
              onChange={(e) => setForm((p) => ({ ...p, upiId: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono font-bold text-amber-300"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.bankNameLabel')}</span>
            <input
              type="text"
              required
              disabled={!canMutate}
              value={form.bankName}
              onChange={(e) => setForm((p) => ({ ...p, bankName: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.branchNameLabel')}</span>
            <input
              type="text"
              required
              disabled={!canMutate}
              value={form.branchName}
              onChange={(e) => setForm((p) => ({ ...p, branchName: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.accountNumberLabel')}</span>
            <input
              type="text"
              required
              disabled={!canMutate}
              value={form.accountNumber}
              onChange={(e) => setForm((p) => ({ ...p, accountNumber: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.ifscCodeLabel')}</span>
            <input
              type="text"
              required
              disabled={!canMutate}
              value={form.ifscCode}
              onChange={(e) => setForm((p) => ({ ...p, ifscCode: e.target.value.toUpperCase() }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.supportPhoneLabel')}</span>
            <input
              type="text"
              disabled={!canMutate}
              value={form.supportPhone || ''}
              onChange={(e) => setForm((p) => ({ ...p, supportPhone: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-300">
            <span>{t('superadminBilling.supportEmailLabel')}</span>
            <input
              type="email"
              disabled={!canMutate}
              value={form.supportEmail || ''}
              onChange={(e) => setForm((p) => ({ ...p, supportEmail: e.target.value }))}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            />
          </label>
        </div>

        {canMutate && (
          <button
            type="submit"
            disabled={busy}
            className="inline-flex items-center gap-1.5 rounded-xl bg-amber-500 px-5 py-2.5 text-xs font-extrabold text-slate-950 hover:bg-amber-400 disabled:opacity-60"
          >
            <Save className="h-4 w-4" />
            {busy ? t('superadminBilling.savingSettingsBtn') : t('superadminBilling.saveSettingsBtn')}
          </button>
        )}
      </div>

      {/* Live UPI QR Preview Card */}
      <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 flex flex-col items-center justify-center text-center space-y-3">
        <p className="font-bold text-amber-300">{t('superadminBilling.qrLivePreviewLabel')}</p>
        <QrMatrixSvg value={upiPreviewUri} sizePx={156} label={form.upiId} />
        <div className="space-y-0.5 text-[11px]">
          <p className="font-bold text-white">{form.accountName}</p>
          <p className="mono text-amber-300">{form.upiId}</p>
          <p className="mono font-extrabold text-emerald-400">{formatInr(form.annualFee)}</p>
        </div>
      </div>
    </form>
  );
}

export function PlatformSettingsTab({ canMutate }: { canMutate: boolean }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const settingsQuery = useQuery({
    queryKey: ['superadmin', 'platform-settings'],
    queryFn: superAdminApi.getPlatformSettings,
  });

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6 shadow-sm space-y-4">
      <div>
        <h2 className="text-sm font-extrabold text-white flex items-center gap-2">
          <Settings className="h-4 w-4 text-amber-400" />
          {t('superadminBilling.settingsTitle')}
        </h2>
        <p className="mt-0.5 text-xs text-slate-400">{t('superadminBilling.settingsSubtitle')}</p>
      </div>

      {settingsQuery.isLoading && <div className="h-48 animate-pulse rounded-xl bg-slate-800" />}

      {settingsQuery.data && (
        <PlatformSettingsForm
          key={settingsQuery.data.updatedAtUtc}
          settings={settingsQuery.data}
          canMutate={canMutate}
          onSaved={() => void queryClient.invalidateQueries({ queryKey: ['superadmin', 'platform-settings'] })}
        />
      )}
    </section>
  );
}

