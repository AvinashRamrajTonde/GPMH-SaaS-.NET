import { useQuery } from '@tanstack/react-query';
import { MapPin, RefreshCw, Search, ShieldAlert } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getDistricts,
  getPanchayatByLgd,
  getPanchayats,
  getTehsils,
  searchPanchayats,
} from '../../../services/lgdApi';
import { useOnboardingWizard } from '../context/useOnboardingWizard';
import { ProvenanceBadge } from './ProvenanceBadge';

type DiscoveryMode = 'code' | 'search' | 'cascade';

export function Step1GeoProfile() {
  const { t } = useTranslation();
  const { state, fieldErrors, updateField, applyLgdAutoFill } = useOnboardingWizard();

  const [mode, setMode] = useState<DiscoveryMode>('code');
  const [debouncedCode, setDebouncedCode] = useState('');
  const [villageQuery, setVillageQuery] = useState('');
  const [debouncedVillage, setDebouncedVillage] = useState('');
  const [selectedDistrict, setSelectedDistrict] = useState('');
  const [selectedTehsil, setSelectedTehsil] = useState('');
  const [pincodeFilter, setPincodeFilter] = useState('');
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [isLookingUp, setIsLookingUp] = useState(false);

  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedCode(state.lgdCode.trim());
    }, 400);
    return () => clearTimeout(handle);
  }, [state.lgdCode]);

  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedVillage(villageQuery.trim());
    }, 350);
    return () => clearTimeout(handle);
  }, [villageQuery]);

  useEffect(() => {
    if (mode !== 'code' || !/^\d{6}$/.test(debouncedCode)) return;

    let cancelled = false;
    async function runLookup() {
      setIsLookingUp(true);
      setLookupError(null);
      try {
        const detail = await getPanchayatByLgd(debouncedCode);
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
  }, [mode, debouncedCode, applyLgdAutoFill, t]);

  const nameSearchQuery = useQuery({
    queryKey: ['lgd', 'search', debouncedVillage],
    queryFn: () => searchPanchayats(debouncedVillage, 12),
    enabled: mode === 'search' && debouncedVillage.length >= 2,
    staleTime: 1000 * 60 * 5,
  });

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
      {/* Discovery Mode Selector Tabs */}
      <div className="grid grid-cols-1 gap-2 rounded-xl border border-line bg-slate-100 p-1.5 sm:grid-cols-3">
        <button
          type="button"
          onClick={() => setMode('code')}
          className={`rounded-lg px-3 py-2 text-xs font-bold transition ${
            mode === 'code'
              ? 'bg-brand-700 text-white shadow-xs'
              : 'text-slate-700 hover:bg-white/70'
          }`}
        >
          {t('onboarding.modeDirect')}
        </button>
        <button
          type="button"
          onClick={() => setMode('search')}
          className={`rounded-lg px-3 py-2 text-xs font-bold transition ${
            mode === 'search'
              ? 'bg-brand-700 text-white shadow-xs'
              : 'text-slate-700 hover:bg-white/70'
          }`}
        >
          {t('onboarding.modeSearch')}
        </button>
        <button
          type="button"
          onClick={() => setMode('cascade')}
          className={`rounded-lg px-3 py-2 text-xs font-bold transition ${
            mode === 'cascade'
              ? 'bg-brand-700 text-white shadow-xs'
              : 'text-slate-700 hover:bg-white/70'
          }`}
        >
          {t('onboarding.modeCascade')}
        </button>
      </div>

      {mode === 'code' && (
        <div className="rounded-xl border border-line bg-surface p-4 space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <label htmlFor="onb-lgd-code" className="text-xs font-bold text-slate-700">
              {t('onboarding.byCode')}
            </label>
            <ProvenanceBadge field="lgdCode" />
          </div>
          <div className="flex gap-2">
            <input
              id="onb-lgd-code"
              type="text"
              inputMode="numeric"
              maxLength={8}
              value={state.lgdCode}
              onChange={(e) => updateField('lgdCode', e.target.value)}
              placeholder={t('onboarding.codePlaceholder')}
              aria-invalid={Boolean(fieldErrors.lgdCode)}
              className={`flex-1 rounded-lg border bg-white px-3 py-2 mono text-sm ${
                fieldErrors.lgdCode ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
              }`}
            />
            <button
              type="button"
              onClick={() => void handleManualVerify()}
              disabled={isLookingUp}
              className="inline-flex items-center gap-2 rounded-lg bg-brand-700 px-4 py-2 text-xs font-bold text-white hover:bg-brand-800 disabled:opacity-60"
            >
              <Search className="h-4 w-4" />
              {isLookingUp ? t('onboarding.loading') : t('onboarding.verifyBtn')}
            </button>
          </div>
          {fieldErrors.lgdCode && (
            <p className="text-[11px] font-semibold text-rose-700">{fieldErrors.lgdCode}</p>
          )}
          <p className="text-[11px] text-muted">{t('onboarding.debounceHint')}</p>
        </div>
      )}

      {mode === 'search' && (
        <div className="rounded-xl border border-line bg-surface p-4 space-y-3">
          <label htmlFor="onb-village-search" className="block text-xs font-bold text-slate-700">
            {t('onboarding.villageSearchLabel')}
          </label>
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              id="onb-village-search"
              type="text"
              value={villageQuery}
              onChange={(e) => setVillageQuery(e.target.value)}
              placeholder={t('onboarding.villageSearchPlaceholder')}
              className="w-full rounded-lg border border-slate-300 bg-white pl-9 pr-3 py-2 text-sm"
            />
          </div>

          {nameSearchQuery.isFetching && (
            <p className="text-xs text-muted animate-pulse">{t('onboarding.loading')}</p>
          )}

          {nameSearchQuery.isError && (
            <p className="text-xs font-semibold text-rose-700">{t('onboarding.lgdLoadError')}</p>
          )}

          {nameSearchQuery.data && nameSearchQuery.data.length === 0 && (
            <p className="text-xs text-muted">{t('onboarding.villageSearchNoHits')}</p>
          )}

          {nameSearchQuery.data && nameSearchQuery.data.length > 0 && (
            <div className="grid max-h-56 gap-2 overflow-y-auto sm:grid-cols-2">
              {nameSearchQuery.data.map((gp) => (
                <button
                  key={gp.lgd_code}
                  type="button"
                  onClick={() => {
                    applyLgdAutoFill(gp);
                    setLookupError(null);
                  }}
                  className="flex items-center justify-between rounded-lg border border-line bg-white p-2.5 text-left text-xs hover:border-brand-600 hover:bg-brand-50/50 transition"
                >
                  <div>
                    <p className="font-bold text-ink">
                      {gp.gp_name_mr} ({gp.gp_name_en})
                    </p>
                    <p className="text-[11px] text-muted">
                      {gp.district} · {gp.tehsil}
                      {gp.pincode ? ` · ${gp.pincode}` : ''}
                    </p>
                  </div>
                  <span className="mono rounded bg-brand-100 px-2 py-0.5 text-[11px] font-bold text-brand-800">
                    {gp.lgd_code}
                  </span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {mode === 'cascade' && (
        <div className="rounded-xl border border-line bg-surface p-4 space-y-3">
          <div className="flex flex-wrap items-center gap-2 text-xs font-bold text-slate-700">
            <MapPin className="h-4 w-4 text-brand-700" />
            <span>{t('onboarding.orCascade')}</span>
            <span className="ml-auto rounded-full bg-slate-200 px-2.5 py-0.5 text-[11px] text-slate-700">
              {t('onboarding.stateDefault')}
            </span>
          </div>

          {(districtsQuery.isError || tehsilsQuery.isError || panchayatsQuery.isError) && (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900">
              <span>{t('onboarding.lgdLoadError')}</span>
              <button
                type="button"
                onClick={() => void districtsQuery.refetch()}
                className="inline-flex items-center gap-1 rounded border border-amber-400 bg-white px-2 py-1 text-[11px] font-bold"
              >
                <RefreshCw className="h-3 w-3" />
                Retry
              </button>
            </div>
          )}

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.district')}</span>
              <select
                value={selectedDistrict}
                onChange={(e) => {
                  setSelectedDistrict(e.target.value);
                  setSelectedTehsil('');
                }}
                className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-xs"
              >
                <option value="">
                  {districtsQuery.isLoading ? t('onboarding.loading') : t('onboarding.selectOption')}
                </option>
                {districtsQuery.data?.map((d) => (
                  <option key={d.district} value={d.district}>
                    {d.district} ({d.gp_count})
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.tehsil')}</span>
              <select
                value={selectedTehsil}
                disabled={!selectedDistrict}
                onChange={(e) => setSelectedTehsil(e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-xs disabled:opacity-50"
              >
                <option value="">
                  {tehsilsQuery.isFetching ? t('onboarding.loading') : t('onboarding.selectOption')}
                </option>
                {tehsilsQuery.data?.map((th) => (
                  <option key={th.tehsil} value={th.tehsil}>
                    {th.tehsil} ({th.gp_count})
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.pincodeFilter')}</span>
              <input
                type="text"
                placeholder="e.g. 431124"
                value={pincodeFilter}
                onChange={(e) => setPincodeFilter(e.target.value)}
                className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-2 mono text-xs"
              />
            </label>

            <label className="space-y-1 text-xs font-semibold text-slate-700">
              <span>{t('onboarding.panchayat')}</span>
              <select
                value={state.lgdCode}
                disabled={!selectedTehsil}
                onChange={(e) => {
                  const found = filteredPanchayats.find((p) => p.lgd_code === e.target.value);
                  if (found) {
                    applyLgdAutoFill(found);
                    setLookupError(null);
                  }
                }}
                className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-xs disabled:opacity-50"
              >
                <option value="">
                  {panchayatsQuery.isFetching ? t('onboarding.loading') : t('onboarding.selectOption')}
                </option>
                {filteredPanchayats.map((gp) => (
                  <option key={gp.lgd_code} value={gp.lgd_code}>
                    {gp.gp_name_mr} ({gp.lgd_code})
                  </option>
                ))}
              </select>
            </label>
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

      {/* Editable Geo Fields with Live Provenance Tracking */}
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label htmlFor="onb-gp-mr" className="text-xs font-bold text-slate-700">
              {t('onboarding.gpNameMr')} <span className="text-rose-600">*</span>
            </label>
            <ProvenanceBadge field="panchayatNameMr" />
          </div>
          <input
            id="onb-gp-mr"
            type="text"
            required
            aria-invalid={Boolean(fieldErrors.panchayatNameMr)}
            value={state.panchayatNameMr}
            onChange={(e) => updateField('panchayatNameMr', e.target.value)}
            className={`w-full rounded-lg border bg-white px-3 py-2 text-sm font-medium ${
              fieldErrors.panchayatNameMr ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
            }`}
          />
          {fieldErrors.panchayatNameMr && (
            <p className="text-[11px] font-semibold text-rose-700">{fieldErrors.panchayatNameMr}</p>
          )}
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label htmlFor="onb-gp-en" className="text-xs font-bold text-slate-700">
              {t('onboarding.gpNameEn')} <span className="text-rose-600">*</span>
            </label>
            <ProvenanceBadge field="panchayatNameEn" />
          </div>
          <input
            id="onb-gp-en"
            type="text"
            required
            aria-invalid={Boolean(fieldErrors.panchayatNameEn)}
            value={state.panchayatNameEn}
            onChange={(e) => updateField('panchayatNameEn', e.target.value)}
            className={`w-full rounded-lg border bg-white px-3 py-2 text-sm font-medium ${
              fieldErrors.panchayatNameEn ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
            }`}
          />
          {fieldErrors.panchayatNameEn && (
            <p className="text-[11px] font-semibold text-rose-700">{fieldErrors.panchayatNameEn}</p>
          )}
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label htmlFor="onb-dist" className="text-xs font-bold text-slate-700">
              {t('onboarding.district')} <span className="text-rose-600">*</span>
            </label>
            <ProvenanceBadge field="districtName" />
          </div>
          <input
            id="onb-dist"
            type="text"
            required
            aria-invalid={Boolean(fieldErrors.districtName)}
            value={state.districtName}
            onChange={(e) => updateField('districtName', e.target.value)}
            className={`w-full rounded-lg border bg-white px-3 py-2 text-sm ${
              fieldErrors.districtName ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
            }`}
          />
          {fieldErrors.districtName && (
            <p className="text-[11px] font-semibold text-rose-700">{fieldErrors.districtName}</p>
          )}
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label htmlFor="onb-tehsil" className="text-xs font-bold text-slate-700">
              {t('onboarding.tehsil')} <span className="text-rose-600">*</span>
            </label>
            <ProvenanceBadge field="tehsilName" />
          </div>
          <input
            id="onb-tehsil"
            type="text"
            required
            aria-invalid={Boolean(fieldErrors.tehsilName)}
            value={state.tehsilName}
            onChange={(e) => updateField('tehsilName', e.target.value)}
            className={`w-full rounded-lg border bg-white px-3 py-2 text-sm ${
              fieldErrors.tehsilName ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
            }`}
          />
          {fieldErrors.tehsilName && (
            <p className="text-[11px] font-semibold text-rose-700">{fieldErrors.tehsilName}</p>
          )}
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between gap-2">
            <label htmlFor="onb-pin" className="text-xs font-bold text-slate-700">
              {t('onboarding.pincode')}
            </label>
            <ProvenanceBadge field="pincode" />
          </div>
          <input
            id="onb-pin"
            type="text"
            maxLength={10}
            aria-invalid={Boolean(fieldErrors.pincode)}
            value={state.pincode}
            onChange={(e) => updateField('pincode', e.target.value)}
            className={`w-full rounded-lg border bg-white px-3 py-2 mono text-sm ${
              fieldErrors.pincode ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
            }`}
          />
          {fieldErrors.pincode && (
            <p className="text-[11px] font-semibold text-rose-700">{fieldErrors.pincode}</p>
          )}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1 text-xs font-bold text-slate-700">
            <span>{t('onboarding.totalWards')}</span>
            <input
              type="number"
              min={1}
              max={50}
              aria-invalid={Boolean(fieldErrors.totalWards)}
              value={state.totalWards}
              onChange={(e) => updateField('totalWards', Number(e.target.value))}
              className={`w-full rounded-lg border bg-white px-3 py-2 mono text-sm ${
                fieldErrors.totalWards ? 'border-rose-500 ring-1 ring-rose-300' : 'border-slate-300'
              }`}
            />
            {fieldErrors.totalWards && (
              <span className="block text-[11px] font-semibold text-rose-700">
                {fieldErrors.totalWards}
              </span>
            )}
          </label>
          <label className="space-y-1 text-xs font-bold text-slate-700">
            <span>{t('onboarding.censusCode')}</span>
            <input
              type="text"
              value={state.censusCode}
              onChange={(e) => updateField('censusCode', e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 mono text-sm"
            />
          </label>
        </div>

        <label className="space-y-1 text-xs font-bold text-slate-700">
          <span>{t('onboarding.population')}</span>
          <input
            type="number"
            min={0}
            value={state.populationCensus}
            onChange={(e) => updateField('populationCensus', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 mono text-sm"
          />
        </label>

        <label className="space-y-1 text-xs font-bold text-slate-700">
          <span>{t('onboarding.officeAddress')}</span>
          <input
            type="text"
            value={state.officeAddress}
            onChange={(e) => updateField('officeAddress', e.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
          />
        </label>
      </div>
    </div>
  );
}
