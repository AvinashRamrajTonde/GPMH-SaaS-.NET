import { useCallback, useMemo, useState, type PropsWithChildren } from 'react';
import type { LgdPanchayatSummary } from '../../../services/lgdApi';
import type {
  BankAccountPayload,
  CompleteOnboardingPayload,
  LgdProvenance,
} from '../../../services/sprint2Api';
import {
  type LgdSnapshotMap,
  OnboardingContext,
  type OnboardingWizardState,
  type TrackedGeoField,
} from './useOnboardingWizard';

const DEFAULT_BANKS: BankAccountPayload[] = [
  {
    fundType: 'GRAM_NIDHI',
    bankName: 'State Bank of India',
    branchName: '',
    accountNumber: '',
    ifscCode: '',
    upiId: '',
    upiPayeeName: '',
    qrImageKey: null,
    openingBalance: 0,
  },
  {
    fundType: 'XV_FINANCE',
    bankName: 'Bank of Maharashtra',
    branchName: '',
    accountNumber: '',
    ifscCode: '',
    upiId: '',
    upiPayeeName: '',
    qrImageKey: null,
    openingBalance: 0,
  },
  {
    fundType: 'WATER_SUPPLY',
    bankName: 'District Central Co-op Bank',
    branchName: '',
    accountNumber: '',
    ifscCode: '',
    upiId: '',
    upiPayeeName: '',
    qrImageKey: null,
    openingBalance: 0,
  },
];

const INITIAL_STATE: OnboardingWizardState = {
  step: 1,
  lgdCode: '',
  districtCode: '27',
  districtName: '',
  tehsilCode: '',
  tehsilName: '',
  panchayatNameEn: '',
  panchayatNameMr: '',
  pincode: '',
  censusCode: '',
  officeAddress: '',
  totalWards: 5,
  populationCensus: '',
  sarpanchName: '',
  sarpanchMobile: '',
  upSarpanchName: '',
  gramsevakName: '',
  gramsevakMobile: '',
  operatorName: '',
  operatorMobile: '',
  gramsevakSignatureKey: null,
  sarpanchSignatureKey: null,
  operatorSignatureKey: null,
  officialStampKey: null,
  assessmentYearLabel: '2026-27',
  assessmentStartYear: 2026,
  assessmentEndYear: 2027,
  lockAssessmentYear: true,
  initializeArrearsRollover: true,
  primaryUpiId: '',
  upiPayeeName: '',
  bankAccounts: DEFAULT_BANKS,
  seedDefaultFormulas: true,
  activateTenant: true,
  adminUsername: '',
  adminPassword: '',
  adminEmail: '',
};

export function OnboardingProvider({ children }: PropsWithChildren) {
  const [state, setState] = useState<OnboardingWizardState>(INITIAL_STATE);
  const [lgdSnapshot, setLgdSnapshot] = useState<LgdSnapshotMap | null>(null);
  const [extraEditedFields, setExtraEditedFields] = useState<string[]>([]);

  const setStep = useCallback((step: number) => {
    setState((prev) => ({ ...prev, step: Math.min(5, Math.max(1, step)) }));
  }, []);

  const updateField = useCallback(
    <K extends keyof OnboardingWizardState>(key: K, value: OnboardingWizardState[K]) => {
      setState((prev) => ({ ...prev, [key]: value }));
      if (key === 'officeAddress' || key === 'gramsevakMobile' || key === 'sarpanchMobile' || key === 'censusCode') {
        setExtraEditedFields((prev) => (prev.includes(key) ? prev : [...prev, key]));
      }
    },
    [],
  );

  const updateBankAccount = useCallback((index: number, patch: Partial<BankAccountPayload>) => {
    setState((prev) => ({
      ...prev,
      bankAccounts: prev.bankAccounts.map((acc, i) => (i === index ? { ...acc, ...patch } : acc)),
    }));
  }, []);

  const applyLgdAutoFill = useCallback(
    (gp: LgdPanchayatSummary & { district_code?: string | null; tehsil_code?: string | null }) => {
      const snapshot: LgdSnapshotMap = {
        lgdCode: gp.lgd_code,
        panchayatNameEn: gp.gp_name_en,
        panchayatNameMr: gp.gp_name_mr,
        districtName: gp.district,
        tehsilName: gp.tehsil,
        pincode: gp.pincode || '',
      };
      setLgdSnapshot(snapshot);
      setExtraEditedFields([]);
      setState((prev) => ({
        ...prev,
        lgdCode: gp.lgd_code,
        districtCode: gp.district_code || prev.districtCode || '27',
        districtName: gp.district,
        tehsilCode: gp.tehsil_code || prev.tehsilCode || '000',
        tehsilName: gp.tehsil,
        panchayatNameEn: gp.gp_name_en,
        panchayatNameMr: gp.gp_name_mr,
        pincode: gp.pincode || '',
        upiPayeeName: prev.upiPayeeName || `Gram Panchayat ${gp.gp_name_en}`,
      }));
    },
    [],
  );

  const restoreOriginalField = useCallback(
    (field: TrackedGeoField) => {
      if (!lgdSnapshot || lgdSnapshot[field] === undefined) return;
      const original = lgdSnapshot[field] ?? '';
      setState((prev) => ({ ...prev, [field]: original }));
    },
    [lgdSnapshot],
  );

  const getFieldProvenance = useCallback(
    (field: TrackedGeoField): 'autofilled' | 'edited' | 'manual' => {
      if (!lgdSnapshot) return 'manual';
      const original = lgdSnapshot[field] ?? '';
      const current = String(state[field] ?? '');
      return current.trim() === original.trim() ? 'autofilled' : 'edited';
    },
    [lgdSnapshot, state],
  );

  const editedFields = useMemo(() => {
    const tracked: TrackedGeoField[] = [
      'lgdCode',
      'panchayatNameEn',
      'panchayatNameMr',
      'districtName',
      'tehsilName',
      'pincode',
    ];
    const modifiedGeo = lgdSnapshot
      ? tracked.filter((f) => String(state[f] ?? '').trim() !== (lgdSnapshot[f] ?? '').trim())
      : [];
    return Array.from(new Set([...modifiedGeo, ...extraEditedFields]));
  }, [lgdSnapshot, state, extraEditedFields]);

  const buildCompletePayload = useCallback((): CompleteOnboardingPayload => {
    const filledBanks = state.bankAccounts.filter(
      (b) => b.accountNumber.trim().length > 0 && b.ifscCode.trim().length > 0,
    );
    const provenance: LgdProvenance = {
      isAutofilledFromLgd: lgdSnapshot !== null,
      isEditedOnOnboarding: editedFields.length > 0,
      editedFields,
      originalLgdSnapshotJson: lgdSnapshot ? JSON.stringify(lgdSnapshot) : null,
    };

    return {
      lgdCode: state.lgdCode.trim(),
      districtCode: state.districtCode.trim() || '27',
      districtName: state.districtName.trim(),
      tehsilCode: state.tehsilCode.trim() || '000',
      tehsilName: state.tehsilName.trim(),
      panchayatNameEn: state.panchayatNameEn.trim(),
      panchayatNameMr: state.panchayatNameMr.trim(),
      pincode: state.pincode.trim() || null,
      censusCode: state.censusCode.trim() || null,
      officeAddress: state.officeAddress.trim() || null,
      totalWards: Number(state.totalWards) || 5,
      populationCensus: state.populationCensus ? Number(state.populationCensus) : null,
      sarpanchName: state.sarpanchName.trim() || null,
      sarpanchMobile: state.sarpanchMobile.trim() || null,
      upSarpanchName: state.upSarpanchName.trim() || null,
      gramsevakName: state.gramsevakName.trim() || null,
      gramsevakMobile: state.gramsevakMobile.trim() || null,
      operatorName: state.operatorName.trim() || null,
      operatorMobile: state.operatorMobile.trim() || null,
      gramsevakSignatureKey: state.gramsevakSignatureKey,
      sarpanchSignatureKey: state.sarpanchSignatureKey,
      operatorSignatureKey: state.operatorSignatureKey,
      officialStampKey: state.officialStampKey,
      assessmentYearLabel: state.assessmentYearLabel,
      assessmentStartYear: state.assessmentStartYear,
      assessmentEndYear: state.assessmentEndYear,
      lockAssessmentYear: state.lockAssessmentYear,
      initializeArrearsRollover: state.initializeArrearsRollover,
      primaryUpiId: state.primaryUpiId.trim() || null,
      upiPayeeName: state.upiPayeeName.trim() || null,
      bankAccounts: filledBanks,
      seedDefaultFormulas: state.seedDefaultFormulas,
      activateTenant: state.activateTenant,
      provenance,
    };
  }, [state, lgdSnapshot, editedFields]);

  return (
    <OnboardingContext.Provider
      value={{
        state,
        lgdSnapshot,
        editedFields,
        setStep,
        updateField,
        updateBankAccount,
        applyLgdAutoFill,
        restoreOriginalField,
        getFieldProvenance,
        buildCompletePayload,
      }}
    >
      {children}
    </OnboardingContext.Provider>
  );
}
