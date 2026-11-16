import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, CheckCircle2, ShieldAlert, ShieldCheck, UserCheck } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { getPanchayatByLgd } from '../../services/lgdApi';
import { sprint2Api, type ProvisionedTenantResult, type ProvisionTenantPayload } from '../../services/sprint2Api';

export function SuperAdminProvisionPage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [form, setForm] = useState<ProvisionTenantPayload>({
    lgdCode: '',
    panchayatNameEn: '',
    panchayatNameMr: '',
    districtName: '',
    tehsilName: '',
    districtCode: '27',
    tehsilCode: '000',
    pincode: '',
    censusCode: '',
    officeAddress: '',
    adminFullName: '',
    adminUsername: '',
    adminEmail: '',
    adminPassword: '',
    planName: 'Standard',
    paymentMode: 'UPI',
    paymentAmount: 6000,
    durationDays: 365,
    paymentReference: '',
    totalWards: 5,
    populationCensus: null,
  });

  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [provisioned, setProvisioned] = useState<ProvisionedTenantResult | null>(null);

  const isSuperAdmin = Boolean(accessToken && user?.role === 'SuperAdmin');

  const tenantsQuery = useQuery({
    queryKey: ['superadmin', 'tenants'],
    queryFn: sprint2Api.listSuperAdminTenants,
    enabled: isSuperAdmin,
  });

  const agentsQuery = useQuery({
    queryKey: ['superadmin', 'agents'],
    queryFn: sprint2Api.listSuperAdminAgents,
    enabled: isSuperAdmin,
  });

  async function handleLgdAutoFill() {
    if (!/^\d{5,8}$/.test(form.lgdCode.trim())) return;
    setErrorMsg(null);
    try {
      const gp = await getPanchayatByLgd(form.lgdCode);
      setForm((prev) => ({
        ...prev,
        panchayatNameEn: gp.gp_name_en,
        panchayatNameMr: gp.gp_name_mr,
        districtName: gp.district,
        districtCode: gp.district_code || '27',
        tehsilName: gp.tehsil,
        tehsilCode: gp.tehsil_code || '000',
        pincode: gp.pincode || '',
      }));
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : t('onboarding.notFound'));
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setSubmitting(true);
    try {
      const result = await sprint2Api.provisionTenantDirect(form);
      setProvisioned(result);
      await queryClient.invalidateQueries({ queryKey: ['superadmin', 'tenants'] });
    } catch (err) {
      if (err instanceof ApiProblemError) {
        const firstFieldErr = err.problem.errors ? Object.values(err.problem.errors).flat()[0] : null;
        setErrorMsg(firstFieldErr || err.problem.detail || err.message);
      } else {
        setErrorMsg(err instanceof Error ? err.message : t('superadmin.provisionFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleAgentDecision(agentId: string, status: number, reqRate: number) {
    await sprint2Api.updateAgentStatus(agentId, status, reqRate, Math.max(5, reqRate - 5), 'Reviewed in SuperAdmin HQ');
    await queryClient.invalidateQueries({ queryKey: ['superadmin', 'agents'] });
  }

  if (!isSuperAdmin) {
    return (
      <div className="mx-auto max-w-lg rounded-2xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-950 space-y-2">
        <div className="flex items-center gap-2 font-bold">
          <ShieldCheck className="h-5 w-5" />
          <span>{t('superadmin.title')}</span>
        </div>
        <p className="text-xs">{t('superadmin.requireSuperAdmin')}</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm space-y-5">
        <div className="flex items-center gap-2 text-emerald-800">
          <Building2 className="h-5 w-5" />
          <h1 className="text-xl font-bold text-slate-900">{t('superadmin.title')}</h1>
        </div>

        {errorMsg && (
          <div className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs text-rose-800">
            <ShieldAlert className="h-4 w-4 shrink-0" />
            <span>{errorMsg}</span>
          </div>
        )}

        {provisioned && (
          <div className="rounded-xl border border-emerald-300 bg-emerald-50 p-4 space-y-2 text-xs">
            <div className="flex items-center gap-2 font-bold text-emerald-900 text-sm">
              <CheckCircle2 className="h-5 w-5" />
              <span>{t('superadmin.successTitle')}</span>
            </div>
            <p className="font-mono text-emerald-900">
              {provisioned.panchayatNameMr} ({provisioned.lgdCode}) · {provisioned.status} · Login:{' '}
              <strong>{provisioned.adminUsername}</strong> · Ref: {provisioned.paymentReference}
            </p>
          </div>
        )}

        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4 text-xs">
          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.byCode')}</label>
              <div className="flex gap-2">
                <input
                  type="text"
                  required
                  value={form.lgdCode}
                  onChange={(e) => setForm((p) => ({ ...p, lgdCode: e.target.value }))}
                  placeholder="171391"
                  className="flex-1 rounded-lg border border-slate-300 px-3 py-2 font-mono"
                />
                <button
                  type="button"
                  onClick={() => void handleLgdAutoFill()}
                  className="rounded-lg bg-slate-800 px-3 py-2 font-bold text-white hover:bg-slate-700"
                >
                  {t('onboarding.verifyBtn')}
                </button>
              </div>
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.gpNameMr')}</label>
              <input
                type="text"
                required
                value={form.panchayatNameMr}
                onChange={(e) => setForm((p) => ({ ...p, panchayatNameMr: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.gpNameEn')}</label>
              <input
                type="text"
                required
                value={form.panchayatNameEn}
                onChange={(e) => setForm((p) => ({ ...p, panchayatNameEn: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.district')}</label>
              <input
                type="text"
                required
                value={form.districtName}
                onChange={(e) => setForm((p) => ({ ...p, districtName: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.tehsil')}</label>
              <input
                type="text"
                required
                value={form.tehsilName}
                onChange={(e) => setForm((p) => ({ ...p, tehsilName: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.pincode')}</label>
              <input
                type="text"
                value={form.pincode || ''}
                onChange={(e) => setForm((p) => ({ ...p, pincode: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono"
              />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-4 border-t border-slate-100 pt-4">
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('superadmin.adminFullName')}</label>
              <input
                type="text"
                required
                value={form.adminFullName}
                onChange={(e) => setForm((p) => ({ ...p, adminFullName: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.adminUsername')}</label>
              <input
                type="text"
                required
                value={form.adminUsername}
                onChange={(e) => setForm((p) => ({ ...p, adminUsername: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono"
              />
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.adminEmail')}</label>
              <input
                type="email"
                required
                value={form.adminEmail}
                onChange={(e) => setForm((p) => ({ ...p, adminEmail: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('onboarding.adminPassword')}</label>
              <input
                type="text"
                required
                value={form.adminPassword}
                onChange={(e) => setForm((p) => ({ ...p, adminPassword: e.target.value }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono"
              />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-5 border-t border-slate-100 pt-4">
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('superadmin.planLabel')}</label>
              <select
                value={form.planName}
                onChange={(e) =>
                  setForm((p) => ({ ...p, planName: e.target.value as 'Standard' | 'Enterprise' }))
                }
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              >
                <option value="Standard">Standard</option>
                <option value="Enterprise">Enterprise</option>
              </select>
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('superadmin.paymentMode')}</label>
              <select
                value={form.paymentMode}
                onChange={(e) =>
                  setForm((p) => ({
                    ...p,
                    paymentMode: e.target.value as ProvisionTenantPayload['paymentMode'],
                  }))
                }
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              >
                <option value="UPI">UPI</option>
                <option value="NEFT_RTGS">NEFT / RTGS</option>
                <option value="CASH">Cash</option>
                <option value="CHEQUE">Cheque</option>
                <option value="TREASURY">Treasury</option>
              </select>
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('superadmin.amount')}</label>
              <input
                type="number"
                min={1}
                value={form.paymentAmount}
                onChange={(e) => setForm((p) => ({ ...p, paymentAmount: Number(e.target.value) }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono"
              />
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('superadmin.durationDays')}</label>
              <select
                value={form.durationDays}
                onChange={(e) => setForm((p) => ({ ...p, durationDays: Number(e.target.value) }))}
                className="w-full rounded-lg border border-slate-300 px-3 py-2"
              >
                <option value={365}>365 Days</option>
                <option value={730}>730 Days</option>
                <option value={1095}>1095 Days</option>
              </select>
            </div>
            <div>
              <label className="block font-bold text-slate-700 mb-1">{t('superadmin.paymentRef')}</label>
              <input
                type="text"
                value={form.paymentReference || ''}
                onChange={(e) => setForm((p) => ({ ...p, paymentReference: e.target.value }))}
                placeholder="UTR-2026-..."
                className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="rounded-xl bg-emerald-700 px-5 py-2.5 text-xs font-bold text-white hover:bg-emerald-800 disabled:opacity-60"
          >
            {submitting ? t('onboarding.loading') : t('superadmin.provisionBtn')}
          </button>
        </form>
      </section>

      <section className="grid gap-6 lg:grid-cols-2">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm space-y-3">
          <h2 className="text-sm font-bold text-slate-900">{t('superadmin.tenantsListTitle')}</h2>
          <div className="space-y-2 text-xs">
            {tenantsQuery.data?.map((item) => (
              <div key={item.tenantId} className="flex items-center justify-between rounded-lg border border-slate-100 bg-slate-50 px-3 py-2">
                <div>
                  <p className="font-bold text-slate-900">{item.panchayatNameMr} ({item.lgdCode})</p>
                  <p className="text-[11px] text-slate-500">{item.districtName} · {item.tehsilName}</p>
                </div>
                <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 font-mono text-[11px] font-bold text-emerald-800">
                  {item.status}
                </span>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm space-y-3">
          <div className="flex items-center gap-2">
            <UserCheck className="h-4 w-4 text-emerald-700" />
            <h2 className="text-sm font-bold text-slate-900">{t('superadmin.agentKycQueueTitle')}</h2>
          </div>
          <div className="space-y-2 text-xs">
            {agentsQuery.data?.map((ag) => (
              <div key={ag.agentId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-slate-100 bg-slate-50 px-3 py-2">
                <div>
                  <p className="font-bold text-slate-900">{ag.fullName} ({ag.agentCode})</p>
                  <p className="font-mono text-[11px] text-slate-500">
                    {ag.mobileNo} · {ag.aadhaarMasked} · {ag.district}
                  </p>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="rounded bg-amber-100 px-2 py-0.5 font-mono text-[10px] font-bold text-amber-900">
                    {String(ag.kycStatus)}
                  </span>
                  <button
                    type="button"
                    onClick={() => void handleAgentDecision(ag.agentId, 2, ag.requestedCommissionRatePct)}
                    className="rounded bg-emerald-700 px-2 py-1 font-bold text-white hover:bg-emerald-800"
                  >
                    {t('superadmin.approveBtn')}
                  </button>
                  <button
                    type="button"
                    onClick={() => void handleAgentDecision(ag.agentId, 4, ag.requestedCommissionRatePct)}
                    className="rounded bg-rose-700 px-2 py-1 font-bold text-white hover:bg-rose-800"
                  >
                    {t('superadmin.rejectBtn')}
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}
