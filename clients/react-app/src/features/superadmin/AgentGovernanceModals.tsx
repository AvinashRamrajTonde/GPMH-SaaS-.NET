import { useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  FileDown,
  ShieldAlert,
  ShieldCheck,
  UploadCloud,
  Wand2,
  X,
} from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ProtectedAssetPreview } from '../../components/ProtectedAssetPreview';
import { KycStatusBadge } from '../../components/StatusBadge';
import {
  type AgentPayoutItem,
  agentPortalApi,
  type SuperAdminAgentSummary,
  superAdminAgentsApi,
} from '../../services/agentPortalApi';
import { formatDateOnly, formatInr } from '../../utils/formatters';
import { extractProblemValidation, generateStrongPassword } from '../../utils/validation';

export function CreateManualAgentModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: () => Promise<void>;
}) {
  const { t } = useTranslation();

  const [fullName, setFullName] = useState('');
  const [mobileNo, setMobileNo] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState(() => generateStrongPassword());
  const [address, setAddress] = useState('');
  const [villageOrCity, setVillageOrCity] = useState('');
  const [district, setDistrict] = useState('Amravati');
  const [talukasCsv, setTalukasCsv] = useState('');
  const [idProofType, setIdProofType] = useState('AADHAAR');
  const [aadhaarNumber, setAadhaarNumber] = useState('');
  const [panNumber, setPanNumber] = useState('');
  const [bankName, setBankName] = useState('');
  const [bankBranch, setBankBranch] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [ifscCode, setIfscCode] = useState('');
  const [upiId, setUpiId] = useState('');
  const [approvedNewGpRatePct, setApprovedNewGpRatePct] = useState(25);
  const [approvedRenewalRatePct, setApprovedRenewalRatePct] = useState(15);
  const [autoApprove, setAutoApprove] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await superAdminAgentsApi.createAgentManual({
        fullName: fullName.trim(),
        mobileNo: mobileNo.trim(),
        email: email.trim(),
        password,
        address: address.trim(),
        villageOrCity: villageOrCity.trim(),
        district: district.trim(),
        talukasCsv: talukasCsv.trim(),
        idProofType,
        aadhaarNumber: aadhaarNumber.trim(),
        panNumber: panNumber.trim().toUpperCase(),
        bankName: bankName.trim() || null,
        bankBranch: bankBranch.trim() || null,
        accountNumber: accountNumber.trim() || null,
        ifscCode: ifscCode.trim().toUpperCase() || null,
        upiId: upiId.trim() || null,
        approvedNewGpRatePct,
        approvedRenewalRatePct,
        autoApprove,
      });
      await onCreated();
      onClose();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to register Field Agent.').summary);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-xs p-4"
      role="dialog"
      aria-modal="true"
    >
      <div className="w-full max-w-2xl max-h-[92vh] overflow-y-auto rounded-2xl border border-amber-500/40 bg-slate-900 p-6 shadow-2xl space-y-4 text-xs">
        <div className="flex items-start justify-between border-b border-slate-800 pb-3">
          <div>
            <h2 className="text-base font-extrabold text-white">
              {t('superadminAgents.manualModalTitle')}
            </h2>
            <p className="text-[11px] text-slate-400 mt-0.5">
              {t('superadminAgents.manualModalSubtitle')}
            </p>
          </div>
          <button
            type="button"
            aria-label={t('superadmin.cancelBtn')}
            onClick={onClose}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {error && (
          <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200">
            {error}
          </div>
        )}

        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
          <div className="grid gap-3.5 sm:grid-cols-2">
            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.fullName')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.mobile')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="tel"
                required
                maxLength={10}
                pattern="\d{10}"
                value={mobileNo}
                onChange={(e) => setMobileNo(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-amber-300 font-bold focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.email')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <span className="font-bold text-slate-300">
                  {t('agentPortal.newPasswordLabel')} <span className="text-rose-400">*</span>
                </span>
                <button
                  type="button"
                  onClick={() => setPassword(generateStrongPassword())}
                  className="inline-flex items-center gap-1 text-[10.5px] font-bold text-amber-300 hover:underline"
                >
                  <Wand2 className="h-3 w-3" />
                  {t('superadmin.generatePassBtn')}
                </button>
              </div>
              <input
                type="text"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-amber-300 focus:border-amber-500 focus:outline-none"
              />
            </div>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('superadminAgents.aadhaarInput')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                maxLength={12}
                pattern="\d{12}"
                value={aadhaarNumber}
                onChange={(e) => setAadhaarNumber(e.target.value)}
                placeholder="12 digits"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('superadminAgents.panInput')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                maxLength={10}
                value={panNumber}
                onChange={(e) => setPanNumber(e.target.value)}
                placeholder="ABCDE1234F"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono uppercase text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('superadminAgents.idProofType')}</span>
              <select
                value={idProofType}
                onChange={(e) => setIdProofType(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100"
              >
                <option value="AADHAAR">{t('agent.idProofAadhaar')}</option>
                <option value="PAN">{t('agent.idProofPan')}</option>
              </select>
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.district')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={district}
                onChange={(e) => setDistrict(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.villageOrCityLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={villageOrCity}
                onChange={(e) => setVillageOrCity(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agent.talukasCsv')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={talukasCsv}
                onChange={(e) => setTalukasCsv(e.target.value)}
                placeholder="Achalpur, Morshi"
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <label className="sm:col-span-2 space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.addressLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-amber-500 focus:outline-none"
              />
            </label>

            <div className="rounded-xl border border-emerald-500/30 bg-slate-950 p-3 space-y-1">
              <label className="block font-bold text-emerald-400">
                {t('superadminAgents.newGpRate')}
              </label>
              <input
                type="number"
                step="0.5"
                min="0"
                max="50"
                required
                value={approvedNewGpRatePct}
                onChange={(e) => setApprovedNewGpRatePct(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono font-bold text-emerald-300"
              />
            </div>

            <div className="rounded-xl border border-sky-500/30 bg-slate-950 p-3 space-y-1">
              <label className="block font-bold text-sky-400">
                {t('superadminAgents.renewalRate')}
              </label>
              <input
                type="number"
                step="0.5"
                min="0"
                max="50"
                required
                value={approvedRenewalRatePct}
                onChange={(e) => setApprovedRenewalRatePct(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono font-bold text-sky-300"
              />
            </div>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.bankName')}</span>
              <input
                type="text"
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.branchName')}</span>
              <input
                type="text"
                value={bankBranch}
                onChange={(e) => setBankBranch(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.accountNo')}</span>
              <input
                type="text"
                value={accountNumber}
                onChange={(e) => setAccountNumber(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agent.ifscCode')}</span>
              <input
                type="text"
                value={ifscCode}
                onChange={(e) => setIfscCode(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono uppercase text-white"
              />
            </label>

            <label className="sm:col-span-2 space-y-1 font-bold text-slate-300">
              <span>{t('agent.upiIdLabel')}</span>
              <input
                type="text"
                value={upiId}
                onChange={(e) => setUpiId(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-emerald-300"
              />
            </label>
          </div>

          <label className="flex items-center gap-2 rounded-xl border border-emerald-500/30 bg-emerald-950/30 p-3 font-bold text-emerald-200">
            <input
              type="checkbox"
              checked={autoApprove}
              onChange={(e) => setAutoApprove(e.target.checked)}
              className="h-4 w-4 rounded border-slate-600 bg-slate-900 text-emerald-500"
            />
            <span>{t('superadminAgents.autoApproveLabel')}</span>
          </label>

          <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 font-bold text-slate-300 hover:bg-slate-700"
            >
              {t('superadmin.cancelBtn')}
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded-lg bg-emerald-600 px-5 py-2 font-extrabold text-white hover:bg-emerald-500 disabled:opacity-50"
            >
              {submitting
                ? t('superadminAgents.creatingAgent')
                : t('superadminAgents.createAgentSubmit')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function KycInspectionInner({
  agent,
  canMutate,
  onClose,
  onDecided,
}: {
  agent: SuperAdminAgentSummary;
  canMutate: boolean;
  onClose: () => void;
  onDecided: () => Promise<void>;
}) {
  const { t, i18n } = useTranslation();
  const [newGpRate, setNewGpRate] = useState(
    agent.approvedNewGpRatePct > 0
      ? agent.approvedNewGpRatePct
      : agent.requestedCommissionRatePct,
  );
  const [renewalRate, setRenewalRate] = useState(
    agent.approvedRenewalRatePct > 0
      ? agent.approvedRenewalRatePct
      : Math.max(5, agent.requestedCommissionRatePct - 5),
  );
  const [reason, setReason] = useState(agent.statusReason ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submitDecision(status: number) {
    setError(null);
    setBusy(true);
    try {
      await superAdminAgentsApi.reviewAgentKyc(agent.agentId, {
        status,
        approvedNewGpRatePct: newGpRate,
        approvedRenewalRatePct: renewalRate,
        reason: reason.trim() || undefined,
      });
      await onDecided();
      onClose();
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to update Agent KYC decision.').summary);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-xs p-4"
      role="dialog"
      aria-modal="true"
    >
      <div className="w-full max-w-2xl max-h-[92vh] overflow-y-auto rounded-2xl border border-emerald-500/40 bg-slate-900 p-6 shadow-2xl space-y-5 text-xs">
        <div className="flex items-start justify-between border-b border-slate-800 pb-3">
          <div className="space-y-1">
            <h2 className="text-base font-extrabold text-white flex items-center gap-2">
              <ShieldCheck className="h-5 w-5 text-emerald-400" />
              {t('superadminAgents.kycModalTitle')}
            </h2>
            <div className="flex flex-wrap items-center gap-2">
              <span className="mono font-bold text-amber-300">{agent.agentCode}</span>
              <KycStatusBadge status={agent.kycStatus} dark />
            </div>
          </div>
          <button
            type="button"
            aria-label={t('superadmin.cancelBtn')}
            onClick={onClose}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="grid gap-3 rounded-xl border border-slate-800 bg-slate-950 p-4 sm:grid-cols-2">
          <div>
            <p className="text-slate-400">{t('agent.fullName')}</p>
            <p className="font-bold text-white">{agent.fullName}</p>
          </div>
          <div>
            <p className="text-slate-400">{t('agent.mobile')}</p>
            <p className="mono font-bold text-amber-300">{agent.mobileNo}</p>
          </div>
          <div>
            <p className="text-slate-400">{t('agent.email')}</p>
            <p className="text-slate-200">{agent.email}</p>
          </div>
          <div>
            <p className="text-slate-400">{t('agent.territoryTitle')}</p>
            <p className="text-slate-200">
              {agent.district} ({agent.talukasCsv})
            </p>
          </div>
          <div>
            <p className="text-slate-400">{t('agent.aadhaarLabel')}</p>
            <p className="mono font-bold text-emerald-300">{agent.aadhaarMasked}</p>
          </div>
          <div>
            <p className="text-slate-400">{t('agent.panLabel')}</p>
            <p className="mono font-bold text-emerald-300">{agent.panMasked}</p>
          </div>
          <div>
            <p className="text-slate-400">{t('superadminAgents.agentUpiBankLabel')}</p>
            <p className="mono text-slate-200">
              {agent.upiId || agent.accountNumber || '—'}
              {agent.ifscCode ? ` (${agent.ifscCode})` : ''}
            </p>
          </div>
          <div>
            <p className="text-slate-400">{t('agentPortal.colDate')}</p>
            <p className="mono text-slate-300">{formatDateOnly(agent.createdAtUtc, i18n.language)}</p>
          </div>
        </div>

        {/* Encrypted DPDP Document Scans */}
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-xl border border-slate-800 bg-slate-950 p-3.5 space-y-2">
            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-200">
                {t('superadminAgents.aadhaarDocLabel')}
              </span>
              {agent.aadhaarDocumentKey && (
                <button
                  type="button"
                  onClick={() =>
                    void agentPortalApi.downloadProtectedAsset(
                      agent.aadhaarDocumentKey!,
                      `${agent.agentCode}-aadhaar`,
                    )
                  }
                  className="text-[11px] font-bold text-sky-300 hover:underline"
                >
                  {t('superadminAgents.downloadRawBtn')}
                </button>
              )}
            </div>
            {agent.aadhaarDocumentKey ? (
              <ProtectedAssetPreview
                assetKey={agent.aadhaarDocumentKey}
                alt={t('superadminAgents.aadhaarDocLabel')}
              />
            ) : (
              <p className="text-slate-500 italic">{t('superadminAgents.noDocUploaded')}</p>
            )}
          </div>

          <div className="rounded-xl border border-slate-800 bg-slate-950 p-3.5 space-y-2">
            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-200">
                {t('superadminAgents.panDocLabel')}
              </span>
              {agent.panDocumentKey && (
                <button
                  type="button"
                  onClick={() =>
                    void agentPortalApi.downloadProtectedAsset(
                      agent.panDocumentKey!,
                      `${agent.agentCode}-pan`,
                    )
                  }
                  className="text-[11px] font-bold text-sky-300 hover:underline"
                >
                  {t('superadminAgents.downloadRawBtn')}
                </button>
              )}
            </div>
            {agent.panDocumentKey ? (
              <ProtectedAssetPreview
                assetKey={agent.panDocumentKey}
                alt={t('superadminAgents.panDocLabel')}
              />
            ) : (
              <p className="text-slate-500 italic">{t('superadminAgents.noDocUploaded')}</p>
            )}
          </div>
        </div>

        {error && (
          <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200">
            {error}
          </div>
        )}

        {canMutate ? (
          <div className="space-y-4 border-t border-slate-800 pt-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="space-y-1 font-bold text-emerald-300">
                <span>{t('superadminAgents.newGpRate')}</span>
                <input
                  type="number"
                  min={0}
                  max={50}
                  step={0.5}
                  value={newGpRate}
                  onChange={(e) => setNewGpRate(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono font-bold text-emerald-300"
                />
              </label>

              <label className="space-y-1 font-bold text-sky-300">
                <span>{t('superadminAgents.renewalRate')}</span>
                <input
                  type="number"
                  min={0}
                  max={50}
                  step={0.5}
                  value={renewalRate}
                  onChange={(e) => setRenewalRate(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono font-bold text-sky-300"
                />
              </label>
            </div>

            <label className="block space-y-1 font-bold text-slate-300">
              <span>{t('superadminAgents.reasonLabel')}</span>
              <input
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder={t('superadmin.reviewReasonPlaceholder')}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white"
              />
            </label>

            <div className="flex flex-wrap justify-end gap-2">
              <button
                type="button"
                disabled={busy}
                onClick={() => void submitDecision(4)}
                className="rounded-lg border border-rose-500/40 bg-rose-950/60 px-4 py-2 font-bold text-rose-300 hover:bg-rose-900/60 disabled:opacity-50"
              >
                {t('superadminAgents.rejectKycBtn')}
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void submitDecision(3)}
                className="rounded-lg border border-amber-500/40 bg-amber-950/60 px-4 py-2 font-bold text-amber-300 hover:bg-amber-900/60 disabled:opacity-50"
              >
                {t('superadminAgents.suspendAgentBtn')}
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void submitDecision(2)}
                className="rounded-lg bg-emerald-600 px-5 py-2 font-extrabold text-white hover:bg-emerald-500 disabled:opacity-50"
              >
                {t('superadminAgents.approveKycBtn')}
              </button>
            </div>
          </div>
        ) : (
          <div className="flex justify-end border-t border-slate-800 pt-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg bg-slate-800 px-4 py-2 font-bold text-slate-200 hover:bg-slate-700"
            >
              {t('superadmin.cancelBtn')}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export function KycInspectionModal(props: {
  agent: SuperAdminAgentSummary;
  canMutate: boolean;
  onClose: () => void;
  onDecided: () => Promise<void>;
}) {
  return <KycInspectionInner key={props.agent.agentId} {...props} />;
}

function ExecuteSettlementInner({
  agents,
  initialAgentId,
  initialRequest,
  onClose,
}: {
  agents: SuperAdminAgentSummary[];
  initialAgentId?: string;
  initialRequest?: AgentPayoutItem | null;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const payableAgents = useMemo(() => {
    const withBalance = agents.filter((a) => a.pendingPayableBalance > 0);
    return withBalance.length > 0 ? withBalance : agents;
  }, [agents]);

  const defaultAgentId =
    initialRequest?.agentId || initialAgentId || payableAgents[0]?.agentId || '';

  const [selectedAgentId, setSelectedAgentId] = useState(defaultAgentId);
  const selectedAgent = useMemo(
    () => agents.find((a) => a.agentId === selectedAgentId) ?? null,
    [agents, selectedAgentId],
  );

  const [amount, setAmount] = useState(() => {
    if (initialRequest) return String(initialRequest.payoutAmount);
    const ag = agents.find((a) => a.agentId === defaultAgentId);
    return ag && ag.pendingPayableBalance > 0 ? String(ag.pendingPayableBalance) : '';
  });
  const [paymentMode, setPaymentMode] = useState(initialRequest?.paymentMode || 'UPI');
  const [utrReference, setUtrReference] = useState('');
  const [paymentDate, setPaymentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [remarks, setRemarks] = useState(initialRequest?.remarks ?? '');
  const [receiptFile, setReceiptFile] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [settledPayout, setSettledPayout] = useState<AgentPayoutItem | null>(null);

  function handleAgentChange(nextId: string) {
    setSelectedAgentId(nextId);
    if (!initialRequest) {
      const found = agents.find((a) => a.agentId === nextId);
      setAmount(found && found.pendingPayableBalance > 0 ? String(found.pendingPayableBalance) : '');
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!selectedAgentId) return;
    setError(null);
    setSubmitting(true);
    try {
      let receiptAssetKey: string | null = null;
      if (receiptFile) {
        const uploaded = await superAdminAgentsApi.uploadPayoutReceipt(receiptFile);
        receiptAssetKey = uploaded.assetKey;
      }

      const settled = await superAdminAgentsApi.settleAgentPayout(selectedAgentId, {
        amount: Number(amount),
        paymentMode,
        utrReference: utrReference.trim(),
        paymentDate,
        fulfillRequestedPayoutId: initialRequest?.payoutId ?? null,
        receiptAssetKey,
        remarks: remarks.trim() || null,
      });

      await queryClient.invalidateQueries({ queryKey: ['superadmin'] });
      setSettledPayout(settled);
    } catch (err) {
      setError(extractProblemValidation(err, 'Failed to disburse commission payout.').summary);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-xs p-4"
      role="dialog"
      aria-modal="true"
    >
      <div className="w-full max-w-xl max-h-[92vh] overflow-y-auto rounded-2xl border border-emerald-500/40 bg-slate-900 p-6 shadow-2xl space-y-4 text-xs">
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <h2 className="text-base font-extrabold text-white">
            {t('superadminAgents.settlementModalTitle')}
          </h2>
          <button
            type="button"
            aria-label={t('superadmin.cancelBtn')}
            onClick={onClose}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-800 hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {settledPayout ? (
          <div className="space-y-4">
            <div className="rounded-xl border border-emerald-500/40 bg-emerald-950/40 p-4 space-y-2 text-emerald-100">
              <div className="flex items-center gap-2 font-extrabold text-sm">
                <CheckCircle2 className="h-5 w-5 text-emerald-400" />
                <span>
                  {t('superadminAgents.settlementSuccessMsg', {
                    voucher: settledPayout.voucherNo,
                  })}
                </span>
              </div>
              <p className="mono text-xs text-emerald-200">
                {formatInr(settledPayout.payoutAmount)} · {settledPayout.paymentMode} · UTR:{' '}
                {settledPayout.utrReference}
              </p>
            </div>

            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() =>
                  void superAdminAgentsApi.downloadVoucherPdf(
                    settledPayout.payoutId,
                    settledPayout.voucherNo,
                  )
                }
                className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-600 px-4 py-2 font-extrabold text-white hover:bg-emerald-500"
              >
                <FileDown className="h-4 w-4" />
                {t('superadminAgents.downloadVoucherBtn')}
              </button>
              <button
                type="button"
                onClick={onClose}
                className="rounded-xl border border-slate-700 bg-slate-800 px-4 py-2 font-bold text-slate-200 hover:bg-slate-700"
              >
                {t('superadmin.cancelBtn')}
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
            {error && (
              <div className="rounded-xl border border-rose-500/40 bg-rose-950/60 p-3 text-rose-200 flex items-center gap-2">
                <ShieldAlert className="h-4 w-4 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {initialRequest && (
              <div className="rounded-xl border border-amber-500/40 bg-amber-950/40 p-3 font-bold text-amber-200">
                {t('superadminAgents.fulfillingRequestNote', {
                  voucher: initialRequest.voucherNo,
                  amount: formatInr(initialRequest.payoutAmount),
                })}
              </div>
            )}

            <label className="block space-y-1 font-bold text-slate-300">
              <span>{t('superadminAgents.selectAgentLabel')}</span>
              <select
                required
                disabled={Boolean(initialRequest)}
                value={selectedAgentId}
                onChange={(e) => handleAgentChange(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 disabled:opacity-60"
              >
                {payableAgents.map((ag) => (
                  <option key={ag.agentId} value={ag.agentId}>
                    {ag.fullName} ({ag.agentCode}) — {formatInr(ag.pendingPayableBalance)}
                  </option>
                ))}
              </select>
            </label>

            {selectedAgent && (
              <div className="grid gap-4 rounded-xl border border-slate-800 bg-slate-950 p-4 sm:grid-cols-2">
                <div>
                  <p className="text-[11px] text-slate-400">
                    {t('superadminAgents.agentBalanceLabel')}
                  </p>
                  <p className="mono text-lg font-extrabold text-rose-400">
                    {formatInr(selectedAgent.pendingPayableBalance)}
                  </p>
                </div>
                <div className="sm:text-right">
                  <p className="text-[11px] text-slate-400">
                    {t('superadminAgents.agentUpiBankLabel')}
                  </p>
                  <p className="mono font-bold text-emerald-400">
                    {selectedAgent.upiId || selectedAgent.accountNumber || '—'}
                  </p>
                </div>
              </div>
            )}

            <div className="space-y-1">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <label className="font-bold text-slate-300">
                  {t('superadminAgents.payoutAmountLabel')} <span className="text-rose-400">*</span>
                </label>
                <span className="rounded bg-emerald-500/15 px-2 py-0.5 text-[10px] font-bold text-emerald-300">
                  {t('superadminAgents.anyAmountHint')}
                </span>
              </div>
              <input
                type="number"
                step="0.01"
                min="1"
                max={selectedAgent?.pendingPayableBalance || undefined}
                required
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-base font-extrabold text-amber-300 focus:border-emerald-500 focus:outline-none"
              />
            </div>

            <div className="grid gap-3.5 sm:grid-cols-2">
              <label className="space-y-1 font-bold text-slate-300">
                <span>
                  {t('superadminAgents.paymentModeLabel')} <span className="text-rose-400">*</span>
                </span>
                <select
                  value={paymentMode}
                  onChange={(e) => setPaymentMode(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100"
                >
                  <option value="UPI">UPI</option>
                  <option value="NEFT_RTGS">NEFT / RTGS</option>
                  <option value="IMPS">IMPS</option>
                  <option value="CASH">CASH</option>
                  <option value="BANK_TRANSFER">BANK_TRANSFER</option>
                </select>
              </label>

              <label className="space-y-1 font-bold text-slate-300">
                <span>
                  {t('superadminAgents.utrRefLabel')} <span className="text-rose-400">*</span>
                </span>
                <input
                  type="text"
                  required
                  value={utrReference}
                  onChange={(e) => setUtrReference(e.target.value)}
                  placeholder="UTR429103918291"
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-300">
                <span>
                  {t('superadminAgents.paymentDateLabel')} <span className="text-rose-400">*</span>
                </span>
                <input
                  type="date"
                  required
                  value={paymentDate}
                  onChange={(e) => setPaymentDate(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 mono text-slate-100"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-300">
                <span className="inline-flex items-center gap-1">
                  <UploadCloud className="h-3.5 w-3.5 text-sky-400" />
                  {t('superadminAgents.receiptUploadLabel')}
                </span>
                <input
                  type="file"
                  accept=".pdf,.png,.jpg,.jpeg,.webp"
                  onChange={(e) => setReceiptFile(e.target.files?.[0] ?? null)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-950 px-2 py-1.5 text-[11px] text-slate-300 file:mr-2 file:rounded file:border-0 file:bg-slate-800 file:px-2 file:py-1 file:text-slate-200"
                />
              </label>
            </div>

            <label className="block space-y-1 font-bold text-slate-300">
              <span>{t('superadminAgents.remarksLabel')}</span>
              <input
                type="text"
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <div className="flex justify-end gap-2 border-t border-slate-800 pt-3">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 font-bold text-slate-300 hover:bg-slate-700"
              >
                {t('superadmin.cancelBtn')}
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="rounded-lg bg-emerald-600 px-5 py-2 font-extrabold text-white hover:bg-emerald-500 disabled:opacity-50"
              >
                {submitting
                  ? t('superadminAgents.disbursingBtn')
                  : t('superadminAgents.disburseSubmitBtn')}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

export function ExecuteSettlementModal(props: {
  agents: SuperAdminAgentSummary[];
  initialAgentId?: string;
  initialRequest?: AgentPayoutItem | null;
  onClose: () => void;
}) {
  return (
    <ExecuteSettlementInner
      key={`${props.initialAgentId ?? ''}-${props.initialRequest?.payoutId ?? ''}`}
      {...props}
    />
  );
}

