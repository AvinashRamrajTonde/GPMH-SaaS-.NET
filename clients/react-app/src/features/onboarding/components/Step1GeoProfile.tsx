import { useQuery } from '@tanstack/react-query';
import { MapPin, Search, ShieldAlert } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getDistricts,
  getPanchayatByLgd,
  getPanchayats,
  getTehsils,
} from '../../../services/lgdApi';
import { useOnboardingWizard } from '../context/useOnboardingWizard';
import { ProvenanceBadge } from './ProvenanceBadge';

type DiscoveryMode = 'code' | 'cascade';

export function Step1GeoProfile() {
  const { t } = useTranslation();
  const { state, updateField, applyLgdAutoFill } = useOnboardingWizard();

  const [mode, setMode] = useState<DiscoveryMode>('code');
  const [debouncedInput, setDebouncedInput] = useState('');
  const [selectedDistrict, setSelectedDistrict] = useState('');
  const [selectedTehsil, setSelectedTehsil] = useState('');
  const [pincodeFilter, setPincodeFilter] = useState('');
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [isLookingUp, setIsLookingUp] = useState(false);

  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedInput(state.lgdCode.trim());
    }, 400);
    return () => clearTimeout(handle);
  }, [state.lgdCode]);

  useEffect(() => {
    if (mode !== 'code' || !/^\d{6}$/.test(debouncedInput)) return;

    let cancelled = false;
    async function runLookup() {
      setIsLookingUp(true);
      setLookupError(null);
      try {
        const detail = await getPanchayatByLgd(debouncedInput);
        if (!cancelled) {
          applyLgdAutoFill(detail);
        }
      } catch (err) {
        if (!cancelled) {
          setLookupError(err instanceof Error ? err.message : t('onboarding.notFound'));
        }
      } finally {
        if (!cancelled) {
          setIsLookingUp(false);
        }
      }
    }
    void runLookup();
    return () => {
      cancelled = true;
    };
  }, [mode, debouncedInput, applyLgdAutoFill, t]);

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

  const filteredPanchayats = useMemo(() => {
    const items = panchayatsQuery.data ?? [];
    if (!pincodeFilter.trim()) return items;
    return items.filter((gp) => (gp.pincode ?? '').includes(pincodeFilter.trim()));
  }, [panchayatsQuery.data, pincodeFilter]);

  async function handleManualVerify() {
    if (!state.lgdCode.trim()) return;
    setIsLookingUp(true);
    setLookupError(null);
    try {
      const gp = await getPanchayatByLgd(state.lgdCode);
      applyLgdAutoFill(gp);
    } catch (err) {
      setLookupError(err instanceof Error ? err.message : t('onboarding.notFound'));
    } finally {
      setIsLookingUp(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap gap-2 rounded-xl border border-slate-200 bg-slate-100 p-1.5">
        <button
          type="button"
          onClick={() => setMode('code')}
          className={`flex-1 rounded-lg px-4 py-2 text-xs font-bold transition ${
            mode === 'code'
              ? 'bg-emerald-700 text-white shadow-sm'
              : 'text-slate-700 hover:bg-white/70'
          }`}
        >
          {t('onboarding.modeDirect')}
        </button>
        <button
          type="button"
          onClick={() => setMode('cascade')}
          className={`flex-1 rounded-lg px-4 py-2 text-xs font-bold transition ${
            mode === 'cascade'
              ? 'bg-emerald-700 text-white shadow-sm'
              : 'text-slate-700 hover:bg-white/70'
          }`}
        >
          {t('onboarding.modeCascade')}
        </button>
      </div>

      {mode === 'code' ? (
        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.byCode')}</label>
            <ProvenanceBadge field="lgdCode" />
          </div>
          <div className="flex gap-2">
            <input
              type="text"
              inputMode="numeric"
              maxLength={8}
              value={state.lgdCode}
              onChange={(e) => updateField('lgdCode', e.target.value)}
              placeholder={t('onboarding.codePlaceholder')}
              className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 font-mono text-sm"
            />
            <button
              type="button"
              onClick={() => void handleManualVerify()}
              disabled={isLookingUp}
              className="inline-flex items-center gap-2 rounded-lg bg-emerald-700 px-4 py-2 text-xs font-bold text-white hover:bg-emerald-800 disabled:opacity-60"
            >
              <Search className="h-4 w-4" />
              {isLookingUp ? t('onboarding.loading') : t('onboarding.verifyBtn')}
            </button>
          </div>
          <p className="text-[11px] text-slate-500">{t('onboarding.debounceHint')}</p>
        </div>
      ) : (
        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
          <div className="flex items-center gap-2 text-xs font-bold text-slate-700">
            <MapPin className="h-4 w-4 text-emerald-700" />
            <span>{t('onboarding.orCascade')}</span>
            <span className="ml-auto rounded-full bg-slate-200 px-2.5 py-0.5 text-[11px] text-slate-700">
              {t('onboarding.stateDefault')}
            </span>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <select
              aria-label={t('onboarding.district')}
              value={selectedDistrict}
              onChange={(e) => {
                setSelectedDistrict(e.target.value);
                setSelectedTehsil('');
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
              onChange={(e) => setSelectedTehsil(e.target.value)}
              className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm disabled:opacity-50"
            >
              <option value="">{t('onboarding.selectOption')}</option>
              {tehsilsQuery.data?.map((th) => (
                <option key={th.tehsil} value={th.tehsil}>
                  {th.tehsil} ({th.gp_count})
                </option>
              ))}
            </select>

            <input
              type="text"
              placeholder={t('onboarding.pincodeFilter')}
              value={pincodeFilter}
              onChange={(e) => setPincodeFilter(e.target.value)}
              className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 font-mono text-sm"
            />

            <select
              aria-label={t('onboarding.panchayat')}
              value={state.lgdCode}
              disabled={!selectedTehsil}
              onChange={(e) => {
                const found = filteredPanchayats.find((p) => p.lgd_code === e.target.value);
                if (found) {
                  applyLgdAutoFill(found);
                  setLookupError(null);
                }
              }}
              className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm disabled:opacity-50"
            >
              <option value="">{t('onboarding.selectOption')}</option>
              {filteredPanchayats.map((gp) => (
                <option key={gp.lgd_code} value={gp.lgd_code}>
                  {gp.gp_name_mr} ({gp.lgd_code})
                </option>
              ))}
            </select>
          </div>
        </div>
      )}

      {isLookingUp && (
        <div className="animate-pulse grid gap-3 sm:grid-cols-2">
          <div className="h-12 rounded-lg bg-slate-200" />
          <div className="h-12 rounded-lg bg-slate-200" />
        </div>
      )}

      {lookupError && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs text-rose-800">
          <span className="inline-flex items-center gap-2 font-medium">
            <ShieldAlert className="h-4 w-4 shrink-0" />
            {lookupError}
          </span>
          <button
            type="button"
            onClick={() => setLookupError(null)}
            className="rounded border border-rose-300 bg-white px-2.5 py-1 text-xs font-semibold text-rose-800 hover:bg-rose-100"
          >
            {t('onboarding.manualFallback')}
          </button>
        </div>
      )}

      {/* All fields are strictly editable (zero readonly lock) with live Provenance Badges */}
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.gpNameMr')}</label>
            <ProvenanceBadge field="panchayatNameMr" />
          </div>
          <input
            type="text"
            value={state.panchayatNameMr}
            onChange={(e) => updateField('panchayatNameMr', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium"
          />
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.gpNameEn')}</label>
            <ProvenanceBadge field="panchayatNameEn" />
          </div>
          <input
            type="text"
            value={state.panchayatNameEn}
            onChange={(e) => updateField('panchayatNameEn', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-medium"
          />
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.district')}</label>
            <ProvenanceBadge field="districtName" />
          </div>
          <input
            type="text"
            value={state.districtName}
            onChange={(e) => updateField('districtName', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
          />
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.tehsil')}</label>
            <ProvenanceBadge field="tehsilName" />
          </div>
          <input
            type="text"
            value={state.tehsilName}
            onChange={(e) => updateField('tehsilName', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
          />
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.pincode')}</label>
            <ProvenanceBadge field="pincode" />
          </div>
          <input
            type="text"
            maxLength={10}
            value={state.pincode}
            onChange={(e) => updateField('pincode', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-mono text-sm"
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.totalWards')}</label>
            <input
              type="number"
              min={1}
              max={50}
              value={state.totalWards}
              onChange={(e) => updateField('totalWards', Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-mono text-sm"
            />
          </div>
          <div className="space-y-1">
            <label className="text-xs font-bold text-slate-700">{t('onboarding.censusCode')}</label>
            <input
              type="text"
              value={state.censusCode}
              onChange={(e) => updateField('censusCode', e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-mono text-sm"
            />
          </div>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <label className="text-xs font-bold text-slate-700">{t('onboarding.population')}</label>
          <input
            type="number"
            min={0}
            value={state.populationCensus}
            onChange={(e) => updateField('populationCensus', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-mono text-sm"
          />
        </div>
        <div className="space-y-1">
          <label className="text-xs font-bold text-slate-700">{t('onboarding.officeAddress')}</label>
          <input
            type="text"
            value={state.officeAddress}
            onChange={(e) => updateField('officeAddress', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
          />
        </div>
      </div>
    </div>
  );
}
