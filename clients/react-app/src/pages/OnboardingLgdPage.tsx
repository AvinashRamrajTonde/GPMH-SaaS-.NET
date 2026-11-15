import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Search, ShieldAlert } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getDistricts,
  getPanchayatByLgd,
  getPanchayats,
  getTehsils,
  type LgdPanchayatSummary,
} from '../services/lgdApi';

export function OnboardingLgdPage() {
  const { t } = useTranslation();

  const [lgdInput, setLgdInput] = useState('171391');
  const [selectedDistrict, setSelectedDistrict] = useState('');
  const [selectedTehsil, setSelectedTehsil] = useState('');
  const [selectedLgdFromCascade, setSelectedLgdFromCascade] = useState('');
  const [allowEdits, setAllowEdits] = useState(false);

  const [lookupResult, setLookupResult] = useState<LgdPanchayatSummary | null>(null);
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [searching, setSearching] = useState(false);

  const districtsQuery = useQuery({
    queryKey: ['lgd', 'districts'],
    queryFn: getDistricts,
    staleTime: 1000 * 60 * 30,
  });

  const tehsilsQuery = useQuery({
    queryKey: ['lgd', 'tehsils', selectedDistrict],
    queryFn: () => getTehsils(selectedDistrict),
    enabled: Boolean(selectedDistrict),
    staleTime: 1000 * 60 * 30,
  });

  const panchayatsQuery = useQuery({
    queryKey: ['lgd', 'panchayats', selectedDistrict, selectedTehsil],
    queryFn: () => getPanchayats(selectedDistrict, selectedTehsil),
    enabled: Boolean(selectedDistrict && selectedTehsil),
    staleTime: 1000 * 60 * 30,
  });

  async function handleVerifyLgd() {
    if (!lgdInput.trim()) return;
    setSearching(true);
    setLookupError(null);
    try {
      const gp = await getPanchayatByLgd(lgdInput);
      setLookupResult(gp);
    } catch (err) {
      setLookupResult(null);
      setLookupError(err instanceof Error ? err.message : t('onboarding.notFound'));
    } finally {
      setSearching(false);
    }
  }

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-xl font-bold text-slate-900">{t('onboarding.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t('onboarding.subtitle')}</p>

        <div className="mt-5 grid gap-6 lg:grid-cols-2">
          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4">
            <label className="block text-xs font-semibold uppercase tracking-wide text-slate-600">
              {t('onboarding.byCode')}
            </label>
            <div className="mt-2 flex gap-2">
              <input
                type="text"
                value={lgdInput}
                onChange={(e) => setLgdInput(e.target.value)}
                placeholder={t('onboarding.codePlaceholder')}
                className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
              />
              <button
                type="button"
                onClick={() => void handleVerifyLgd()}
                disabled={searching}
                className="inline-flex items-center gap-2 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 disabled:opacity-60"
              >
                <Search className="h-4 w-4" />
                {searching ? t('onboarding.loading') : t('onboarding.verifyBtn')}
              </button>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-600">
              {t('onboarding.orCascade')}
            </p>
            <div className="grid gap-3 sm:grid-cols-3">
              <select
                aria-label={t('onboarding.district')}
                value={selectedDistrict}
                onChange={(e) => {
                  setSelectedDistrict(e.target.value);
                  setSelectedTehsil('');
                  setSelectedLgdFromCascade('');
                }}
                className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm"
              >
                <option value="">{t('onboarding.selectOption')}</option>
                {districtsQuery.data?.map((d) => (
                  <option key={d.district} value={d.district}>
                    {d.district} ({d.gp_count})
                  </option>
                ))}
              </select>

              <select
                aria-label={t('onboarding.tehsil')}
                value={selectedTehsil}
                disabled={!selectedDistrict}
                onChange={(e) => {
                  setSelectedTehsil(e.target.value);
                  setSelectedLgdFromCascade('');
                }}
                className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm disabled:opacity-50"
              >
                <option value="">{t('onboarding.selectOption')}</option>
                {tehsilsQuery.data?.map((th) => (
                  <option key={th.tehsil} value={th.tehsil}>
                    {th.tehsil} ({th.gp_count})
                  </option>
                ))}
              </select>

              <select
                aria-label={t('onboarding.panchayat')}
                value={selectedLgdFromCascade}
                disabled={!selectedTehsil}
                onChange={(e) => {
                  const code = e.target.value;
                  setSelectedLgdFromCascade(code);
                  const found = panchayatsQuery.data?.find((p) => p.lgd_code === code) ?? null;
                  setLookupResult(found);
                  setLookupError(null);
                  if (found) setLgdInput(found.lgd_code);
                }}
                className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm disabled:opacity-50"
              >
                <option value="">{t('onboarding.selectOption')}</option>
                {panchayatsQuery.data?.map((gp) => (
                  <option key={gp.lgd_code} value={gp.lgd_code}>
                    {gp.gp_name_mr} ({gp.lgd_code})
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        <label className="mt-4 inline-flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={allowEdits}
            onChange={(e) => setAllowEdits(e.target.checked)}
            className="h-4 w-4 rounded border-slate-300 text-emerald-700"
          />
          {t('onboarding.allowEdits')}
        </label>

        {lookupError && (
          <div className="mt-4 flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
            <ShieldAlert className="h-4 w-4 shrink-0" />
            <span>{lookupError}</span>
          </div>
        )}

        {lookupResult && (
          <div className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50/60 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2 text-emerald-800 font-semibold">
                <CheckCircle2 className="h-5 w-5" />
                <span>
                  LGD #{lookupResult.lgd_code} — {lookupResult.gp_name_mr} ({lookupResult.gp_name_en})
                </span>
              </div>
              {allowEdits && (
                <span className="rounded-full bg-amber-100 px-3 py-0.5 text-xs font-semibold text-amber-800">
                  Manual Override Enabled (Audited)
                </span>
              )}
            </div>
            <p className="mt-2 text-sm text-slate-700">
              {t('onboarding.district')}: <strong>{lookupResult.district}</strong> ·{' '}
              {t('onboarding.tehsil')}: <strong>{lookupResult.tehsil}</strong> · PIN:{' '}
              <strong>{lookupResult.pincode || 'N/A'}</strong>
            </p>
          </div>
        )}
      </section>
    </div>
  );
}

