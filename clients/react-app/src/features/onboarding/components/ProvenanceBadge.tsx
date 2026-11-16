import { Pencil, RotateCcw, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { type TrackedGeoField, useOnboardingWizard } from '../context/useOnboardingWizard';

interface ProvenanceBadgeProps {
  field: TrackedGeoField;
}

export function ProvenanceBadge({ field }: ProvenanceBadgeProps) {
  const { t } = useTranslation();
  const { getFieldProvenance, restoreOriginalField } = useOnboardingWizard();
  const status = getFieldProvenance(field);

  if (status === 'manual') {
    return null;
  }

  if (status === 'autofilled') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-brand-100 px-2 py-0.5 text-[10.5px] font-bold text-brand-800">
        <Zap className="h-3 w-3" />
        {t('onboarding.badgeAuto')}
      </span>
    );
  }

  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-[10.5px] font-bold text-amber-900">
        <Pencil className="h-3 w-3" />
        {t('onboarding.badgeEdited')}
      </span>
      <button
        type="button"
        onClick={() => restoreOriginalField(field)}
        className="inline-flex items-center gap-1 rounded-md border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10.5px] font-semibold text-amber-900 hover:bg-amber-100"
      >
        <RotateCcw className="h-3 w-3" />
        {t('onboarding.restoreOriginal')}
      </button>
    </span>
  );
}
