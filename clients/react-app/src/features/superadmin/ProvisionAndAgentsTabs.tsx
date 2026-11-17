import { useQueryClient } from '@tanstack/react-query';
import {
  Building2,
  CheckCircle2,
  Copy,
  KeyRound,
  Printer,
  ShieldCheck,
  UserCheck,
  X,
} from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { KycStatusBadge } from '../../components/StatusBadge';
import { ValidationSummary } from '../../components/ValidationSummary';
import { ApiProblemError } from '../../services/apiClient';
import { getPanchayatByLgd } from '../../services/lgdApi';
import {
  type AgentProfile,
  type ProvisionedTenantResult,
  type ProvisionTenantPayload,
  sprint2Api,
} from '../../services/sprint2Api';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import {
  evaluatePasswordStrength,
  extractProblemValidation,
  generateStrongPassword,
  type ParsedValidationItem,
} from '../../utils/validation';

export function ProvisionGpTab() {
  const { t, i18n } = useTranslation();
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

  const [lgdLoading, setLgdLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [errorItems, setErrorItems] = useState<ParsedValidationItem[]>([]);
  const [provisioned, setProvisioned] = useState<ProvisionedTenantResult | null>(null);
  const [copiedSlip, setCopiedSlip] = useState(false);
  const [lastProvisionedPassword, setLastProvisionedPassword] = useState('');

  async function handleLgdAutoFill() {
    if (!/^\d{5,8}$/.test(form.lgdCode.trim())) return;
    setErrorMsg(null);
    setLgdLoading(true);
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
    } finally {
      setLgdLoading(false);
    }
  }

  async function handleSubmitProvision(e: FormEvent) {
    e.preventDefault();
    setErrorMsg(null);
    setErrorItems([]);

    const items: ParsedValidationItem[] = [];
    if (!/^\d{5,8}$/.test(form.lgdCode.trim())) {
      items.push({ key: 'lgdCode', rawField: 'LgdCode', message: t('validation.lgdDigits') });
    }
    if (form.adminUsername.trim().length < 4) {
      items.push({
        key: 'adminUsername',
        rawField: 'AdminUsername',
        message: t('validation.usernameMin'),
      });
    }
    if (!evaluatePasswordStrength(form.adminPassword).isValid) {
      items.push({
        key: 'adminPassword',
        rawField: 'AdminPassword',
        message: t('validation.passwordRules'),
      });
    }
    if (items.length > 0) {
      setErrorMsg(t('validation.summaryTitle'));
      setErrorItems(items);
      return;
    }

    setSubmitting(true);
    try {
      const passwordSnapshot = form.adminPassword;
      const result = await sprint2Api.provisionTenantDirect(form);
      setLastProvisionedPassword(passwordSnapshot);
      setProvisioned(result);
      await queryClient.invalidateQueries({ queryKey: ['superadmin'] });
    } catch (err) {
      const parsed = extractProblemValidation(err, t('superadmin.provisionFailed'));
      setErrorMsg(parsed.summary);
      setErrorItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  }

  async function copyCredentialsSlip() {
    if (!provisioned) return;
    const text = [
      `GP: ${provisioned.panchayatNameMr} (${provisioned.panchayatNameEn})`,
      `LGD Code: ${provisioned.lgdCode}`,
      `Tenant ID: ${provisioned.tenantId}`,
      `Username: ${provisioned.adminUsername}`,
      `Password: ${lastProvisionedPassword}`,
      `Receipt Ref: ${provisioned.paymentReference}`,
      `Valid Until: ${formatDateOnly(provisioned.subscriptionExpiresAtUtc, i18n.language)}`,
    ].join('\n');
    await navigator.clipboard.writeText(text);
    setCopiedSlip(true);
    setTimeout(() => setCopiedSlip(false), 2500);
  }

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6 shadow-sm space-y-5">
      <div className="flex items-center gap-2 text-emerald-400">
        <Building2 className="h-5 w-5" />
        <h2 className="text-lg font-extrabold text-white">{t('superadmin.tabProvision')}</h2>
      </div>

      <ValidationSummary summary={errorMsg} items={errorItems} dark />

      {provisioned && (
        <div className="rounded-2xl border border-emerald-500/40 bg-emerald-950/40 p-5 space-y-3 text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2 font-extrabold text-emerald-300 text-sm">
              <CheckCircle2 className="h-5 w-5" />
              <span>{t('superadmin.successTitle')}</span>
            </div>
            <div className="flex gap-2 no-print">
              <button
                type="button"
                onClick={() => void copyCredentialsSlip()}
                className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-3 py-1.5 font-bold text-white hover:bg-emerald-500"
              >
                <Copy className="h-3.5 w-3.5" />
                {copiedSlip ? t('superadmin.copiedNotice') : t('superadmin.copyCredentialsBtn')}
              </button>
              <button
                type="button"
                onClick={() => window.print()}
                className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-500/40 bg-slate-900 px-3 py-1.5 font-bold text-emerald-200 hover:bg-slate-800"
              >
                <Printer className="h-3.5 w-3.5" />
                {t('superadmin.printSlipBtn')}
              </button>
            </div>
          </div>
          <div className="grid gap-2 sm:grid-cols-3 rounded-xl border border-emerald-500/20 bg-slate-950/70 p-3 mono text-[11px] text-slate-200">
            <p>
              GP: <strong>{provisioned.panchayatNameMr}</strong> ({provisioned.lgdCode})
            </p>
            <p>
              Username: <strong className="text-amber-300">{provisioned.adminUsername}</strong>
            </p>
            <p>
              Ref: <strong>{provisioned.paymentReference}</strong> · {formatInr(provisioned.amountPaid)}
            </p>
          </div>
        </div>
      )}

      <form onSubmit={(e) => void handleSubmitProvision(e)} className="space-y-5 text-xs">
        <div className="space-y-3">
          <p className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
            {t('superadmin.sectionGeo')}
          </p>
          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <label htmlFor="sup-lgd" className="block font-bold text-slate-300 mb-1">
                {t('onboarding.byCode')} <span className="text-rose-400">*</span>
              </label>
              <div className="flex gap-2">
                <input
                  id="sup-lgd"
                  type="text"
                  required
                  value={form.lgdCode}
                  onChange={(e) => setForm((p) => ({ ...p, lgdCode: e.target.value }))}
                  placeholder="171391"
                  className="flex-1 rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-amber-300 font-bold"
                />
                <button
                  type="button"
                  disabled={lgdLoading}
                  onClick={() => void handleLgdAutoFill()}
                  className="rounded-lg bg-slate-800 border border-slate-700 px-3 py-2 font-bold text-white hover:bg-slate-700 disabled:opacity-50"
                >
                  {lgdLoading ? '...' : t('onboarding.verifyBtn')}
                </button>
              </div>
            </div>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.gpNameMr')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.panchayatNameMr}
                onChange={(e) => setForm((p) => ({ ...p, panchayatNameMr: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.gpNameEn')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.panchayatNameEn}
                onChange={(e) => setForm((p) => ({ ...p, panchayatNameEn: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.district')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.districtName}
                onChange={(e) => setForm((p) => ({ ...p, districtName: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.tehsil')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.tehsilName}
                onChange={(e) => setForm((p) => ({ ...p, tehsilName: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('onboarding.pincode')}</span>
              <input
                type="text"
                value={form.pincode || ''}
                onChange={(e) => setForm((p) => ({ ...p, pincode: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>
          </div>
        </div>

        <div className="space-y-3 border-t border-slate-800 pt-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
              {t('superadmin.sectionAdmin')}
            </p>
            <button
              type="button"
              onClick={() =>
                setForm((p) => ({ ...p, adminPassword: generateStrongPassword() }))
              }
              className="inline-flex items-center gap-1 rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-[11px] font-bold text-amber-300 hover:bg-slate-700"
            >
              <KeyRound className="h-3 w-3" />
              {t('superadmin.generatePassBtn')}
            </button>
          </div>
          <div className="grid gap-3 sm:grid-cols-4">
            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('superadmin.adminFullName')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.adminFullName}
                onChange={(e) => setForm((p) => ({ ...p, adminFullName: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.adminUsername')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.adminUsername}
                onChange={(e) => setForm((p) => ({ ...p, adminUsername: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.adminEmail')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="email"
                required
                value={form.adminEmail}
                onChange={(e) => setForm((p) => ({ ...p, adminEmail: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('onboarding.adminPassword')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={form.adminPassword}
                onChange={(e) => setForm((p) => ({ ...p, adminPassword: e.target.value }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300 font-bold"
              />
            </label>
          </div>
        </div>

        <div className="space-y-3 border-t border-slate-800 pt-4">
          <p className="text-xs font-extrabold uppercase tracking-wider text-amber-300">
            {t('superadmin.sectionSubscription')}
          </p>
          <div className="grid gap-3 sm:grid-cols-5">
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.planLabel')}</span>
              <select
                value={form.planName}
                onChange={(e) =>
                  setForm((p) => ({
                    ...p,
                    planName: e.target.value as 'Standard' | 'Enterprise',
                  }))
                }
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              >
                <option value="Standard">{t('superadmin.planStandard')}</option>
                <option value="Enterprise">{t('superadmin.planEnterprise')}</option>
              </select>
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.paymentMode')}</span>
              <select
                value={form.paymentMode}
                onChange={(e) =>
                  setForm((p) => ({
                    ...p,
                    paymentMode: e.target.value as ProvisionTenantPayload['paymentMode'],
                  }))
                }
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              >
                <option value="UPI">{t('superadmin.modeUpi')}</option>
                <option value="NEFT_RTGS">{t('superadmin.modeNeft')}</option>
                <option value="CASH">{t('superadmin.modeCash')}</option>
                <option value="CHEQUE">{t('superadmin.modeCheque')}</option>
                <option value="TREASURY">{t('superadmin.modeTreasury')}</option>
              </select>
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.amount')}</span>
              <input
                type="number"
                min={1}
                value={form.paymentAmount}
                onChange={(e) => setForm((p) => ({ ...p, paymentAmount: Number(e.target.value) }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.durationDays')}</span>
              <select
                value={form.durationDays}
                onChange={(e) => setForm((p) => ({ ...p, durationDays: Number(e.target.value) }))}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              >
                <option value={365}>{t('superadmin.daysOption', { days: 365 })}</option>
                <option value={730}>{t('superadmin.daysOption', { days: 730 })}</option>
                <option value={1095}>{t('superadmin.daysOption', { days: 1095 })}</option>
              </select>
            </label>
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.paymentRef')}</span>
              <input
                type="text"
                value={form.paymentReference || ''}
                onChange={(e) => setForm((p) => ({ ...p, paymentReference: e.target.value }))}
                placeholder={t('superadmin.paymentRefPlaceholder')}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>
          </div>
        </div>

        <button
          type="submit"
          disabled={submitting}
          className="rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-6 py-2.5 text-xs font-bold text-white hover:from-emerald-500 hover:to-teal-500 disabled:opacity-60 transition shadow-md"
        >
          {submitting ? t('onboarding.loading') : t('superadmin.provisionBtn')}
        </button>
      </form>
    </section>
  );
}

export function AgentKycTab({
  agents,
  isLoading,
  canMutate,
}: {
  agents: AgentProfile[];
  isLoading: boolean;
  canMutate: boolean;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [agentKycFilter, setAgentKycFilter] = useState('ALL');
  const [reviewAgent, setReviewAgent] = useState<AgentProfile | null>(null);
  const [newGpRate, setNewGpRate] = useState(20);
  const [renewalRate, setRenewalRate] = useState(15);
  const [reviewReason, setReviewReason] = useState('');
  const [decisionBusy, setDecisionBusy] = useState(false);
  const [agentError, setAgentError] = useState<string | null>(null);

  const filteredAgents = useMemo(() => {
    if (agentKycFilter === 'ALL') return agents;
    return agents.filter((ag) => ag.kycStatus === agentKycFilter);
  }, [agents, agentKycFilter]);

  function openReviewModal(agent: AgentProfile) {
    setAgentError(null);
    setReviewAgent(agent);
    setNewGpRate(agent.requestedCommissionRatePct);
    setRenewalRate(Math.max(5, agent.requestedCommissionRatePct - 5));
    setReviewReason(agent.statusReason || '');
  }

  async function submitAgentDecision(status: number) {
    if (!reviewAgent) return;
    setAgentError(null);
    setDecisionBusy(true);
    try {
      await sprint2Api.updateAgentStatus(
        reviewAgent.agentId,
        status,
        newGpRate,
        renewalRate,
        reviewReason.trim() || undefined,
      );
      await queryClient.invalidateQueries({ queryKey: ['superadmin', 'agents'] });
      setReviewAgent(null);
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setAgentError(err.problem.detail || err.message);
      } else {
        setAgentError(err instanceof Error ? err.message : 'Update failed');
      }
    } finally {
      setDecisionBusy(false);
    }
  }

  return (
    <section className="rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-sm space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <UserCheck className="h-4 w-4 text-emerald-400" />
          <h2 className="text-sm font-extrabold text-white">
            {t('superadmin.agentKycQueueTitle')}
          </h2>
        </div>

        <select
          aria-label={t('superadmin.allKycStatuses')}
          value={agentKycFilter}
          onChange={(e) => setAgentKycFilter(e.target.value)}
          className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-xs text-slate-200"
        >
          <option value="ALL">{t('superadmin.allKycStatuses')}</option>
          <option value="PendingApproval">{t('kycStatus.PendingApproval')}</option>
          <option value="Approved">{t('kycStatus.Approved')}</option>
          <option value="Suspended">{t('kycStatus.Suspended')}</option>
          <option value="Rejected">{t('kycStatus.Rejected')}</option>
        </select>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {[1, 2].map((n) => (
            <div key={n} className="h-16 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      )}

      {!isLoading && filteredAgents.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/60 p-8 text-center text-xs text-slate-400">
          {t('superadmin.noAgentsMatch')}
        </div>
      ) : (
        <div className="space-y-2.5 text-xs">
          {filteredAgents.map((ag) => (
            <div
              key={ag.agentId}
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-950/80 p-4"
            >
              <div className="space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-extrabold text-white text-sm">{ag.fullName}</span>
                  <span className="mono rounded bg-slate-800 px-2 py-0.5 text-[11px] font-bold text-amber-300">
                    {ag.agentCode}
                  </span>
                  <KycStatusBadge status={ag.kycStatus} dark />
                </div>
                <p className="mono text-[11px] text-slate-400">
                  {ag.mobileNo} · {ag.aadhaarMasked} · {ag.panMasked} · {ag.district} ({ag.talukasCsv})
                </p>
                <p className="text-[11px] text-slate-400">
                  Requested: <strong className="text-white">{ag.requestedCommissionRatePct}%</strong>{' '}
                  {ag.kycStatus === 'Approved' && (
                    <span>
                      · Approved:{' '}
                      <strong className="text-emerald-400">
                        {ag.approvedNewGpRatePct}% / {ag.approvedRenewalRatePct}%
                      </strong>
                    </span>
                  )}
                </p>
              </div>

              {canMutate && (
                <button
                  type="button"
                  onClick={() => openReviewModal(ag)}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-amber-500/40 bg-amber-500/15 px-3.5 py-2 text-xs font-bold text-amber-300 hover:bg-amber-500/25 transition"
                >
                  <ShieldCheck className="h-4 w-4" />
                  {t('superadmin.reviewActionBtn')}
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {reviewAgent && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-xs"
          role="dialog"
          aria-modal="true"
          aria-labelledby="review-modal-title"
        >
          <div className="w-full max-w-lg rounded-2xl border border-slate-700 bg-slate-900 p-6 shadow-2xl space-y-4 text-xs text-slate-200">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 id="review-modal-title" className="text-sm font-extrabold text-white">
                {t('superadmin.modalReviewTitle')} — {reviewAgent.fullName}
              </h3>
              <button
                type="button"
                aria-label={t('superadmin.cancelBtn')}
                onClick={() => setReviewAgent(null)}
                className="rounded p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {agentError && (
              <div className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-3 py-2 text-rose-200">
                {agentError}
              </div>
            )}

            <div className="rounded-xl border border-slate-800 bg-slate-950 p-3 mono text-[11px] space-y-1">
              <p>Code: {reviewAgent.agentCode}</p>
              <p>
                Aadhaar: {reviewAgent.aadhaarMasked} · PAN: {reviewAgent.panMasked}
              </p>
              <p>
                District: {reviewAgent.district} · {reviewAgent.talukasCsv}
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="space-y-1 font-bold text-slate-300">
                <span>{t('superadmin.newGpRateLabel')}</span>
                <input
                  type="number"
                  min={1}
                  max={50}
                  value={newGpRate}
                  onChange={(e) => setNewGpRate(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300 font-bold"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-300">
                <span>{t('superadmin.renewalRateLabel')}</span>
                <input
                  type="number"
                  min={0}
                  max={50}
                  value={renewalRate}
                  onChange={(e) => setRenewalRate(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-sky-300 font-bold"
                />
              </label>
            </div>

            <label className="block space-y-1 font-bold text-slate-300">
              <span>{t('superadmin.reviewReasonLabel')}</span>
              <textarea
                rows={3}
                value={reviewReason}
                onChange={(e) => setReviewReason(e.target.value)}
                placeholder={t('superadmin.reviewReasonPlaceholder')}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <div className="flex flex-wrap justify-end gap-2 border-t border-slate-800 pt-3">
              <button
                type="button"
                disabled={decisionBusy}
                onClick={() => void submitAgentDecision(4)}
                className="rounded-lg bg-rose-700 px-3.5 py-2 font-bold text-white hover:bg-rose-600 disabled:opacity-50"
              >
                {t('superadmin.rejectBtn')}
              </button>
              <button
                type="button"
                disabled={decisionBusy}
                onClick={() => void submitAgentDecision(3)}
                className="rounded-lg bg-amber-600 px-3.5 py-2 font-bold text-white hover:bg-amber-500 disabled:opacity-50"
              >
                {t('superadmin.suspendBtn')}
              </button>
              <button
                type="button"
                disabled={decisionBusy}
                onClick={() => void submitAgentDecision(2)}
                className="rounded-lg bg-emerald-600 px-4 py-2 font-bold text-white hover:bg-emerald-500 disabled:opacity-50"
              >
                {t('superadmin.approveBtn')}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
