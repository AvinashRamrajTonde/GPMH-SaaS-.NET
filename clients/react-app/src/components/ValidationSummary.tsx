import { ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { ParsedValidationItem } from '../utils/validation';

interface ValidationSummaryProps {
  summary: string | null;
  items?: ParsedValidationItem[];
  resolveStep?: (key: string) => number | null;
  currentStep?: number;
  onJumpToStep?: (step: number) => void;
  showLoginHint?: boolean;
  dark?: boolean;
}

export function ValidationSummary({
  summary,
  items = [],
  resolveStep,
  currentStep,
  onJumpToStep,
  showLoginHint = false,
  dark = false,
}: ValidationSummaryProps) {
  const { t } = useTranslation();

  if (!summary && items.length === 0) return null;

  return (
    <div
      role="alert"
      className={`rounded-xl border px-4 py-3 text-xs space-y-2 ${
        dark
          ? 'border-rose-500/40 bg-rose-950/60 text-rose-200'
          : 'border-rose-300 bg-rose-50 text-rose-900'
      }`}
    >
      <div className="flex items-start gap-2 font-bold">
        <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
        <div className="space-y-0.5">
          <p>{items.length > 0 ? t('validation.summaryTitle') : summary}</p>
          {items.length > 0 && summary && (
            <p className={`text-[11px] font-medium ${dark ? 'text-rose-300/80' : 'text-rose-700'}`}>
              {summary}
            </p>
          )}
        </div>
      </div>

      {items.length > 0 && (
        <ul className="space-y-1.5 pl-6 list-disc">
          {items.map((item, idx) => {
            const targetStep = resolveStep ? resolveStep(item.key) : null;
            return (
              <li key={`${item.key}-${idx}`} className="leading-snug">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="mono rounded bg-rose-500/15 px-1.5 py-0.5 text-[10.5px] font-bold">
                    {item.key}
                  </span>
                  <span>{item.message}</span>
                  {targetStep &&
                    onJumpToStep &&
                    currentStep !== undefined &&
                    targetStep !== currentStep && (
                      <button
                        type="button"
                        onClick={() => onJumpToStep(targetStep)}
                        className={`rounded-md border px-2 py-0.5 text-[10.5px] font-bold transition ${
                          dark
                            ? 'border-rose-400/50 bg-rose-900/50 text-white hover:bg-rose-800'
                            : 'border-rose-300 bg-white text-rose-800 hover:bg-rose-100'
                        }`}
                      >
                        {t('validation.goToStep', { n: targetStep })} →
                      </button>
                    )}
                </div>
              </li>
            );
          })}
        </ul>
      )}

      {showLoginHint && (
        <div
          className={`flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-[11px] ${
            dark
              ? 'border-amber-500/30 bg-amber-950/40 text-amber-200'
              : 'border-amber-200 bg-amber-50 text-amber-950'
          }`}
        >
          <span>{t('validation.alreadyRegisteredHint')}</span>
          <Link
            to="/login"
            className="rounded-md bg-brand-700 px-2.5 py-1 font-bold text-white hover:bg-brand-800"
          >
            {t('nav.login')} →
          </Link>
        </div>
      )}
    </div>
  );
}

