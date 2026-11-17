import { useQueryClient } from '@tanstack/react-query';
import {
  Building2,
  CheckCircle2,
  Eye,
  EyeOff,
  RefreshCw,
  Search,
  Sparkles,
  Wand2,
} from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { TenantStatusBadge } from '../../components/StatusBadge';
import { ValidationSummary } from '../../components/ValidationSummary';
import {
  type AgentVillageItem,
  agentPortalApi,
} from '../../services/agentPortalApi';
import { getPanchayatByLgd } from '../../services/lgdApi';
import {
  formatDateOnly,
  formatInr,
} from '../../utils/formatters';
import {
  evaluatePasswordStrength,
  extractProblemValidation,
  generateStrongPassword,
  type ParsedValidationItem,
} from '../../utils/validation';

const DEFAULT_ANNUAL_PLAN_FEE = 6000;

export function AgentAddGpTab({
  approvedNewGpRatePct,
  onSuccessJumpToVillages,
}: {
  approvedNewGpRatePct: number;
  onSuccessJumpToVillages: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [lgdCode, setLgdCode] = useState('');
  const [districtCode, setDistrictCode] = useState('27');
  const [districtName, setDistrictName] = useState('');
  const [tehsilCode, setTehsilCode] = useState('');
  const [tehsilName, setTehsilName] = useState('');
  const [panchayatNameEn, setPanchayatNameEn] = useState('');
  const [panchayatNameMr, setPanchayatNameMr] = useState('');
  const [pincode, setPincode] = useState('');
  const [officeAddress, setOfficeAddress] = useState('');
  const [totalWards, setTotalWards] = useState(5);
  const [assessmentYearLabel, setAssessmentYearLabel] = useState('2026-27');

  const [gramsevakName, setGramsevakName] = useState('');
  const [adminUsername, setAdminUsername] = useState('');
  const [adminEmail, setAdminEmail] = useState('');
  const [adminPassword, setAdminPassword] = useState(() => generateStrongPassword());
  const [showPass, setShowPass] = useState(false);

  const [bankName, setBankName] = useState('State Bank of India');
  const [branchName, setBranchName] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [ifscCode, setIfscCode] = useState('');
  const [upiId, setUpiId] = useState('');
  const [seedDefaultFormulas, setSeedDefaultFormulas] = useState(true);

  const [lookingUpLgd, setLookingUpLgd] = useState(false);
  const [lgdLookupError, setLgdLookupError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [validationItems, setValidationItems] = useState<ParsedValidationItem[]>([]);
  const [createdBanner, setCreatedBanner] = useState<{ name: string; lgd: string } | null>(null);

  const estimatedCommission = useMemo(
    () => Math.round(((DEFAULT_ANNUAL_PLAN_FEE * approvedNewGpRatePct) / 100) * 100) / 100,
    [approvedNewGpRatePct],
  );

  const passwordCheck = evaluatePasswordStrength(adminPassword);

  async function handleLgdAutoFill() {
    const clean = lgdCode.trim();
    if (!/^\d{5,8}$/.test(clean)) {
      setLgdLookupError(t('validation.lgdDigits'));
      return;
    }
    setLgdLookupError(null);
    setLookingUpLgd(true);
    try {
      const detail = await getPanchayatByLgd(clean);
      setDistrictName(detail.district);
      setTehsilName(detail.tehsil);
      setDistrictCode(detail.district_code || '27');
      setTehsilCode(detail.tehsil_code || '');
      setPanchayatNameEn(detail.gp_name_en);
      setPanchayatNameMr(detail.gp_name_mr);
      if (detail.pincode) setPincode(detail.pincode);
      if (!adminUsername.trim()) {
        setAdminUsername(`gp_${clean}`);
      }
    } catch {
      setLgdLookupError(t('agentPortal.lgdNotFound'));
    } finally {
      setLookingUpLgd(false);
    }
  }

  function parseAyYears(ay: string): { start: number; end: number } {
    const m = /^(\d{4})-(\d{2})$/.exec(ay.trim());
    if (!m) return { start: 2026, end: 2027 };
    const start = Number(m[1]);
    return { start, end: start + 1 };
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSummaryError(null);
    setValidationItems([]);
    setCreatedBanner(null);

    const hasAnyBankDetail = Boolean(branchName.trim() || accountNumber.trim() || ifscCode.trim());
    if (hasAnyBankDetail && (!branchName.trim() || !accountNumber.trim() || !ifscCode.trim())) {
      setSummaryError(t('validation.branchRequired'));
      return;
    }

    const { start, end } = parseAyYears(assessmentYearLabel);
    setSubmitting(true);
    try {
      const created = await agentPortalApi.assistedRegisterGp({
        adminUsername: adminUsername.trim(),
        adminPassword,
        adminEmail: adminEmail.trim() || null,
        wizard: {
          lgdCode: lgdCode.trim(),
          districtCode: districtCode.trim() || '27',
          districtName: districtName.trim(),
          tehsilCode: tehsilCode.trim(),
          tehsilName: tehsilName.trim(),
          panchayatNameEn: panchayatNameEn.trim(),
          panchayatNameMr: panchayatNameMr.trim(),
          pincode: pincode.trim() || null,
          censusCode: null,
          officeAddress: officeAddress.trim() || null,
          totalWards: Math.max(1, Math.min(50, Number(totalWards) || 5)),
          populationCensus: null,
          sarpanchName: null,
          sarpanchMobile: null,
          upSarpanchName: null,
          gramsevakName: gramsevakName.trim() || null,
          gramsevakMobile: null,
          operatorName: null,
          operatorMobile: null,
          gramsevakSignatureKey: null,
          sarpanchSignatureKey: null,
          operatorSignatureKey: null,
          officialStampKey: null,
          assessmentYearLabel: assessmentYearLabel.trim(),
          assessmentStartYear: start,
          assessmentEndYear: end,
          lockAssessmentYear: false,
          initializeArrearsRollover: false,
          primaryUpiId: upiId.trim() || null,
          upiPayeeName: panchayatNameEn.trim() || null,
          bankAccounts: hasAnyBankDetail
            ? [
                {
                  fundType: 'GRAM_NIDHI',
                  bankName: bankName.trim() || 'State Bank of India',
                  branchName: branchName.trim(),
                  accountNumber: accountNumber.trim(),
                  ifscCode: ifscCode.trim().toUpperCase(),
                  upiId: upiId.trim() || null,
                  upiPayeeName: panchayatNameEn.trim() || null,
                  qrImageKey: null,
                  openingBalance: 0,
                },
              ]
            : [],
          seedDefaultFormulas,
          activateTenant: false,
          provenance: null,
        },
      });

      await queryClient.invalidateQueries({ queryKey: ['agent'] });
      setCreatedBanner({
        name: created.panchayatNameMr || created.panchayatNameEn || panchayatNameEn,
        lgd: created.lgdCode || lgdCode.trim(),
      });
      setLgdCode('');
      setDistrictName('');
      setTehsilName('');
      setPanchayatNameEn('');
      setPanchayatNameMr('');
      setPincode('');
      setOfficeAddress('');
      setGramsevakName('');
      setAdminUsername('');
      setAdminEmail('');
      setAdminPassword(generateStrongPassword());
      setBranchName('');
      setAccountNumber('');
      setIfscCode('');
      setUpiId('');
    } catch (err) {
      const parsed = extractProblemValidation(err, t('onboarding.submitFailed'));
      setSummaryError(parsed.summary);
      setValidationItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-5">
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-2xl border border-slate-800 bg-slate-900 p-5 space-y-2">
          <div className="flex items-center gap-2 text-emerald-400">
            <Building2 className="h-5 w-5" />
            <h2 className="text-base font-extrabold text-white">{t('agentPortal.addGpTitle')}</h2>
          </div>
          <p className="text-xs text-slate-400">
            {t('agentPortal.addGpSubtitle', { rate: approvedNewGpRatePct.toFixed(1) })}
          </p>
        </div>

        <div className="rounded-2xl border border-emerald-500/40 bg-emerald-950/30 p-5 flex flex-col justify-between">
          <p className="text-xs font-bold uppercase tracking-wider text-emerald-300 flex items-center gap-1.5">
            <Sparkles className="h-4 w-4" />
            {t('agentPortal.estCommissionCardLabel')}
          </p>
          <p className="mono text-2xl font-extrabold text-emerald-400 mt-1">
            {formatInr(estimatedCommission)}
          </p>
          <p className="text-[11px] text-emerald-200/80 mt-1">
            {t('agentPortal.estCommissionFormula', {
              fee: DEFAULT_ANNUAL_PLAN_FEE.toLocaleString('en-IN'),
              rate: approvedNewGpRatePct.toFixed(1),
            })}
          </p>
        </div>
      </div>

      {createdBanner && (
        <div className="rounded-xl border border-emerald-500/40 bg-emerald-950/40 p-4 text-xs text-emerald-100 flex flex-wrap items-center justify-between gap-3">
          <span className="inline-flex items-center gap-2 font-bold">
            <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0" />
            {t('agentPortal.gpSubmittedMsg', {
              name: createdBanner.name,
              lgd: createdBanner.lgd,
            })}
          </span>
          <button
            type="button"
            onClick={onSuccessJumpToVillages}
            className="rounded-lg bg-emerald-600 px-3 py-1.5 font-bold text-white hover:bg-emerald-500"
          >
            {t('agentPortal.tabMyVillages')}
          </button>
        </div>
      )}

      {summaryError && (
        <ValidationSummary
          summary={summaryError}
          items={validationItems}
          resolveStep={() => 1}
          currentStep={1}
          onJumpToStep={() => {}}
          dark
        />
      )}

      <form
        onSubmit={(e) => void handleSubmit(e)}
        className="rounded-2xl border border-slate-800 bg-slate-900 p-6 space-y-6 text-xs"
      >
        <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-4">
          <h3 className="font-bold text-emerald-400 uppercase tracking-wider">
            1. {t('superadmin.sectionGeo')}
          </h3>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div className="space-y-1">
              <label className="block font-bold text-slate-300">
                {t('agentPortal.lgdCodeLabel')} <span className="text-rose-400">*</span>
              </label>
              <div className="flex gap-2">
                <input
                  type="text"
                  required
                  value={lgdCode}
                  onChange={(e) => setLgdCode(e.target.value)}
                  placeholder="271234"
                  className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-amber-300 focus:border-emerald-500 focus:outline-none"
                />
                <button
                  type="button"
                  disabled={lookingUpLgd}
                  onClick={() => void handleLgdAutoFill()}
                  className="shrink-0 inline-flex items-center gap-1 rounded-lg border border-emerald-500/40 bg-emerald-500/15 px-3 py-2 font-bold text-emerald-300 hover:bg-emerald-500/25 disabled:opacity-50"
                >
                  <RefreshCw className={`h-3.5 w-3.5 ${lookingUpLgd ? 'animate-spin' : ''}`} />
                  {lookingUpLgd ? t('agentPortal.lgdLookingUp') : t('agentPortal.lgdLookupBtn')}
                </button>
              </div>
              {lgdLookupError && <p className="text-[11px] text-rose-400">{lgdLookupError}</p>}
            </div>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.panchayatEnLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={panchayatNameEn}
                onChange={(e) => setPanchayatNameEn(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.panchayatMrLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={panchayatNameMr}
                onChange={(e) => setPanchayatNameMr(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.districtLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={districtName}
                onChange={(e) => setDistrictName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.tehsilLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                value={tehsilName}
                onChange={(e) => setTehsilName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <div className="grid grid-cols-2 gap-3">
              <label className="space-y-1 font-bold text-slate-300">
                <span>{t('agentPortal.pincodeLabel')}</span>
                <input
                  type="text"
                  maxLength={6}
                  value={pincode}
                  onChange={(e) => setPincode(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-300">
                <span>
                  {t('agentPortal.totalWardsLabel')} <span className="text-rose-400">*</span>
                </span>
                <input
                  type="number"
                  min={1}
                  max={50}
                  required
                  value={totalWards}
                  onChange={(e) => setTotalWards(Number(e.target.value))}
                  className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
                />
              </label>
            </div>
          </div>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-4">
          <h3 className="font-bold text-sky-400 uppercase tracking-wider">
            2. {t('superadmin.sectionAdmin')}
          </h3>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.adminFullNameLabel')}</span>
              <input
                type="text"
                value={gramsevakName}
                onChange={(e) => setGramsevakName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>
                {t('agentPortal.adminUsernameLabel')} <span className="text-rose-400">*</span>
              </span>
              <input
                type="text"
                required
                minLength={4}
                value={adminUsername}
                onChange={(e) => setAdminUsername(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.adminEmailLabel')}</span>
              <input
                type="email"
                value={adminEmail}
                onChange={(e) => setAdminEmail(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <div className="space-y-1">
              <div className="flex items-center justify-between gap-2">
                <span className="font-bold text-slate-300">
                  {t('agentPortal.adminPasswordLabel')} <span className="text-rose-400">*</span>
                </span>
                <button
                  type="button"
                  onClick={() => setAdminPassword(generateStrongPassword())}
                  className="inline-flex items-center gap-1 text-[10.5px] font-bold text-amber-300 hover:underline"
                >
                  <Wand2 className="h-3 w-3" />
                  {t('superadmin.generatePassBtn')}
                </button>
              </div>
              <div className="relative">
                <input
                  type={showPass ? 'text' : 'password'}
                  required
                  value={adminPassword}
                  onChange={(e) => setAdminPassword(e.target.value)}
                  className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 pr-8 mono text-white focus:border-emerald-500 focus:outline-none"
                />
                <button
                  type="button"
                  aria-label={showPass ? t('login.hidePassword') : t('login.showPassword')}
                  onClick={() => setShowPass((v) => !v)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white"
                >
                  {showPass ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                </button>
              </div>
              {!passwordCheck.isValid && (
                <p className="text-[10.5px] text-amber-300">{t('validation.passwordRules')}</p>
              )}
            </div>
          </div>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-4">
          <h3 className="font-bold text-amber-400 uppercase tracking-wider">
            3. {t('agentPortal.bankSetupTitle')}
          </h3>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.ayLabel')}</span>
              <input
                type="text"
                required
                pattern="\d{4}-\d{2}"
                value={assessmentYearLabel}
                onChange={(e) => setAssessmentYearLabel(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.upiIdLabel')}</span>
              <input
                type="text"
                value={upiId}
                onChange={(e) => setUpiId(e.target.value)}
                placeholder="gpname@sbi"
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.bankNameLabel')}</span>
              <input
                type="text"
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.branchNameLabel')}</span>
              <input
                type="text"
                value={branchName}
                onChange={(e) => setBranchName(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.accountNoLabel')}</span>
              <input
                type="text"
                value={accountNumber}
                onChange={(e) => setAccountNumber(e.target.value)}
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>

            <label className="space-y-1 font-bold text-slate-300">
              <span>{t('agentPortal.ifscLabel')}</span>
              <input
                type="text"
                value={ifscCode}
                onChange={(e) => setIfscCode(e.target.value)}
                placeholder="SBIN0001234"
                className="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 mono uppercase text-white focus:border-emerald-500 focus:outline-none"
              />
            </label>
          </div>

          <label className="flex items-center gap-2 pt-1 font-semibold text-slate-200">
            <input
              type="checkbox"
              checked={seedDefaultFormulas}
              onChange={(e) => setSeedDefaultFormulas(e.target.checked)}
              className="h-4 w-4 rounded border-slate-600 bg-slate-900 text-emerald-500"
            />
            <span>{t('agentPortal.seedFormulasToggle')}</span>
          </label>
        </div>

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={submitting}
            className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-6 py-2.5 text-xs font-extrabold text-white hover:bg-emerald-500 disabled:opacity-60 transition shadow"
          >
            <CheckCircle2 className="h-4 w-4" />
            {submitting ? t('agentPortal.submittingGpBtn') : t('agentPortal.submitGpBtn')}
          </button>
        </div>
      </form>
    </div>
  );
}

export function AgentVillagesTab({
  villages,
  isLoading,
  onJumpToAddGp,
}: {
  villages: AgentVillageItem[];
  isLoading: boolean;
  onJumpToAddGp: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('ALL');

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return villages.filter((v) => {
      if (statusFilter !== 'ALL' && v.effectiveStatus !== statusFilter && v.subscriptionStatus !== statusFilter) {
        return false;
      }
      if (!q) return true;
      return (
        v.lgdCode.includes(q) ||
        v.panchayatNameEn.toLowerCase().includes(q) ||
        v.panchayatNameMr.toLowerCase().includes(q) ||
        v.districtName.toLowerCase().includes(q) ||
        v.tehsilName.toLowerCase().includes(q)
      );
    });
  }, [villages, search, statusFilter]);

  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2.5 flex-1 min-w-[260px]">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-500" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('agentPortal.searchVillagesPlaceholder')}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 pl-9 pr-3 py-1.5 text-xs text-slate-100 placeholder:text-slate-500 focus:border-emerald-500"
            />
          </div>

          <select
            aria-label={t('agentPortal.allStatusFilter')}
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-xs text-slate-200"
          >
            <option value="ALL">{t('agentPortal.allStatusFilter')}</option>
            <option value="Active">{t('tenantStatus.Active')}</option>
            <option value="PendingApproval">{t('tenantStatus.PendingApproval')}</option>
            <option value="Trial">{t('tenantStatus.Trial')}</option>
            <option value="Expired">{t('tenantStatus.Expired')}</option>
          </select>
        </div>

        <button
          type="button"
          onClick={onJumpToAddGp}
          className="rounded-lg bg-emerald-600 px-3.5 py-1.5 text-xs font-bold text-white hover:bg-emerald-500"
        >
          {t('agentPortal.tabAddGp')}
        </button>
      </div>

      {isLoading ? (
        <div className="space-y-2">
          {[1, 2, 3].map((n) => (
            <div key={n} className="h-14 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-900 p-8 text-center text-xs text-slate-400">
          {t('agentPortal.noVillagesYet')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-950 text-slate-400">
              <tr>
                <th className="px-4 py-3">{t('agentPortal.colLgd')}</th>
                <th className="px-4 py-3">{t('agentPortal.colGpName')}</th>
                <th className="px-4 py-3">{t('agentPortal.colLocation')}</th>
                <th className="px-4 py-3">{t('agentPortal.colOnboardedDate')}</th>
                <th className="px-4 py-3">{t('agentPortal.colSubStatus')}</th>
                <th className="px-4 py-3">{t('agentPortal.colExpiresAt')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colSalesCollected')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colCommissionEarned')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colProjectedRenewal')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/70">
              {filtered.map((v) => (
                <tr key={v.tenantId} className="hover:bg-slate-800/40">
                  <td className="px-4 py-3 mono font-bold text-amber-300">{v.lgdCode}</td>
                  <td className="px-4 py-3">
                    <p className="font-bold text-white">{v.panchayatNameMr}</p>
                    <p className="text-[11px] text-slate-400">{v.panchayatNameEn}</p>
                  </td>
                  <td className="px-4 py-3 text-slate-300">
                    {v.districtName} · {v.tehsilName}
                  </td>
                  <td className="px-4 py-3 mono text-slate-400">
                    {formatDateOnly(v.onboardedAtUtc, i18n.language)}
                  </td>
                  <td className="px-4 py-3">
                    <TenantStatusBadge status={v.effectiveStatus || v.subscriptionStatus} dark />
                  </td>
                  <td className="px-4 py-3 mono text-slate-300">
                    <div>{formatDateOnly(v.subscriptionExpiresAtUtc, i18n.language)}</div>
                    {typeof v.daysUntilExpiry === 'number' && (
                      <span
                        className={`inline-block text-[10.5px] font-bold ${
                          v.daysUntilExpiry <= 0
                            ? 'text-rose-400'
                            : v.daysUntilExpiry <= 30
                              ? 'text-amber-300'
                              : 'text-slate-400'
                        }`}
                      >
                        {v.daysUntilExpiry <= 0
                          ? t('agentPortal.expiredBadge')
                          : t('agentPortal.daysLeftBadge', { days: v.daysUntilExpiry })}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right mono text-slate-200">
                    {formatInr(v.totalSalesCollected)}
                  </td>
                  <td className="px-4 py-3 text-right mono font-bold text-emerald-400">
                    {formatInr(v.totalCommissionEarned)}
                  </td>
                  <td className="px-4 py-3 text-right mono text-sky-300">
                    {formatInr(v.projectedRenewalCommission)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function AgentRenewalsTab({
  upcomingRenewals,
  approvedRenewalRatePct,
  isLoading,
}: {
  upcomingRenewals: AgentVillageItem[];
  approvedRenewalRatePct: number;
  isLoading: boolean;
}) {
  const { t, i18n } = useTranslation();

  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-800 bg-slate-900 p-5 space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-extrabold text-white">
            {t('agentPortal.renewalsNoticeTitle')}
          </h2>
          <span className="rounded-full border border-sky-500/40 bg-sky-500/15 px-3 py-0.5 mono text-xs font-bold text-sky-300">
            {t('agentPortal.renewalRatePill', { rate: approvedRenewalRatePct.toFixed(1) })}
          </span>
        </div>
        <p className="text-xs text-slate-400">
          {t('agentPortal.renewalsNoticeDesc', { rate: approvedRenewalRatePct.toFixed(1) })}
        </p>
      </div>

      {isLoading ? (
        <div className="space-y-2">
          {[1, 2].map((n) => (
            <div key={n} className="h-14 animate-pulse rounded-xl bg-slate-800" />
          ))}
        </div>
      ) : upcomingRenewals.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-800 bg-slate-900 p-8 text-center text-xs text-slate-400">
          {t('agentPortal.noUpcomingRenewals')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-950 text-slate-400">
              <tr>
                <th className="px-4 py-3">{t('agentPortal.colLgd')}</th>
                <th className="px-4 py-3">{t('agentPortal.colGpName')}</th>
                <th className="px-4 py-3">{t('agentPortal.colLocation')}</th>
                <th className="px-4 py-3">{t('agentPortal.colSubStatus')}</th>
                <th className="px-4 py-3">{t('agentPortal.colExpiresAt')}</th>
                <th className="px-4 py-3">{t('agentPortal.colDaysLeft')}</th>
                <th className="px-4 py-3 text-right">{t('agentPortal.colProjectedRenewal')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/70">
              {upcomingRenewals.map((v) => (
                <tr key={v.tenantId} className="hover:bg-slate-800/40">
                  <td className="px-4 py-3 mono font-bold text-amber-300">{v.lgdCode}</td>
                  <td className="px-4 py-3">
                    <p className="font-bold text-white">{v.panchayatNameMr}</p>
                    <p className="text-[11px] text-slate-400">{v.panchayatNameEn}</p>
                  </td>
                  <td className="px-4 py-3 text-slate-300">
                    {v.districtName} · {v.tehsilName}
                  </td>
                  <td className="px-4 py-3">
                    <TenantStatusBadge status={v.effectiveStatus || v.subscriptionStatus} dark />
                  </td>
                  <td className="px-4 py-3 mono text-slate-300">
                    {formatDateOnly(v.subscriptionExpiresAtUtc, i18n.language)}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex rounded-full px-2.5 py-0.5 mono text-[11px] font-bold ${
                        (v.daysUntilExpiry ?? 0) <= 0
                          ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40'
                          : 'bg-amber-500/20 text-amber-300 border border-amber-500/40'
                      }`}
                    >
                      {(v.daysUntilExpiry ?? 0) <= 0
                        ? t('agentPortal.expiredBadge')
                        : t('agentPortal.daysLeftBadge', { days: v.daysUntilExpiry })}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right mono font-bold text-emerald-400">
                    {formatInr(v.projectedRenewalCommission)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

