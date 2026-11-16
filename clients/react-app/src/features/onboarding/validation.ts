import type { TFunction } from 'i18next';
import type { OnboardingWizardState } from './context/useOnboardingWizard';
import { evaluatePasswordStrength } from '../../utils/validation';

const IFSC_REGEX = /^[A-Z]{4}0[A-Z0-9]{6}$/;
const MOBILE_REGEX = /^\d{10}$/;
const UPI_REGEX = /^[\w.-]+@[\w.-]+$/;

export function resolveStepForFieldKey(key: string): number {
  const k = key.toLowerCase();
  if (
    k.includes('gramsevak') ||
    k.includes('sarpanch') ||
    k.includes('operator') ||
    k.includes('stamp')
  ) {
    return 2;
  }
  if (k.includes('assessment')) {
    return 3;
  }
  if (k.includes('bankaccounts') || k.includes('upi')) {
    return 4;
  }
  if (k.includes('admin') || k.includes('formula')) {
    return 5;
  }
  return 1;
}

export function validateOnboardingStep(
  step: number,
  state: OnboardingWizardState,
  isSelfService: boolean,
  t: TFunction,
): Record<string, string> {
  const errors: Record<string, string> = {};

  if (step === 1) {
    if (!/^\d{5,8}$/.test(state.lgdCode.trim())) {
      errors.lgdCode = t('validation.lgdDigits');
    }
    if (!state.panchayatNameMr.trim()) {
      errors.panchayatNameMr = t('validation.required');
    }
    if (!state.panchayatNameEn.trim()) {
      errors.panchayatNameEn = t('validation.required');
    }
    if (!state.districtName.trim()) {
      errors.districtName = t('validation.required');
    }
    if (!state.tehsilName.trim()) {
      errors.tehsilName = t('validation.required');
    }
    if (state.totalWards < 1 || state.totalWards > 50 || Number.isNaN(state.totalWards)) {
      errors.totalWards = t('validation.wardsRange');
    }
    if (state.pincode.trim() && !/^\d{6}$/.test(state.pincode.trim())) {
      errors.pincode = t('validation.pincodeDigits');
    }
  }

  if (step === 2) {
    if (state.gramsevakMobile.trim() && !MOBILE_REGEX.test(state.gramsevakMobile.trim())) {
      errors.gramsevakMobile = t('validation.mobileDigits');
    }
    if (state.sarpanchMobile.trim() && !MOBILE_REGEX.test(state.sarpanchMobile.trim())) {
      errors.sarpanchMobile = t('validation.mobileDigits');
    }
    if (state.operatorMobile.trim() && !MOBILE_REGEX.test(state.operatorMobile.trim())) {
      errors.operatorMobile = t('validation.mobileDigits');
    }
  }

  if (step === 3) {
    if (!/^\d{4}-\d{2}$/.test(state.assessmentYearLabel.trim())) {
      errors.assessmentYearLabel = t('validation.ayFormat');
    }
  }

  if (step === 4) {
    state.bankAccounts.forEach((acc, idx) => {
      const hasAnyDetail =
        acc.branchName.trim().length > 0 ||
        acc.accountNumber.trim().length > 0 ||
        acc.ifscCode.trim().length > 0;
      if (!hasAnyDetail) return;

      if (!acc.bankName.trim()) {
        errors[`bankAccounts.${idx}.bankName`] = t('validation.required');
      }
      if (!acc.branchName.trim()) {
        errors[`bankAccounts.${idx}.branchName`] = t('validation.branchRequired');
      }
      if (!acc.accountNumber.trim()) {
        errors[`bankAccounts.${idx}.accountNumber`] = t('validation.accountRequired');
      }
      const ifsc = acc.ifscCode.trim().toUpperCase();
      if (!ifsc) {
        errors[`bankAccounts.${idx}.ifscCode`] = t('validation.ifscRequired');
      } else if (!IFSC_REGEX.test(ifsc)) {
        errors[`bankAccounts.${idx}.ifscCode`] = t('validation.ifscFormat');
      }
    });

    if (state.primaryUpiId.trim() && !UPI_REGEX.test(state.primaryUpiId.trim())) {
      errors.primaryUpiId = t('validation.upiFormat');
    }
  }

  if (step === 5 && isSelfService) {
    const uname = state.adminUsername.trim();
    if (uname.length < 4 || uname.length > 120) {
      errors.adminUsername = t('validation.usernameMin');
    }
    if (state.adminEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(state.adminEmail.trim())) {
      errors.adminEmail = t('validation.emailFormat');
    }
    const strength = evaluatePasswordStrength(state.adminPassword);
    if (!strength.isValid) {
      errors.adminPassword = t('validation.passwordRules');
    }
    if (state.adminPassword !== state.adminConfirmPassword) {
      errors.adminConfirmPassword = t('onboarding.passwordMismatch');
    }
  }

  return errors;
}

export function validateAllOnboardingSteps(
  state: OnboardingWizardState,
  isSelfService: boolean,
  t: TFunction,
): { firstInvalidStep: number | null; fieldErrors: Record<string, string>; stepHasError: Record<number, boolean> } {
  const combined: Record<string, string> = {};
  const stepHasError: Record<number, boolean> = {};
  let firstInvalidStep: number | null = null;

  for (let s = 1; s <= 5; s++) {
    const stepErrs = validateOnboardingStep(s, state, isSelfService, t);
    const hasErr = Object.keys(stepErrs).length > 0;
    stepHasError[s] = hasErr;
    if (hasErr && firstInvalidStep === null) {
      firstInvalidStep = s;
    }
    Object.assign(combined, stepErrs);
  }

  return { firstInvalidStep, fieldErrors: combined, stepHasError };
}
