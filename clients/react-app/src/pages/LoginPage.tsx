import { Shield } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

const CANONICAL_ROLES = [
  'SuperAdmin',
  'Gramsevak',
  'GP_Admin',
  'GP_Operator',
  'Surveyor',
  'Citizen',
  'Auditor',
  'Support_Admin',
  'Field_Agent',
];

export function LoginPage() {
  const { t } = useTranslation();
  const [role, setRole] = useState('Gramsevak');
  const [submittedNote, setSubmittedNote] = useState(false);

  return (
    <div className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="flex items-center gap-2 text-emerald-700">
        <Shield className="h-5 w-5" />
        <h1 className="text-lg font-bold text-slate-900">{t('login.title')}</h1>
      </div>
      <p className="mt-2 rounded-lg bg-amber-50 p-3 text-xs text-amber-900">{t('login.note')}</p>

      <form
        className="mt-4 space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          setSubmittedNote(true);
        }}
      >
        <label className="block text-xs font-semibold text-slate-700">
          {t('login.roleLabel')}
          <select
            value={role}
            onChange={(e) => setRole(e.target.value)}
            className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
          >
            {CANONICAL_ROLES.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-xs font-semibold text-slate-700">
          {t('login.email')}
          <input
            type="text"
            placeholder="gramsevak.gp"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
        </label>

        <label className="block text-xs font-semibold text-slate-700">
          {t('login.password')}
          <input
            type="password"
            placeholder="••••••••"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
        </label>

        <button
          type="submit"
          className="w-full rounded-lg bg-emerald-700 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-800"
        >
          {t('login.submit')}
        </button>

        {submittedNote && (
          <p className="text-center text-xs text-emerald-700">
            Selected role: <code>{role}</code> (JWT endpoint scheduled in Sprint 1 Issue #7).
          </p>
        )}
      </form>
    </div>
  );
}

