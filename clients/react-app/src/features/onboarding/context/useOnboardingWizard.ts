import { createContext, useContext } from 'react';
import type { LgdPanchayatSummary } from '../../../services/lgdApi';
import type { BankAccountPayload, CompleteOnboardingPayload } from '../../../services/sprint2Api';

export type TrackedGeoField =
  | 'lgdCode'
  | 'panchayatNameEn'
  | 'panchayatNameMr'
  | 'districtName'
  | 'tehsilName'
  | 'pincode';

export interface OnboardingWizardState {
  step: number;
  lgdCode: string;
  districtCode: string;
  districtName: string;
  tehsilCode: string;
  tehsilName: string;
  panchayatNameEn: string;
  panchayatNameMr: string;
  pincode: string;
  censusCode: string;
  officeAddress: string;
  totalWards: number;
  populationCensus: string;
  sarpanchName: string;
  sarpanchMobile: string;
  upSarpanchName: string;
  gramsevakName: string;
  gramsevakMobile: string;
  operatorName: string;
  operatorMobile: string;
  gramsevakSignatureKey: string | null;
  sarpanchSignatureKey: string | null;
  operatorSignatureKey: string | null;
  officialStampKey: string | null;
  assessmentYearLabel: string;
  assessmentStartYear: number;
  assessmentEndYear: number;
  lockAssessmentYear: boolean;
  initializeArrearsRollover: boolean;
  primaryUpiId: string;
  upiPayeeName: string;
  bankAccounts: BankAccountPayload[];
  seedDefaultFormulas: boolean;
  activateTenant: boolean;
  adminUsername: string;
  adminPassword: string;
  adminEmail: string;
}

export type LgdSnapshotMap = Partial<Record<TrackedGeoField, string>>;

export interface OnboardingContextValue {
  state: OnboardingWizardState;
  lgdSnapshot: LgdSnapshotMap | null;
  editedFields: string[];
  setStep: (step: number) => void;
  updateField: <K extends keyof OnboardingWizardState>(key: K, value: OnboardingWizardState[K]) => void;
  updateBankAccount: (index: number, patch: Partial<BankAccountPayload>) => void;
  applyLgdAutoFill: (gp: LgdPanchayatSummary & { district_code?: string | null; tehsil_code?: string | null }) => void;
  restoreOriginalField: (field: TrackedGeoField) => void;
  getFieldProvenance: (field: TrackedGeoField) => 'autofilled' | 'edited' | 'manual';
  buildCompletePayload: () => CompleteOnboardingPayload;
}

export const OnboardingContext = createContext<OnboardingContextValue | null>(null);

export function useOnboardingWizard(): OnboardingContextValue {
  const ctx = useContext(OnboardingContext);
  if (!ctx) {
    throw new Error('useOnboardingWizard must be used within OnboardingProvider');
  }
  return ctx;
}

