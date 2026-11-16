import { ArrowLeft, CalendarClock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

interface PlannedModulePageProps {
  titleKey: string;
  sprintNumber: number;
}

export function PlannedModulePage({ titleKey, sprintNumber }: PlannedModulePageProps) {
  const { t } = useTranslation();
  const sprintLabel = t('dashboard.sprintBadge', { n: sprintNumber });

  return (
    <div className="mx-auto max-w-lg rounded-2xl border border-line bg-card p-7 shadow-xs text-center space-y-4">
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-700">
        <CalendarClock className="h-6 w-6" />
      </div>
      <div className="space-y-1">
        <span className="inline-block rounded-full bg-brand-50 border border-brand-200 px-3 py-0.5 text-[11px] font-bold text-brand-800">
          {sprintLabel}
        </span>
        <h1 className="text-lg font-extrabold text-ink">{t(titleKey)}</h1>
      </div>
      <p className="text-xs text-muted">
        {t('dashboard.scheduledModuleNotice', { sprint: sprintLabel })}
      </p>
      <Link
        to="/dashboard"
        className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-xs font-bold text-white hover:bg-brand-800 transition"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('notFound.back')}
      </Link>
    </div>
  );
}
