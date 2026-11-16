import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  Building2,
  Calculator,
  Compass,
  Layers,
  Plus,
  Save,
  Search,
  Trash2,
  UserPlus,
  Users,
} from 'lucide-react';
import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import { type CalculatedPropertyTaxDto, formulaApi, type FormulaWorkspaceDto } from '../../services/formulaApi';
import {
  type CitizenDto,
  namuna8Api,
  type PropertyDetailDto,
  type PropertyFloorRequestItem,
} from '../../services/namuna8Api';
import { formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { QuickAddCitizenModal } from '../citizens/QuickAddCitizenModal';

const SQFT_TO_SQM = 0.09290304;

interface OwnerDraft {
  citizen: CitizenDto;
  ownershipSharePct: number;
  isPrimaryOwner: boolean;
}

interface FormDraftState {
  wardNo: string;
  propertyNo: string;
  streetOrLaneName: string;
  citySurveyOrGutNo: string;
  occupierName: string;
  propertyDescription: string;
  openPlotAreaSqFt: number;
  waterConnectionCode: string;
  otherTax: number;
  isTaxExempt: boolean;
  boundaryEast: string;
  boundaryWest: string;
  boundaryNorth: string;
  boundarySouth: string;
  owners: OwnerDraft[];
  floors: PropertyFloorRequestItem[];
}

function defaultFloorLabel(floorNo: number): string {
  if (floorNo === 0) return 'तळमजला';
  return `मजला ${floorNo}`;
}

function buildInitialDraft(
  existing: PropertyDetailDto | null,
  prefillCitizen: CitizenDto | null,
  workspace: FormulaWorkspaceDto | undefined,
): FormDraftState {
  const defaultConstCode =
    workspace?.constructionTypes.find((c) => c.code === 'RCC_PUCCA')?.code ??
    workspace?.constructionTypes[0]?.code ??
    'RCC_PUCCA';
  const defaultUsageCode = workspace?.usageWeightages?.[0]?.code ?? 'RESIDENTIAL';
  const defaultWaterCode = workspace?.waterTaxes?.[0]?.code ?? 'NONE';

  if (existing) {
    return {
      wardNo: existing.wardNo,
      propertyNo: existing.propertyNo,
      streetOrLaneName: existing.streetOrLaneName ?? '',
      citySurveyOrGutNo: existing.citySurveyOrGutNo ?? '',
      occupierName: existing.occupierName ?? '',
      propertyDescription: existing.propertyDescription ?? '',
      openPlotAreaSqFt: existing.openPlotAreaSqFt,
      waterConnectionCode: existing.waterConnectionCode || defaultWaterCode,
      otherTax: existing.otherTax,
      isTaxExempt: existing.isTaxExempt,
      boundaryEast: existing.boundaryEast ?? '',
      boundaryWest: existing.boundaryWest ?? '',
      boundaryNorth: existing.boundaryNorth ?? '',
      boundarySouth: existing.boundarySouth ?? '',
      owners: existing.owners.map((o) => ({
        citizen: {
          id: o.citizenId,
          tenantId: existing.tenantId,
          citizenCode: o.citizenCode,
          wardNo: existing.wardNo,
          fullNameMr: o.fullNameMr,
          fullNameEn: o.fullNameEn,
          gender: 'Male',
          mobileNo: o.mobileNo ?? null,
          aadhaarMasked: o.aadhaarMasked ?? null,
          socialCategory: 'General',
          exemptionCategory: o.exemptionCategory,
          isActive: o.isActive,
          linkedPropertiesCount: 1,
          createdAtUtc: '',
        },
        ownershipSharePct: o.ownershipSharePct,
        isPrimaryOwner: o.isPrimaryOwner,
      })),
      floors:
        existing.constructions.length > 0
          ? existing.constructions.map((f) => ({
              floorNo: f.floorNo,
              floorLabel: f.floorLabel,
              constructionTypeCode: f.constructionTypeCode,
              usageCode: f.usageCode,
              lengthFt: f.lengthFt > 0 ? f.lengthFt : f.areaSqFt,
              widthFt: f.widthFt > 0 ? f.widthFt : 1,
              areaSqFt: f.areaSqFt,
              constructionStartYear: f.constructionStartYear,
            }))
          : [],
    };
  }

  return {
    wardNo: prefillCitizen?.wardNo || 'W-01',
    propertyNo: '',
    streetOrLaneName: '',
    citySurveyOrGutNo: '',
    occupierName: '',
    propertyDescription: '',
    openPlotAreaSqFt: 0,
    waterConnectionCode: defaultWaterCode,
    otherTax: 0,
    isTaxExempt: false,
    boundaryEast: '',
    boundaryWest: '',
    boundaryNorth: '',
    boundarySouth: '',
    owners: prefillCitizen
      ? [{ citizen: prefillCitizen, ownershipSharePct: 100, isPrimaryOwner: true }]
      : [],
    floors: [
      {
        floorNo: 0,
        floorLabel: 'तळमजला',
        constructionTypeCode: defaultConstCode,
        usageCode: defaultUsageCode,
        lengthFt: 20,
        widthFt: 15,
        constructionStartYear: 2021,
      },
    ],
  };
}

export function PropertyFormPage() {
  const { id } = useParams<{ id?: string }>();
  const [searchParams] = useSearchParams();
  const prefillCitizenId = searchParams.get('citizenId');
  const { user, accessToken } = useAuth();
  const { t } = useTranslation();

  const isEditMode = Boolean(id);
  const canRead = Boolean(accessToken && user?.activeTenantId);

  const workspaceQuery = useQuery({
    queryKey: ['formula-workspace', user?.activeTenantId],
    queryFn: formulaApi.getWorkspace,
    enabled: canRead,
  });

  const detailQuery = useQuery({
    queryKey: ['property-detail', id],
    queryFn: () => namuna8Api.getPropertyDetail(id!),
    enabled: Boolean(canRead && id),
  });

  const prefillCitizenQuery = useQuery({
    queryKey: ['citizen-prefill', prefillCitizenId],
    queryFn: async () => {
      const res = await namuna8Api.listCitizens({ pageSize: 100 });
      return res.items.find((c) => c.id === prefillCitizenId) ?? null;
    },
    enabled: Boolean(canRead && !id && prefillCitizenId),
  });

  if (!canRead) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <h1 className="text-sm font-bold">{t('properties.addBtn')}</h1>
        <p>{t('properties.signInRequired')}</p>
        <Link to="/login" className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white">
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  if ((isEditMode && detailQuery.isLoading) || workspaceQuery.isLoading || (prefillCitizenId && prefillCitizenQuery.isLoading)) {
    return (
      <div className="space-y-4">
        <div className="h-24 animate-pulse rounded-2xl bg-slate-200" />
        <div className="h-96 animate-pulse rounded-2xl bg-slate-200" />
      </div>
    );
  }

  const formKey = `${id ?? 'new'}-${detailQuery.data?.id ?? ''}-${prefillCitizenQuery.data?.id ?? ''}-${workspaceQuery.data?.constructionTypes.length ?? 0}`;

  return (
    <PropertyFormInner
      key={formKey}
      propertyId={id ?? null}
      existing={detailQuery.data ?? null}
      prefillCitizen={prefillCitizenQuery.data ?? null}
      workspace={workspaceQuery.data}
    />
  );
}

interface PropertyFormInnerProps {
  propertyId: string | null;
  existing: PropertyDetailDto | null;
  prefillCitizen: CitizenDto | null;
  workspace: FormulaWorkspaceDto | undefined;
}

function PropertyFormInner({
  propertyId,
  existing,
  prefillCitizen,
  workspace,
}: PropertyFormInnerProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuth();

  const [form, setForm] = useState<FormDraftState>(() =>
    buildInitialDraft(existing, prefillCitizen, workspace),
  );
  const [citizenSearch, setCitizenSearch] = useState('');
  const [quickAddOpen, setQuickAddOpen] = useState(false);

  const [liveTax, setLiveTax] = useState<CalculatedPropertyTaxDto | null>(null);
  const [liveTaxError, setLiveTaxError] = useState<string | null>(null);
  const [calculating, setCalculating] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  const citizenLookupQuery = useQuery({
    queryKey: ['citizens-picker', user?.activeTenantId, citizenSearch],
    queryFn: () =>
      namuna8Api.listCitizens({
        search: citizenSearch.trim() || undefined,
        pageSize: 15,
      }),
    enabled: Boolean(user?.activeTenantId),
  });

  const constructionTypes = workspace?.constructionTypes ?? [];
  const usageWeightages = workspace?.usageWeightages ?? [];
  const waterTaxes = workspace?.waterTaxes ?? [];

  const totalBuiltUpSqFt = useMemo(
    () =>
      form.floors.reduce(
        (acc, f) => acc + Math.max(0, Number(f.lengthFt) || 0) * Math.max(0, Number(f.widthFt) || 0),
        0,
      ),
    [form.floors],
  );

  const shareTotal = useMemo(
    () => form.owners.reduce((acc, o) => acc + (Number(o.ownershipSharePct) || 0), 0),
    [form.owners],
  );

  const assessmentStartYear = workspace?.activeSetting?.rateStartYear ?? 2026;

  // Debounced live calculation against backend formula engine
  useEffect(() => {
    const validFloors = form.floors.filter(
      (f) => Number(f.lengthFt) > 0 && Number(f.widthFt) > 0 && f.constructionTypeCode && f.usageCode,
    );
    const openSqFt = Math.max(0, Number(form.openPlotAreaSqFt) || 0);
    if (validFloors.length === 0 && openSqFt <= 0) {
      return;
    }

    let cancelled = false;
    const handle = window.setTimeout(async () => {
      setCalculating(true);
      setLiveTaxError(null);
      try {
        const res = await formulaApi.calculateFromMasters({
          assessmentStartYear,
          floors: validFloors.map((f) => ({
            constructionTypeCode: f.constructionTypeCode,
            usageCode: f.usageCode,
            constructionStartYear: Number(f.constructionStartYear) || assessmentStartYear,
            lengthFt: Number(f.lengthFt),
            widthFt: Number(f.widthFt),
          })),
          openAreaSqFt: openSqFt,
          openPlotConstructionCode: 'OPEN_PLOT',
          waterConnectionCode: form.waterConnectionCode || 'NONE',
          otherTax: Math.max(0, Number(form.otherTax) || 0),
        });
        if (!cancelled) {
          setLiveTax(res);
        }
      } catch (err) {
        if (!cancelled) {
          setLiveTaxError(extractProblemValidation(err, t('formula.simFailed')).summary);
        }
      } finally {
        if (!cancelled) {
          setCalculating(false);
        }
      }
    }, 320);

    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [
    assessmentStartYear,
    form.floors,
    form.openPlotAreaSqFt,
    form.waterConnectionCode,
    form.otherTax,
    t,
  ]);

  function addCitizenOwner(c: CitizenDto) {
    if (form.owners.some((o) => o.citizen.id === c.id)) return;
    setForm((prev) => {
      const nextOwners = [
        ...prev.owners,
        {
          citizen: c,
          ownershipSharePct: prev.owners.length === 0 ? 100 : 0,
          isPrimaryOwner: prev.owners.length === 0,
        },
      ];
      return { ...prev, owners: nextOwners };
    });
  }

  function removeOwner(citizenId: string) {
    setForm((prev) => {
      const remaining = prev.owners.filter((o) => o.citizen.id !== citizenId);
      if (remaining.length > 0 && !remaining.some((o) => o.isPrimaryOwner)) {
        remaining[0] = { ...remaining[0], isPrimaryOwner: true };
      }
      return { ...prev, owners: remaining };
    });
  }

  function addFloor() {
    const nextNo = form.floors.length;
    setForm((prev) => ({
      ...prev,
      floors: [
        ...prev.floors,
        {
          floorNo: nextNo,
          floorLabel: defaultFloorLabel(nextNo),
          constructionTypeCode: constructionTypes[0]?.code ?? 'RCC_PUCCA',
          usageCode: usageWeightages[0]?.code ?? 'RESIDENTIAL',
          lengthFt: 20,
          widthFt: 15,
          constructionStartYear: assessmentStartYear,
        },
      ],
    }));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrSummary(null);
    setErrItems([]);

    const errs: ParsedValidationItem[] = [];
    if (!form.wardNo.trim()) {
      errs.push({ key: 'wardNo', rawField: 'WardNo', message: t('validation.required') });
    }
    if (!form.propertyNo.trim()) {
      errs.push({ key: 'propertyNo', rawField: 'PropertyNo', message: t('validation.required') });
    }
    if (totalBuiltUpSqFt + (Number(form.openPlotAreaSqFt) || 0) <= 0) {
      errs.push({
        key: 'totalArea',
        rawField: 'BuiltUpAreaSqFt',
        message: t('validation.propertyAreaPositive'),
      });
    }
    if (form.owners.length === 0) {
      errs.push({ key: 'owners', rawField: 'Owners', message: t('properties.errOwnerRequired') });
    } else if (Math.abs(shareTotal - 100) > 0.01) {
      errs.push({
        key: 'owners',
        rawField: 'Owners',
        message: t('properties.errShareSum100', { sum: shareTotal.toFixed(2) }),
      });
    }

    if (errs.length > 0) {
      setErrSummary(t('validation.summaryTitle'));
      setErrItems(errs);
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        wardNo: form.wardNo.trim(),
        propertyNo: form.propertyNo.trim(),
        builtUpAreaSqFt: Number(totalBuiltUpSqFt.toFixed(4)),
        openPlotAreaSqFt: Math.max(0, Number(form.openPlotAreaSqFt) || 0),
        boundaryEast: form.boundaryEast.trim() || null,
        boundaryWest: form.boundaryWest.trim() || null,
        boundaryNorth: form.boundaryNorth.trim() || null,
        boundarySouth: form.boundarySouth.trim() || null,
        waterConnectionCode: form.waterConnectionCode || 'NONE',
        isTaxExempt: form.isTaxExempt,
        streetOrLaneName: form.streetOrLaneName.trim() || null,
        citySurveyOrGutNo: form.citySurveyOrGutNo.trim() || null,
        occupierName: form.occupierName.trim() || null,
        propertyDescription: form.propertyDescription.trim() || null,
        otherTax: Math.max(0, Number(form.otherTax) || 0),
        openPlotConstructionCode: 'OPEN_PLOT',
        owners: form.owners.map((o) => ({
          citizenId: o.citizen.id,
          ownershipSharePct: Number(o.ownershipSharePct),
          isPrimaryOwner: o.isPrimaryOwner,
        })),
        floors: form.floors.map((f, idx) => ({
          floorNo: idx,
          floorLabel: f.floorLabel.trim() || defaultFloorLabel(idx),
          constructionTypeCode: f.constructionTypeCode,
          usageCode: f.usageCode,
          lengthFt: Number(f.lengthFt),
          widthFt: Number(f.widthFt),
          constructionStartYear: Number(f.constructionStartYear),
        })),
        calculateTax: true,
      };

      const saved = propertyId
        ? await namuna8Api.updateProperty(propertyId, payload)
        : await namuna8Api.createProperty(payload);

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['properties', user?.activeTenantId] }),
        queryClient.invalidateQueries({ queryKey: ['properties-hub-summary', user?.activeTenantId] }),
        queryClient.invalidateQueries({ queryKey: ['property-detail', saved.id] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard', 'summary', user?.activeTenantId] }),
      ]);

      navigate(`/namuna-8/view/${saved.id}`);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('onboarding.submitFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setSubmitting(false);
    }
  }

  const candidateCitizens = (citizenLookupQuery.data?.items ?? []).filter(
    (c) => !form.owners.some((o) => o.citizen.id === c.id),
  );

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <Link
            to="/properties"
            className="inline-flex items-center gap-1 rounded-xl border border-line bg-card px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            {t('properties.backToHub')}
          </Link>
          <h1 className="text-xl font-extrabold text-ink">
            {propertyId ? t('properties.editFormTitle') : t('properties.addFormTitle')}
          </h1>
        </div>
      </div>

      <ValidationSummary summary={errSummary} items={errItems} />

      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="grid gap-5 lg:grid-cols-[1fr_380px] items-start text-xs">
        {/* Left Entry Sections */}
        <div className="space-y-5">
          {/* Section 1: Identification & Plot */}
          <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
            <div className="flex items-center gap-2 border-b border-line pb-2.5">
              <Building2 className="h-4 w-4 text-brand-700" />
              <h2 className="text-sm font-extrabold text-ink">{t('properties.secIdentity')}</h2>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <label className="space-y-1 font-bold text-slate-700">
                <span>
                  {t('properties.wardNo')} <span className="text-rose-600">*</span>
                </span>
                <input
                  type="text"
                  disabled={Boolean(propertyId)}
                  value={form.wardNo}
                  onChange={(e) => setForm((p) => ({ ...p, wardNo: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 mono disabled:bg-slate-100"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>
                  {t('properties.propertyNo')} <span className="text-rose-600">*</span>
                </span>
                <input
                  type="text"
                  disabled={Boolean(propertyId)}
                  value={form.propertyNo}
                  onChange={(e) => setForm((p) => ({ ...p, propertyNo: e.target.value }))}
                  placeholder="e.g. 102"
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 mono disabled:bg-slate-100"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.citySurveyNo')}</span>
                <input
                  type="text"
                  value={form.citySurveyOrGutNo}
                  onChange={(e) => setForm((p) => ({ ...p, citySurveyOrGutNo: e.target.value }))}
                  placeholder="CS-45 / गट १२"
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.streetLane')}</span>
                <input
                  type="text"
                  value={form.streetOrLaneName}
                  onChange={(e) => setForm((p) => ({ ...p, streetOrLaneName: e.target.value }))}
                  placeholder="चावडी रोड / मेन गल्ली"
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.occupierName')}</span>
                <input
                  type="text"
                  value={form.occupierName}
                  onChange={(e) => setForm((p) => ({ ...p, occupierName: e.target.value }))}
                  placeholder={t('properties.occupierPlaceholder')}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.description')}</span>
                <input
                  type="text"
                  value={form.propertyDescription}
                  onChange={(e) => setForm((p) => ({ ...p, propertyDescription: e.target.value }))}
                  placeholder="निवासी दुमजली घर"
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.openPlotArea')}</span>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.openPlotAreaSqFt}
                  onChange={(e) => setForm((p) => ({ ...p, openPlotAreaSqFt: Number(e.target.value) }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                />
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.waterConnection')}</span>
                <select
                  value={form.waterConnectionCode}
                  onChange={(e) => setForm((p) => ({ ...p, waterConnectionCode: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                >
                  {waterTaxes.length > 0 ? (
                    waterTaxes.map((w) => (
                      <option key={w.id} value={w.code}>
                        {w.waterTypeName} ({formatInr(w.amount)})
                      </option>
                    ))
                  ) : (
                    <>
                      <option value="NONE">{t('properties.waterNone')}</option>
                      <option value="GENERAL">{t('properties.waterGeneral')}</option>
                      <option value="SPECIAL">{t('properties.waterSpecial')}</option>
                    </>
                  )}
                </select>
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.simOtherTax')}</span>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.otherTax}
                  onChange={(e) => setForm((p) => ({ ...p, otherTax: Number(e.target.value) }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
                />
              </label>
            </div>

            <label className="inline-flex items-center gap-2 font-semibold text-slate-700 cursor-pointer">
              <input
                type="checkbox"
                checked={form.isTaxExempt}
                onChange={(e) => setForm((p) => ({ ...p, isTaxExempt: e.target.checked }))}
                className="h-4 w-4 rounded border-slate-300 text-brand-700"
              />
              <span>{t('properties.exemptLabel')}</span>
            </label>
          </section>

          {/* Section 2: Owners & Joint Ownership (100% Share) */}
          <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line pb-2.5">
              <div className="flex items-center gap-2">
                <Users className="h-4 w-4 text-brand-700" />
                <h2 className="text-sm font-extrabold text-ink">{t('properties.secOwners')}</h2>
                <span
                  className={`rounded-full px-2.5 py-0.5 mono text-[10.5px] font-bold border ${
                    Math.abs(shareTotal - 100) <= 0.01
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                      : 'border-rose-200 bg-rose-50 text-rose-800'
                  }`}
                >
                  {shareTotal.toFixed(2)}% / 100%
                </span>
              </div>

              <button
                type="button"
                onClick={() => setQuickAddOpen(true)}
                className="inline-flex items-center gap-1 rounded-xl border border-brand-200 bg-brand-50 px-3 py-1.5 font-bold text-brand-800 hover:bg-brand-100"
              >
                <UserPlus className="h-3.5 w-3.5" />
                {t('properties.quickAddCitizenBtn')}
              </button>
            </div>

            {/* Citizen Picker Search */}
            <div className="space-y-2">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
                <input
                  type="search"
                  value={citizenSearch}
                  onChange={(e) => setCitizenSearch(e.target.value)}
                  placeholder={t('properties.searchCitizenPlaceholder')}
                  className="w-full rounded-xl border border-line bg-surface pl-9 pr-3 py-2 focus:bg-white focus:border-brand-600 focus:outline-none"
                />
              </div>

              {candidateCitizens.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {candidateCitizens.slice(0, 6).map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      onClick={() => addCitizenOwner(c)}
                      className="inline-flex items-center gap-1 rounded-lg border border-line bg-surface px-2.5 py-1 text-[11px] font-semibold text-ink hover:border-brand-300 hover:bg-brand-50"
                    >
                      <Plus className="h-3 w-3 text-brand-700" />
                      {c.fullNameMr} ({c.citizenCode})
                    </button>
                  ))}
                </div>
              )}
            </div>

            {form.owners.length === 0 ? (
              <div className="rounded-xl border border-dashed border-slate-300 bg-surface p-4 text-center text-muted">
                {t('properties.noOwnersSelected')}
              </div>
            ) : (
              <div className="space-y-2">
                {form.owners.map((o, idx) => (
                  <div
                    key={o.citizen.id}
                    className="grid gap-2 sm:grid-cols-[1fr_110px_110px_auto] items-center rounded-xl border border-line bg-surface p-3"
                  >
                    <div>
                      <div className="font-bold text-ink">{o.citizen.fullNameMr}</div>
                      <div className="text-[11px] text-muted mono">
                        {o.citizen.fullNameEn} · {o.citizen.citizenCode} · {o.citizen.aadhaarMasked || '—'}
                      </div>
                    </div>

                    <label className="space-y-0.5 font-bold text-slate-700">
                      <span className="text-[10.5px]">{t('properties.sharePctLabel')}</span>
                      <input
                        type="number"
                        min="0.01"
                        max="100"
                        step="0.01"
                        value={o.ownershipSharePct}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            owners: p.owners.map((row, i) =>
                              i === idx ? { ...row, ownershipSharePct: Number(e.target.value) } : row,
                            ),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 mono"
                      />
                    </label>

                    <label className="inline-flex items-center gap-1.5 font-bold text-slate-700 cursor-pointer pt-3">
                      <input
                        type="radio"
                        name="primaryOwner"
                        checked={o.isPrimaryOwner}
                        onChange={() =>
                          setForm((p) => ({
                            ...p,
                            owners: p.owners.map((row, i) => ({ ...row, isPrimaryOwner: i === idx })),
                          }))
                        }
                      />
                      <span>{t('properties.primaryBadge')}</span>
                    </label>

                    <button
                      type="button"
                      onClick={() => removeOwner(o.citizen.id)}
                      className="rounded-lg border border-rose-200 bg-rose-50 p-1.5 text-rose-700 hover:bg-rose-100 justify-self-end"
                      aria-label={t('formula.removeRowBtn')}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Section 3: Multi-Floor Construction Builder */}
          <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line pb-2.5">
              <div className="flex items-center gap-2">
                <Layers className="h-4 w-4 text-brand-700" />
                <h2 className="text-sm font-extrabold text-ink">{t('properties.secFloors')}</h2>
                <span className="rounded-full bg-brand-50 border border-brand-200 px-2.5 py-0.5 mono text-[10.5px] font-bold text-brand-800">
                  {totalBuiltUpSqFt.toFixed(2)} {t('dashboard.sqFtUnit')} ({(totalBuiltUpSqFt * SQFT_TO_SQM).toFixed(2)} {t('dashboard.sqMUnit')})
                </span>
              </div>
              <button
                type="button"
                onClick={addFloor}
                className="inline-flex items-center gap-1 rounded-xl border border-brand-200 bg-brand-50 px-3 py-1.5 font-bold text-brand-800 hover:bg-brand-100"
              >
                <Plus className="h-3.5 w-3.5" />
                {t('formula.simAddFloorBtn')}
              </button>
            </div>

            {form.floors.map((floor, idx) => {
              const sqFt = (Number(floor.lengthFt) || 0) * (Number(floor.widthFt) || 0);
              const sqM = sqFt * SQFT_TO_SQM;
              return (
                <div key={idx} className="rounded-xl border border-line bg-surface p-3.5 space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="font-extrabold text-brand-900">
                      #{idx + 1} · {floor.floorLabel || defaultFloorLabel(idx)}
                    </span>
                    <div className="flex items-center gap-2">
                      <span className="mono text-[11px] font-bold text-slate-700">
                        {sqFt.toFixed(2)} {t('dashboard.sqFtUnit')} ({sqM.toFixed(2)} {t('dashboard.sqMUnit')})
                      </span>
                      {form.floors.length > 0 && (
                        <button
                          type="button"
                          onClick={() =>
                            setForm((p) => ({
                              ...p,
                              floors: p.floors.filter((_, i) => i !== idx),
                            }))
                          }
                          className="rounded-lg border border-rose-200 bg-rose-50 p-1 text-rose-700 hover:bg-rose-100"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="grid gap-2.5 sm:grid-cols-3">
                    <label className="space-y-1 font-bold text-slate-700">
                      <span>{t('properties.floorLabel')}</span>
                      <input
                        type="text"
                        value={floor.floorLabel}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            floors: p.floors.map((row, i) => (i === idx ? { ...row, floorLabel: e.target.value } : row)),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5"
                      />
                    </label>
                    <label className="space-y-1 font-bold text-slate-700">
                      <span>{t('formula.colTypeName')}</span>
                      <select
                        value={floor.constructionTypeCode}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            floors: p.floors.map((row, i) =>
                              i === idx ? { ...row, constructionTypeCode: e.target.value } : row,
                            ),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5"
                      >
                        {constructionTypes.map((c) => (
                          <option key={c.id} value={c.code}>
                            {c.typeName}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="space-y-1 font-bold text-slate-700">
                      <span>{t('formula.colUsageName')}</span>
                      <select
                        value={floor.usageCode}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            floors: p.floors.map((row, i) =>
                              i === idx ? { ...row, usageCode: e.target.value } : row,
                            ),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5"
                      >
                        {usageWeightages.map((u) => (
                          <option key={u.id} value={u.code}>
                            {u.usageName} ({u.weightage.toFixed(2)}x)
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>

                  <div className="grid gap-2.5 sm:grid-cols-3">
                    <label className="space-y-1 font-bold text-slate-700">
                      <span>{t('formula.simBuiltYear')}</span>
                      <input
                        type="number"
                        value={floor.constructionStartYear}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            floors: p.floors.map((row, i) =>
                              i === idx ? { ...row, constructionStartYear: Number(e.target.value) } : row,
                            ),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 mono"
                      />
                    </label>
                    <label className="space-y-1 font-bold text-slate-700">
                      <span>{t('formula.simLengthFt')}</span>
                      <input
                        type="number"
                        min="0"
                        step="0.1"
                        value={floor.lengthFt}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            floors: p.floors.map((row, i) =>
                              i === idx ? { ...row, lengthFt: Number(e.target.value) } : row,
                            ),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 mono"
                      />
                    </label>
                    <label className="space-y-1 font-bold text-slate-700">
                      <span>{t('formula.simWidthFt')}</span>
                      <input
                        type="number"
                        min="0"
                        step="0.1"
                        value={floor.widthFt}
                        onChange={(e) =>
                          setForm((p) => ({
                            ...p,
                            floors: p.floors.map((row, i) =>
                              i === idx ? { ...row, widthFt: Number(e.target.value) } : row,
                            ),
                          }))
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 mono"
                      />
                    </label>
                  </div>
                </div>
              );
            })}
          </section>

          {/* Section 4: Four Boundaries */}
          <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
            <div className="flex items-center gap-2 border-b border-line pb-2.5">
              <Compass className="h-4 w-4 text-brand-700" />
              <h2 className="text-sm font-extrabold text-ink">{t('properties.boundariesTitle')}</h2>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.east')}</span>
                <input
                  type="text"
                  value={form.boundaryEast}
                  onChange={(e) => setForm((p) => ({ ...p, boundaryEast: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.west')}</span>
                <input
                  type="text"
                  value={form.boundaryWest}
                  onChange={(e) => setForm((p) => ({ ...p, boundaryWest: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.north')}</span>
                <input
                  type="text"
                  value={form.boundaryNorth}
                  onChange={(e) => setForm((p) => ({ ...p, boundaryNorth: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('properties.south')}</span>
                <input
                  type="text"
                  value={form.boundarySouth}
                  onChange={(e) => setForm((p) => ({ ...p, boundarySouth: e.target.value }))}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
            </div>
          </section>
        </div>

        {/* Sticky Live Capital Valuation & Tax Sidebar */}
        <aside className="lg:sticky lg:top-20 space-y-4">
          <div className="rounded-2xl border border-brand-200 bg-card p-5 shadow-sm space-y-4">
            <div className="flex items-center justify-between border-b border-line pb-2.5">
              <div className="flex items-center gap-2">
                <Calculator className="h-4 w-4 text-brand-700" />
                <h2 className="text-sm font-extrabold text-ink">{t('properties.liveCalcTitle')}</h2>
              </div>
              {calculating && (
                <span className="text-[10.5px] font-bold text-brand-700 animate-pulse">
                  {t('properties.calculating')}
                </span>
              )}
            </div>

            {liveTaxError && (
              <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-[11px] font-semibold text-amber-900">
                {liveTaxError}
              </div>
            )}

            {liveTax ? (
              <div className="space-y-3">
                <div className="rounded-xl bg-brand-700 p-4 text-white">
                  <p className="text-[11px] font-semibold opacity-90">{t('formula.simTotalTax')}</p>
                  <p className="mt-0.5 text-2xl font-extrabold mono">
                    {formatInr(form.isTaxExempt ? 0 : liveTax.calculation.totalTax)}
                  </p>
                  {form.isTaxExempt && (
                    <p className="mt-1 text-[10.5px] font-bold text-amber-200">
                      {t('properties.exemptCalcNotice')} ({formatInr(liveTax.calculation.totalTax)})
                    </p>
                  )}
                </div>

                <dl className="space-y-1.5 text-xs">
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.simBuildingTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.buildingTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.simOpenLandTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.openLandTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.colLightingTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.lightingTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.colHealthTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.healthTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.colSanitationTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.sanitationTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.colToiletTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.toiletTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.tabWaterTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.waterTax)}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted">{t('formula.simOtherTax')}</dt>
                    <dd className="mono font-bold text-ink">{formatInr(liveTax.calculation.otherTax)}</dd>
                  </div>
                  <div className="flex justify-between border-t border-line pt-2 text-[11px] text-muted">
                    <dt>{t('formula.tabAreaSlabs')}</dt>
                    <dd className="mono font-semibold">{liveTax.appliedAreaSlabRange} sq.ft</dd>
                  </div>
                </dl>
              </div>
            ) : (
              <p className="text-xs text-muted">{t('properties.liveCalcPrompt')}</p>
            )}

            <button
              type="submit"
              disabled={submitting}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-700 px-4 py-3 text-xs font-extrabold text-white hover:bg-brand-800 disabled:opacity-60 transition shadow-xs"
            >
              <Save className="h-4 w-4" />
              {submitting ? t('onboarding.submitting') : t('properties.submitAssessmentBtn')}
            </button>
          </div>
        </aside>
      </form>

      {quickAddOpen && (
        <QuickAddCitizenModal
          defaultWardNo={form.wardNo}
          onClose={() => setQuickAddOpen(false)}
          onSaved={(createdCitizen) => {
            setQuickAddOpen(false);
            void queryClient.invalidateQueries({ queryKey: ['citizens-picker', user?.activeTenantId] });
            void queryClient.invalidateQueries({ queryKey: ['citizens', user?.activeTenantId] });
            addCitizenOwner(createdCitizen);
          }}
        />
      )}
    </div>
  );
}

