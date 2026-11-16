import { ArrowLeft, CalendarClock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

interface PlannedModulePageProps {
  titleKey: string;
  targetSprint: string;
}

export function PlannedModulePage({ titleKey, targetSprint }: PlannedModulePageProps) {
  const { t } = useTranslation();

  return (
    <div className="mx-auto max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-sm text-center space-y-4">
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-700">
        <CalendarClock className="h-6 w-6" />
      </div>
      <h1 className="text-lg font-bold text-slate-900">{t(titleKey)}</h1>
      <p className="text-xs text-slate-600">
        {t('dashboard.scheduledModuleNotice', { sprint: targetSprint })}
      </p>
      <Link
        to="/dashboard"
        className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-700 px-4 py-2 text-xs font-bold text-white hover:bg-emerald-800"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('notFound.back')}
      </Link>
    </div>
  );
}

