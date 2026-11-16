import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

export function NotFoundPage() {
  const { t } = useTranslation();

  return (
    <div className="mx-auto max-w-md rounded-2xl border border-line bg-card p-8 text-center shadow-xs space-y-3">
      <h1 className="text-xl font-extrabold text-ink">{t('notFound.title')}</h1>
      <p className="text-xs text-muted">{t('notFound.desc')}</p>
      <Link
        to="/"
        className="inline-block rounded-xl bg-brand-700 px-5 py-2.5 text-xs font-bold text-white hover:bg-brand-800 transition"
      >
        {t('notFound.back')}
      </Link>
    </div>
  );
}
