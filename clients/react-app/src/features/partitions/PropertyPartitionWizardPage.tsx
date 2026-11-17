import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRightLeft,
  Building2,
  Calculator,
  CheckCircle2,
  FileUp,
  GitBranch,
  Plus,
  Printer,
  Scale,
  Search,
  ShieldAlert,
  ShieldCheck,
  Trash2,
  UserPlus,
  Users,
} from 'lucide-react';
import { useState, type ChangeEvent, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import { formulaApi } from '../../services/formulaApi';
import { mutationApi, type UploadedMutationAssetDto } from '../../services/mutationApi';
import { type CitizenDto, namuna8Api } from '../../services/namuna8Api';
import {
  partitionApi,
  type ExecutePartitionResultDto,
  type PartitionEligibilityDto,
} from '../../services/partitionApi';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { QuickAddCitizenModal } from '../citizens/QuickAddCitizenModal';

const SQFT_TO_SQM = 0.09290304;
const WATER_CODES = ['NONE', 'GENERAL', 'SPECIAL'] as const;

interface OwnerDraft {
  citizen: CitizenDto;
  sharePct: string;
  isPrimary: boolean;
}

interface ChildPartitionDraft {
  localId: string;
  childPropertyNo: string;
  builtUpSqFt: string;
  openPlotSqFt: string;
  waterConnectionCode: string;
  occupierName: string;
  boundaryEast: string;
  boundaryWest: string;
  boundaryNorth: string;
  boundarySouth: string;
  isTaxExempt: boolean;
  owners: OwnerDraft[];
  estimatedTax: number | null;
}

function roundTo2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function buildInitialDrafts(
  eligibility: PartitionEligibilityDto,
  count: number,
  seedIdxStart = 1,
): ChildPartitionDraft[] {
  const safeCount = Math.max(2, count);
  const baseNo = eligibility.propertyNo.trim();
  const totalBuiltUp = eligibility.builtUpAreaSqFt;
  const totalOpen = eligibility.openPlotAreaSqFt;

  let remBuiltUp = totalBuiltUp;
  let remOpen = totalOpen;

  return Array.from({ length: safeCount }, (_, idx) => {
    const isLast = idx === safeCount - 1;
    const builtUp = isLast ? roundTo2(remBuiltUp) : roundTo2(totalBuiltUp / safeCount);
    const open = isLast ? roundTo2(remOpen) : roundTo2(totalOpen / safeCount);
    remBuiltUp = Math.max(0, remBuiltUp - builtUp);
    remOpen = Math.max(0, remOpen - open);

    return {
      localId: `part-${seedIdxStart + idx}-${Date.now()}`,
      childPropertyNo: `${baseNo}/${idx + 1}`,
      builtUpSqFt: builtUp.toString(),
      openPlotSqFt: open.toString(),
      waterConnectionCode: eligibility.waterConnectionCode || 'NONE',
      occupierName: '',
      boundaryEast: '',
      boundaryWest: '',
      boundaryNorth: '',
      boundarySouth: '',
      isTaxExempt: false,
      owners: [],
      estimatedTax: null,
    };
  });
}

interface PartitionWizardEditorProps {
  eligibility: PartitionEligibilityDto;
  canApprovePartition: boolean;
  tenantId: string;
  onSuccess: (result: ExecutePartitionResultDto) => void;
}

function PartitionWizardEditor({
  eligibility,
  canApprovePartition,
  tenantId,
  onSuccess,
}: PartitionWizardEditorProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [parts, setParts] = useState<ChildPartitionDraft[]>(() => buildInitialDrafts(eligibility, 2));
  const [citizenSearch, setCitizenSearch] = useState('');
  const [activeCitizenPartId, setActiveCitizenPartId] = useState<string>(() => parts[0]?.localId ?? '');
  const [quickAddTargetPartId, setQuickAddTargetPartId] = useState<string | null>(null);

  const [partitionReason, setPartitionReason] = useState('Family Partition Deed');
  const [documentReferenceNo, setDocumentReferenceNo] = useState('');
  const [documentDate, setDocumentDate] = useState(new Date().toISOString().slice(0, 10));
  const [resolutionNo, setResolutionNo] = useState('');
  const [resolutionDate, setResolutionDate] = useState(new Date().toISOString().slice(0, 10));
  const [remarks, setRemarks] = useState('');
  const [uploadedAssets, setUploadedAssets] = useState<UploadedMutationAssetDto[]>([]);
  const [uploading, setUploading] = useState(false);

  const [estimatingIdx, setEstimatingIdx] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [validationSummary, setValidationSummary] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<ParsedValidationItem[]>([]);

  const citizensQuery = useQuery({
    queryKey: ['partition-citizen-picker', tenantId, citizenSearch],
    queryFn: () => namuna8Api.listCitizens({ search: citizenSearch || undefined, pageSize: 15 }),
  });

  const parentTotalSqFt =
    eligibility.totalAreaSqFt > 0
      ? eligibility.totalAreaSqFt
      : eligibility.builtUpAreaSqFt + eligibility.openPlotAreaSqFt;
  const parentAreaSqM = roundTo2(parentTotalSqFt * SQFT_TO_SQM);
  const toleranceSqFt = parentTotalSqFt * 0.001;
  const toleranceSqM = parentTotalSqFt * SQFT_TO_SQM * 0.001;

  const totalChildSqFt = parts.reduce(
    (sum, p) => sum + (Number(p.builtUpSqFt) || 0) + (Number(p.openPlotSqFt) || 0),
    0,
  );
  const totalChildSqM = totalChildSqFt * SQFT_TO_SQM;
  const deltaSqFt = Math.abs(totalChildSqFt - parentTotalSqFt);
  const deltaSqM = Math.abs(totalChildSqM - parentTotalSqFt * SQFT_TO_SQM);
  const areaConserved = parentTotalSqFt > 0 && deltaSqFt <= toleranceSqFt + 0.0001;

  const setQuickSplitCount = (count: number) => {
    setParts((prev) => {
      const next = buildInitialDrafts(eligibility, count, Date.now());
      return next.map((draft, idx) => ({
        ...draft,
        owners: prev[idx]?.owners ?? [],
      }));
    });
  };

  const handleEqualSplit = () => {
    setParts((prev) => {
      const count = prev.length;
      if (count < 2) return prev;
      let remBuilt = eligibility.builtUpAreaSqFt;
      let remOpen = eligibility.openPlotAreaSqFt;
      return prev.map((part, idx) => {
        const isLast = idx === count - 1;
        const b = isLast ? roundTo2(remBuilt) : roundTo2(eligibility.builtUpAreaSqFt / count);
        const o = isLast ? roundTo2(remOpen) : roundTo2(eligibility.openPlotAreaSqFt / count);
        remBuilt = Math.max(0, remBuilt - b);
        remOpen = Math.max(0, remOpen - o);
        return {
          ...part,
          builtUpSqFt: b.toString(),
          openPlotSqFt: o.toString(),
          estimatedTax: null,
        };
      });
    });
  };

  const addPart = () => {
    const baseNo = eligibility.propertyNo.trim();
    setParts((prev) => [
      ...prev,
      {
        localId: `part-${Date.now()}-${prev.length}`,
        childPropertyNo: `${baseNo}/${prev.length + 1}`,
        builtUpSqFt: '0',
        openPlotSqFt: '0',
        waterConnectionCode: eligibility.waterConnectionCode || 'NONE',
        occupierName: '',
        boundaryEast: '',
        boundaryWest: '',
        boundaryNorth: '',
        boundarySouth: '',
        isTaxExempt: false,
        owners: [],
        estimatedTax: null,
      },
    ]);
  };

  const removePart = (localId: string) => {
    setParts((prev) => (prev.length <= 2 ? prev : prev.filter((p) => p.localId !== localId)));
  };

  const updatePartField = (
    localId: string,
    patch: Partial<ChildPartitionDraft>,
  ) => {
    setParts((prev) =>
      prev.map((p) => (p.localId === localId ? { ...p, ...patch, estimatedTax: null } : p)),
    );
  };

  const addOwnerToPart = (localId: string, citizen: CitizenDto) => {
    setParts((prev) =>
      prev.map((part) => {
        if (part.localId !== localId) return part;
        if (part.owners.some((o) => o.citizen.id === citizen.id)) return part;
        const nextOwners: OwnerDraft[] = [
          ...part.owners,
          {
            citizen,
            sharePct: part.owners.length === 0 ? '100' : '0',
            isPrimary: part.owners.length === 0,
          },
        ];
        if (nextOwners.length === 2 && Number(nextOwners[0].sharePct) === 100) {
          nextOwners[0].sharePct = '50';
          nextOwners[1].sharePct = '50';
        }
        return { ...part, owners: nextOwners };
      }),
    );
  };

  const removeOwnerFromPart = (localId: string, citizenId: string) => {
    setParts((prev) =>
      prev.map((part) => {
        if (part.localId !== localId) return part;
        const filtered = part.owners.filter((o) => o.citizen.id !== citizenId);
        if (filtered.length > 0 && !filtered.some((o) => o.isPrimary)) {
          filtered[0] = { ...filtered[0], isPrimary: true };
        }
        return { ...part, owners: filtered };
      }),
    );
  };

  const setPrimaryOwnerOnPart = (localId: string, citizenId: string) => {
    setParts((prev) =>
      prev.map((part) =>
        part.localId === localId
          ? {
              ...part,
              owners: part.owners.map((o) => ({
                ...o,
                isPrimary: o.citizen.id === citizenId,
              })),
            }
          : part,
      ),
    );
  };

  const updateOwnerShareOnPart = (localId: string, citizenId: string, sharePct: string) => {
    setParts((prev) =>
      prev.map((part) =>
        part.localId === localId
          ? {
              ...part,
              owners: part.owners.map((o) =>
                o.citizen.id === citizenId ? { ...o, sharePct } : o,
              ),
            }
          : part,
      ),
    );
  };

  const handleEstimateChildTax = async (index: number) => {
    const part = parts[index];
    if (!part) return;
    const builtUp = Math.max(0, Number(part.builtUpSqFt) || 0);
    const openPlot = Math.max(0, Number(part.openPlotSqFt) || 0);
    if (builtUp + openPlot <= 0) return;

    setEstimatingIdx(index);
    try {
      const parentBuiltUp = eligibility.currentFloors.reduce((acc, f) => acc + f.areaSqFt, 0);
      const floors =
        builtUp > 0
          ? eligibility.currentFloors.length > 0 && parentBuiltUp > 0
            ? eligibility.currentFloors.map((f) => ({
                constructionTypeCode: f.constructionTypeCode,
                usageCode: f.usageCode,
                constructionStartYear: f.constructionStartYear,
                lengthFt: 0,
                widthFt: 0,
                areaSqFt: Math.max(0.01, roundTo2(f.areaSqFt * (builtUp / parentBuiltUp))),
              }))
            : [
                {
                  constructionTypeCode: 'RCC_PUCCA',
                  usageCode: 'RESIDENTIAL',
                  constructionStartYear: new Date().getFullYear() - 1,
                  lengthFt: 0,
                  widthFt: 0,
                  areaSqFt: builtUp,
                },
              ]
          : [];

      const res = await formulaApi.calculateFromMasters({
        assessmentStartYear: new Date().getFullYear(),
        floors,
        openAreaSqFt: openPlot,
        openPlotConstructionCode: 'OPEN_PLOT',
        waterConnectionCode: part.waterConnectionCode || 'NONE',
        otherTax: 0,
      });

      const finalTotal = part.isTaxExempt
        ? 0
        : res.calculation.totalTax;
      setParts((prev) =>
        prev.map((item, idx) =>
          idx === index ? { ...item, estimatedTax: finalTotal } : item,
        ),
      );
    } catch {
      // Preview failure leaves estimatedTax unset without blocking wizard flow
    } finally {
      setEstimatingIdx(null);
    }
  };

  const handleFileUpload = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    setValidationSummary(null);
    setValidationErrors([]);
    try {
      const uploaded = await mutationApi.uploadDocument(file);
      setUploadedAssets((prev) => [...prev, uploaded]);
      e.target.value = '';
    } catch (err) {
      const parsed = extractProblemValidation(err, t('mutations.uploadFailed'));
      setValidationSummary(parsed.summary);
      setValidationErrors(parsed.items);
    } finally {
      setUploading(false);
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setValidationSummary(null);
    setValidationErrors([]);

    if (!eligibility.zeroDuesGatePassed) {
      setValidationSummary(eligibility.blockReason || t('partition.zeroDuesBlockedTitle'));
      return;
    }

    if (!areaConserved) {
      setValidationSummary(t('partition.areaMismatchBanner'));
      return;
    }

    if (!resolutionNo.trim()) {
      setValidationSummary(t('partition.errResolutionRequired'));
      return;
    }

    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      if (!part.childPropertyNo.trim()) {
        setValidationSummary(t('partition.errChildPropNo', { index: i + 1 }));
        return;
      }
      if (part.owners.length === 0) {
        setValidationSummary(t('partition.errChildOwnerRequired', { propNo: part.childPropertyNo }));
        return;
      }
      const shareSum = part.owners.reduce((sum, o) => sum + (Number(o.sharePct) || 0), 0);
      if (Math.abs(shareSum - 100) > 0.01) {
        setValidationSummary(
          t('partition.errOwnerShareSum', { propNo: part.childPropertyNo, sum: shareSum.toFixed(2) }),
        );
        return;
      }
    }

    setSubmitting(true);
    try {
      const res = await partitionApi.executePartition(eligibility.propertyId, {
        resolutionNo: resolutionNo.trim(),
        resolutionDate,
        partitionReason: partitionReason.trim() || null,
        documentReferenceNo: documentReferenceNo.trim() || null,
        documentDate: documentDate || null,
        remarks: remarks.trim() || null,
        supportingAssetKeys:
          uploadedAssets.length > 0 ? uploadedAssets.map((a) => a.assetKey) : null,
        childPartitions: parts.map((part) => ({
          childPropertyNo: part.childPropertyNo.trim(),
          owners: part.owners.map((o) => ({
            citizenId: o.citizen.id,
            ownershipSharePct: Number(o.sharePct),
            isPrimaryOwner: o.isPrimary,
          })),
          builtUpAreaSqFt: Number(part.builtUpSqFt) || 0,
          openPlotAreaSqFt: Number(part.openPlotSqFt) || 0,
          floors: null,
          occupierName: part.occupierName.trim() || null,
          boundaryEast: part.boundaryEast.trim() || null,
          boundaryWest: part.boundaryWest.trim() || null,
          boundaryNorth: part.boundaryNorth.trim() || null,
          boundarySouth: part.boundarySouth.trim() || null,
          waterConnectionCode: part.waterConnectionCode || 'NONE',
          otherTax: 0,
          isTaxExempt: part.isTaxExempt,
        })),
      });

      await queryClient.invalidateQueries({ queryKey: ['properties'] });
      await queryClient.invalidateQueries({ queryKey: ['namuna8-hub-kpis'] });
      await queryClient.invalidateQueries({ queryKey: ['mutations'] });
      onSuccess(res);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('citizens.saveFailed'));
      setValidationSummary(parsed.summary);
      setValidationErrors(parsed.items);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <ValidationSummary
        summary={validationSummary}
        items={validationErrors}
      />

      {/* Section 1: Parent Property Summary & Zero-Dues Security Gate */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="rounded-2xl border border-line bg-card p-5 shadow-xs lg:col-span-2 grid grid-cols-2 gap-4 sm:grid-cols-3">
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('properties.colWardProp')}</p>
            <p className="mt-1 font-mono text-base font-extrabold text-brand-700">
              {eligibility.wardNo} / {eligibility.propertyNo}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('partition.currentOwnersLabel')}</p>
            <p className="mt-1 text-sm font-bold text-ink truncate">
              {eligibility.currentOwners.map((o) => o.fullNameMr).join(', ') || '—'}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('partition.addressLabel')}</p>
            <p className="mt-1 text-xs font-semibold text-slate-700 truncate">
              {eligibility.streetOrLaneName || eligibility.citySurveyOrGutNo || '—'}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('partition.builtUpSqFtLabel')}</p>
            <p className="mt-1 font-mono text-sm font-bold text-ink">
              {eligibility.builtUpAreaSqFt.toLocaleString('en-IN')} {t('partition.sqFtUnit')}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('partition.openPlotSqFtLabel')}</p>
            <p className="mt-1 font-mono text-sm font-bold text-ink">
              {eligibility.openPlotAreaSqFt.toLocaleString('en-IN')} {t('partition.sqFtUnit')}
            </p>
          </div>
          <div>
            <p className="text-[11px] font-bold uppercase text-muted">{t('partition.totalParentAreaLabel')}</p>
            <p className="mt-1 font-mono text-base font-extrabold text-purple-800">
              {parentTotalSqFt.toLocaleString('en-IN')} {t('partition.sqFtUnit')} ({parentAreaSqM.toFixed(2)} m²)
            </p>
          </div>
        </div>

        {/* Zero-Dues Gate Card */}
        <div
          className={`rounded-2xl border p-5 shadow-xs flex flex-col justify-between ${
            eligibility.zeroDuesGatePassed
              ? 'border-emerald-300 bg-emerald-50/70'
              : 'border-rose-300 bg-rose-50/80'
          }`}
        >
          <div>
            <div className="flex items-center justify-between gap-2">
              <span
                className={`inline-flex items-center gap-1.5 text-[11px] font-bold uppercase ${
                  eligibility.zeroDuesGatePassed ? 'text-emerald-800' : 'text-rose-800'
                }`}
              >
                {eligibility.zeroDuesGatePassed ? (
                  <ShieldCheck className="h-4 w-4" />
                ) : (
                  <ShieldAlert className="h-4 w-4" />
                )}
                {t('partition.zeroDuesGateHeader')}
              </span>
              <span
                className={`rounded-full px-2.5 py-0.5 text-[11px] font-extrabold ${
                  eligibility.zeroDuesGatePassed
                    ? 'bg-emerald-200 text-emerald-900'
                    : 'bg-rose-200 text-rose-900'
                }`}
              >
                {eligibility.zeroDuesGatePassed ? t('partition.gatePassed') : t('partition.gateBlocked')}
              </span>
            </div>

            <p
              className={`mt-2 text-sm font-extrabold ${
                eligibility.zeroDuesGatePassed ? 'text-emerald-950' : 'text-rose-950'
              }`}
            >
              {eligibility.zeroDuesGatePassed
                ? t('partition.zeroDuesClearedTitle')
                : t('partition.zeroDuesBlockedTitle')}
            </p>

            {eligibility.blockReason ? (
              <div className="mt-1 space-y-2">
                <p className="text-xs text-rose-800 leading-relaxed">{eligibility.blockReason}</p>
                <Link
                  to={`/namuna-10/collect?propertyId=${encodeURIComponent(eligibility.propertyId)}`}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-rose-700 px-3 py-1.5 text-[11px] font-bold text-white hover:bg-rose-800"
                >
                  {t('namuna10.settleDuesInNamuna10Btn')}
                </Link>
              </div>
            ) : (
              <p className="mt-1 text-xs text-emerald-800 leading-relaxed">
                {t('partition.zeroDuesClearedDesc')}
              </p>
            )}
          </div>

          <div className="mt-3 flex items-center justify-between border-t border-black/10 pt-2 text-[11px] font-semibold">
            <span>
              {t('partition.pendingDuesLabel')}: ₹{eligibility.pendingDues.toLocaleString('en-IN')}
            </span>
            <span>
              {t('partition.paidLabel')}: ₹{eligibility.paidAmount.toLocaleString('en-IN')}
            </span>
          </div>
        </div>
      </div>

      {/* Section 2: Multi-Heir Split Builder & Law of Conservation of Area */}
      <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line pb-4">
          <div>
            <h2 className="flex items-center gap-2 text-base font-bold text-ink">
              <GitBranch className="h-4 w-4 text-purple-700" />
              {t('partition.splitBuilderTitle')}
              <span className="rounded-md border border-purple-200 bg-purple-100 px-2 py-0.5 text-[11px] font-bold text-purple-900">
                ±0.1% {t('partition.conservationBadge')}
              </span>
            </h2>
            <p className="mt-0.5 text-xs text-muted">{t('partition.splitBuilderSubtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex rounded-lg border border-line bg-slate-50 p-0.5">
              {[2, 3, 4].map((cnt) => (
                <button
                  key={cnt}
                  type="button"
                  onClick={() => setQuickSplitCount(cnt)}
                  className={`rounded-md px-2.5 py-1 text-xs font-bold transition ${
                    parts.length === cnt
                      ? 'bg-brand-700 text-white shadow-xs'
                      : 'text-slate-600 hover:text-ink'
                  }`}
                >
                  {t('partition.quickPartsCount', { count: cnt })}
                </button>
              ))}
            </div>

            <button
              type="button"
              onClick={handleEqualSplit}
              className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-purple-300 bg-purple-50 px-3 text-xs font-bold text-purple-900 hover:bg-purple-100"
            >
              <Scale className="h-3.5 w-3.5" />
              {t('partition.equalSplitBtn')}
            </button>

            <button
              type="button"
              onClick={addPart}
              className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-emerald-600 px-3 text-xs font-bold text-white hover:bg-emerald-700"
            >
              <Plus className="h-3.5 w-3.5" />
              {t('partition.addHeirPartBtn')}
            </button>
          </div>
        </div>

        {/* Live Area Conservation Bar */}
        <div
          className={`flex flex-wrap items-center justify-between gap-3 rounded-xl border p-3.5 ${
            areaConserved
              ? 'border-emerald-300 bg-emerald-50/70 text-emerald-950'
              : 'border-amber-300 bg-amber-50/80 text-amber-950'
          }`}
        >
          <div className="flex items-center gap-3">
            {areaConserved ? (
              <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" />
            ) : (
              <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" />
            )}
            <div className="text-xs">
              <div className="flex flex-wrap items-center gap-3 font-semibold">
                <span>
                  {t('partition.parentTotalAreaShort')}: <strong>{parentTotalSqFt.toFixed(2)} sq.ft</strong>
                </span>
                <span>|</span>
                <span>
                  {t('partition.allocatedSumShort')}: <strong>{totalChildSqFt.toFixed(2)} sq.ft</strong> ({totalChildSqM.toFixed(2)} m²)
                </span>
                <span>|</span>
                <span>
                  Δ: <strong>{deltaSqM.toFixed(4)} m²</strong> (±{toleranceSqM.toFixed(4)} m²)
                </span>
              </div>
              <p className="mt-0.5 font-bold">
                {areaConserved ? t('partition.areaBalancedBanner') : t('partition.areaMismatchBanner')}
              </p>
            </div>
          </div>

          <span
            className={`rounded-full px-3 py-1 text-xs font-extrabold ${
              areaConserved ? 'bg-emerald-200 text-emerald-950' : 'bg-amber-200 text-amber-950'
            }`}
          >
            {parts.length} {t('partition.partsBadge')}
          </span>
        </div>

        {/* Shared Citizen Picker Bar */}
        <div className="rounded-xl border border-line bg-slate-50/70 p-3.5 space-y-2.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-xs font-bold text-ink">
              <Users className="h-4 w-4 text-brand-700" />
              <span>{t('partition.assignCitizenHint')}</span>
              <span className="rounded bg-brand-100 px-2 py-0.5 font-mono text-[11px] text-brand-900">
                {parts.find((p) => p.localId === activeCitizenPartId)?.childPropertyNo ||
                  parts[0]?.childPropertyNo}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted" />
                <input
                  type="text"
                  value={citizenSearch}
                  onChange={(e) => setCitizenSearch(e.target.value)}
                  placeholder={t('citizens.searchPlaceholder')}
                  className="h-8 rounded-lg border border-line bg-white pl-8 pr-3 text-xs outline-none focus:border-brand-600"
                />
              </div>
              <button
                type="button"
                onClick={() =>
                  setQuickAddTargetPartId(activeCitizenPartId || parts[0]?.localId || null)
                }
                className="inline-flex h-8 items-center gap-1 rounded-lg border border-brand-600 bg-white px-2.5 text-xs font-bold text-brand-700 hover:bg-brand-50"
              >
                <UserPlus className="h-3.5 w-3.5" />
                {t('properties.quickAddCitizenBtn')}
              </button>
            </div>
          </div>

          <div className="flex flex-wrap gap-1.5 max-h-24 overflow-y-auto">
            {(citizensQuery.data?.items ?? []).map((c) => (
              <button
                key={c.id}
                type="button"
                onClick={() =>
                  addOwnerToPart(activeCitizenPartId || parts[0].localId, c)
                }
                className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-white px-2.5 py-1 text-xs font-medium hover:border-brand-600 hover:bg-brand-50/40"
              >
                <Plus className="h-3 w-3 text-brand-700" />
                <span className="font-bold text-ink">{c.fullNameMr}</span>
                <span className="font-mono text-[10px] text-muted">{c.aadhaarMasked}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Dynamic Child Partition Cards */}
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          {parts.map((part, idx) => {
            const pSqFt = (Number(part.builtUpSqFt) || 0) + (Number(part.openPlotSqFt) || 0);
            const pSqM = pSqFt * SQFT_TO_SQM;
            const shareSum = part.owners.reduce((sum, o) => sum + (Number(o.sharePct) || 0), 0);
            const isSelectedTarget = (activeCitizenPartId || parts[0]?.localId) === part.localId;

            return (
              <div
                key={part.localId}
                onClick={() => setActiveCitizenPartId(part.localId)}
                className={`rounded-2xl border p-4 space-y-3 transition ${
                  isSelectedTarget
                    ? 'border-brand-600 bg-brand-50/20 shadow-xs'
                    : 'border-line bg-white'
                }`}
              >
                <div className="flex items-center justify-between gap-2 border-b border-line pb-2.5">
                  <div className="flex items-center gap-2">
                    <span className="flex h-6 w-6 items-center justify-center rounded-full bg-purple-700 text-[11px] font-bold text-white">
                      {idx + 1}
                    </span>
                    <span className="text-xs font-extrabold text-ink">
                      {t('partition.heirPartCardTitle', { index: idx + 1 })}
                    </span>
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      disabled={estimatingIdx === idx || pSqFt <= 0}
                      onClick={(e) => {
                        e.stopPropagation();
                        void handleEstimateChildTax(idx);
                      }}
                      className="inline-flex h-7 items-center gap-1 rounded-md border border-indigo-200 bg-indigo-50 px-2 text-[11px] font-bold text-indigo-800 hover:bg-indigo-100 disabled:opacity-50"
                    >
                      <Calculator className="h-3 w-3" />
                      {part.estimatedTax !== null
                        ? `₹${part.estimatedTax.toLocaleString('en-IN')}`
                        : estimatingIdx === idx
                          ? t('properties.calculating')
                          : t('partition.estimateTaxBtn')}
                    </button>

                    {parts.length > 2 && (
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          removePart(part.localId);
                        }}
                        className="rounded-md p-1 text-rose-600 hover:bg-rose-50"
                        title={t('partition.removePartBtn')}
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <div>
                    <label className="mb-1 block text-[11px] font-semibold text-muted">
                      {t('partition.childPropNoLabel')} *
                    </label>
                    <input
                      type="text"
                      value={part.childPropertyNo}
                      onChange={(e) =>
                        updatePartField(part.localId, { childPropertyNo: e.target.value })
                      }
                      className="h-9 w-full rounded-lg border border-line bg-white px-2.5 font-mono text-xs font-bold outline-none focus:border-brand-600"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-[11px] font-semibold text-muted">
                      {t('partition.builtUpSqFtLabel')}
                    </label>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      value={part.builtUpSqFt}
                      onChange={(e) =>
                        updatePartField(part.localId, { builtUpSqFt: e.target.value })
                      }
                      className="h-9 w-full rounded-lg border border-line bg-white px-2.5 font-mono text-xs outline-none focus:border-brand-600"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-[11px] font-semibold text-muted">
                      {t('partition.openPlotSqFtLabel')}
                    </label>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      value={part.openPlotSqFt}
                      onChange={(e) =>
                        updatePartField(part.localId, { openPlotSqFt: e.target.value })
                      }
                      className="h-9 w-full rounded-lg border border-line bg-white px-2.5 font-mono text-xs outline-none focus:border-brand-600"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-[11px] font-semibold text-muted">
                      {t('properties.waterConnection')}
                    </label>
                    <select
                      value={part.waterConnectionCode}
                      onChange={(e) =>
                        updatePartField(part.localId, { waterConnectionCode: e.target.value })
                      }
                      className="h-9 w-full rounded-lg border border-line bg-white px-2 text-xs outline-none focus:border-brand-600"
                    >
                      {WATER_CODES.map((wc) => (
                        <option key={wc} value={wc}>
                          {wc}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 text-[11px] text-slate-600">
                  <span>
                    {t('partition.partTotalLabel')}:{' '}
                    <strong className="font-mono text-ink">
                      {pSqFt.toFixed(2)} sq.ft ({pSqM.toFixed(2)} m²)
                    </strong>
                  </span>
                  <label className="inline-flex items-center gap-1.5 font-semibold">
                    <input
                      type="checkbox"
                      checked={part.isTaxExempt}
                      onChange={(e) =>
                        updatePartField(part.localId, { isTaxExempt: e.target.checked })
                      }
                      className="h-3.5 w-3.5 rounded border-line text-brand-700"
                    />
                    {t('properties.exemptLabel')}
                  </label>
                </div>

                {/* Assigned Owners for this Heir */}
                <div className="rounded-xl border border-line bg-slate-50/70 p-2.5 space-y-2">
                  <div className="flex items-center justify-between text-[11px]">
                    <span className="font-bold text-ink">
                      {t('partition.heirOwnersLabel')} *
                    </span>
                    <span
                      className={`font-mono font-bold ${
                        Math.abs(shareSum - 100) <= 0.01 ? 'text-emerald-700' : 'text-amber-700'
                      }`}
                    >
                      {shareSum.toFixed(1)}% / 100%
                    </span>
                  </div>

                  {part.owners.length === 0 ? (
                    <p className="text-[11px] text-muted">{t('partition.clickCitizenToAssign')}</p>
                  ) : (
                    <div className="space-y-1.5">
                      {part.owners.map((o) => (
                        <div
                          key={o.citizen.id}
                          className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-line bg-white px-2.5 py-1.5 text-xs"
                        >
                          <div className="min-w-0 flex-1">
                            <p className="truncate font-bold text-ink">{o.citizen.fullNameMr}</p>
                            <p className="truncate text-[10px] text-muted">{o.citizen.fullNameEn}</p>
                          </div>
                          <div className="flex items-center gap-2">
                            <input
                              type="number"
                              min="0.01"
                              max="100"
                              step="0.01"
                              value={o.sharePct}
                              onChange={(e) =>
                                updateOwnerShareOnPart(part.localId, o.citizen.id, e.target.value)
                              }
                              className="h-7 w-16 rounded border border-line px-1.5 text-right font-mono text-xs"
                            />
                            <button
                              type="button"
                              onClick={() => setPrimaryOwnerOnPart(part.localId, o.citizen.id)}
                              className={`rounded px-1.5 py-0.5 text-[10px] font-bold ${
                                o.isPrimary
                                  ? 'bg-emerald-100 text-emerald-900'
                                  : 'bg-slate-100 text-slate-600'
                              }`}
                            >
                              {t('properties.primaryBadge')}
                            </button>
                            <button
                              type="button"
                              onClick={() => removeOwnerFromPart(part.localId, o.citizen.id)}
                              className="text-rose-600 hover:text-rose-800"
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* Section 3: Deed Evidence & GP Resolution */}
      <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-4">
        <h2 className="text-base font-bold text-ink">{t('partition.deedAndResolutionTitle')}</h2>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <div>
            <label className="mb-1 block text-xs font-semibold text-ink">
              {t('partition.reasonLabel')}
            </label>
            <input
              type="text"
              value={partitionReason}
              onChange={(e) => setPartitionReason(e.target.value)}
              className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-ink">
              {t('mutations.docRefNoLabel')}
            </label>
            <input
              type="text"
              value={documentReferenceNo}
              onChange={(e) => setDocumentReferenceNo(e.target.value)}
              placeholder="Deed No. 12/2026"
              className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-ink">
              {t('mutations.docDateLabel')}
            </label>
            <input
              type="date"
              value={documentDate}
              onChange={(e) => setDocumentDate(e.target.value)}
              className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-ink">
              {t('mutations.resolutionNoLabel')} *
            </label>
            <input
              type="text"
              required
              value={resolutionNo}
              onChange={(e) => setResolutionNo(e.target.value)}
              placeholder="ठराव क्र. ९/१"
              className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-ink">
              {t('mutations.resolutionDateLabel')} *
            </label>
            <input
              type="date"
              required
              value={resolutionDate}
              onChange={(e) => setResolutionDate(e.target.value)}
              className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-ink">
              {t('mutations.remarksLabel')}
            </label>
            <input
              type="text"
              value={remarks}
              onChange={(e) => setRemarks(e.target.value)}
              className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
            />
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4">
          <div className="flex flex-wrap items-center gap-3">
            <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-brand-600 bg-brand-50 px-3.5 py-2 text-xs font-bold text-brand-800 hover:bg-brand-100">
              <FileUp className="h-4 w-4" />
              {uploading ? t('partition.loadingLabel') : t('mutations.uploadDocBtn')}
              <input
                type="file"
                accept=".pdf,.png,.jpg,.jpeg,.webp,application/pdf,image/*"
                className="hidden"
                onChange={handleFileUpload}
                disabled={uploading}
              />
            </label>
            {uploadedAssets.map((a) => (
              <span
                key={a.assetKey}
                className="rounded-md border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-[11px] font-semibold text-emerald-900"
              >
                {a.fileName}
              </span>
            ))}
          </div>

          <button
            type="submit"
            disabled={!canApprovePartition || !eligibility.zeroDuesGatePassed || !areaConserved || submitting}
            className="inline-flex h-10 items-center gap-2 rounded-xl bg-emerald-700 px-5 text-xs font-extrabold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <GitBranch className="h-4 w-4" />
            {submitting ? t('partition.savingLabel') : t('partition.executeSubmitBtn')}
          </button>
        </div>

        {!canApprovePartition && (
          <p className="text-xs font-semibold text-amber-800">{t('partition.gramsevakOnlyNote')}</p>
        )}
      </section>

      {quickAddTargetPartId && (
        <QuickAddCitizenModal
          defaultWardNo={eligibility.wardNo}
          onClose={() => setQuickAddTargetPartId(null)}
          onSaved={(citizen) => {
            addOwnerToPart(quickAddTargetPartId, citizen);
            setQuickAddTargetPartId(null);
          }}
        />
      )}
    </form>
  );
}

export function PropertyPartitionWizardPage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();
  const { id: routePropertyId } = useParams<{ id?: string }>();

  const [selectedPropertyId, setSelectedPropertyId] = useState(routePropertyId ?? '');
  const [completedResult, setCompletedResult] = useState<ExecutePartitionResultDto | null>(null);

  const canRead = Boolean(accessToken && user?.activeTenantId);
  const canApprovePartition = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin'].includes(user.role),
  );

  const propsQuery = useQuery({
    queryKey: ['partition-properties-list', user?.activeTenantId],
    queryFn: namuna8Api.listProperties,
    enabled: canRead,
  });

  const activePropertyId = selectedPropertyId || routePropertyId || '';
  const eligibilityQuery = useQuery({
    queryKey: ['partition-eligibility', activePropertyId],
    queryFn: () => partitionApi.getEligibility(activePropertyId),
    enabled: canRead && Boolean(activePropertyId),
  });

  if (!canRead) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <p>{t('properties.signInRequired')}</p>
        <Link to="/login" className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white">
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  const activeProps = (propsQuery.data ?? []).filter((p) => p.isActive && p.status !== 'Partitioned');

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link
            to="/properties"
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-muted hover:text-brand-700"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            {t('properties.backToHub')}
          </Link>
          <h1 className="mt-1 text-2xl font-bold text-ink">{t('partition.pageTitle')}</h1>
          <p className="text-xs text-muted">{t('partition.pageSubtitle')}</p>
        </div>

        <div className="flex items-center gap-2">
          <Link
            to={
              activePropertyId
                ? `/namuna-8/mutation/new?propertyId=${encodeURIComponent(activePropertyId)}`
                : '/namuna-8/mutation/new'
            }
            className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50"
          >
            <ArrowRightLeft className="h-3.5 w-3.5" />
            {t('partition.switchToMutationTab')}
          </Link>
          <span className="inline-flex items-center gap-1.5 rounded-lg bg-brand-700 px-3 py-2 text-xs font-bold text-white">
            <GitBranch className="h-3.5 w-3.5" />
            {t('partition.activeTabLabel')}
          </span>
        </div>
      </div>

      {/* Property selector (when launched from sidebar or changing target) */}
      <div className="rounded-2xl border border-line bg-card p-4 shadow-xs flex flex-wrap items-end gap-4">
        <div className="min-w-64 flex-1">
          <label className="mb-1 block text-xs font-semibold text-ink">
            <Building2 className="mr-1 inline h-3.5 w-3.5 text-brand-700" />
            {t('mutations.selectPropertyLabel')}
          </label>
          <select
            value={activePropertyId}
            onChange={(e) => {
              setCompletedResult(null);
              setSelectedPropertyId(e.target.value);
            }}
            className="h-10 w-full rounded-lg border border-line bg-white px-3 text-xs outline-none focus:border-brand-600"
          >
            <option value="">{t('mutations.selectPropertyPlaceholder')}</option>
            {activeProps.map((p) => (
              <option key={p.id} value={p.id}>
                {p.wardNo} / {p.propertyNo} — {p.primaryOwnerNameMr || p.occupierName || '—'} ({p.totalAreaSqFt} sq.ft)
              </option>
            ))}
          </select>
        </div>
      </div>

      {completedResult ? (
        <div className="rounded-2xl border border-emerald-300 bg-emerald-50/70 p-6 space-y-4">
          <div className="flex items-start gap-3">
            <CheckCircle2 className="h-7 w-7 shrink-0 text-emerald-700" />
            <div>
              <h2 className="text-lg font-extrabold text-emerald-950">
                {t('partition.successTitle')}
              </h2>
              <p className="mt-1 text-xs text-emerald-900">
                {t('partition.successSubtitle', {
                  originalNo: completedResult.originalParentPropertyNo,
                  archivedNo: completedResult.archivedParentPropertyNo,
                  appNo: completedResult.applicationNo,
                  count: completedResult.childProperties.length,
                })}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
            {completedResult.childProperties.map((child) => (
              <div
                key={child.propertyId}
                className="rounded-xl border border-emerald-200 bg-white p-4 space-y-2 shadow-2xs"
              >
                <div className="flex items-center justify-between">
                  <span className="font-mono text-sm font-extrabold text-brand-700">
                    {child.wardNo} / {child.propertyNo}
                  </span>
                  <span className="rounded-full bg-emerald-100 px-2 py-0.5 font-mono text-[11px] font-bold text-emerald-900">
                    ₹{child.totalAnnualTax.toLocaleString('en-IN')}/yr
                  </span>
                </div>
                <p className="text-xs font-bold text-ink">{child.primaryOwnerNameMr || '—'}</p>
                <p className="font-mono text-[11px] text-muted">
                  {child.totalAreaSqFt.toFixed(2)} sq.ft ({child.areaSqM.toFixed(2)} m²)
                </p>
                <div className="flex items-center gap-2 pt-1">
                  <Link
                    to={`/namuna-8/view/${child.propertyId}`}
                    className="rounded-md bg-brand-700 px-2.5 py-1 text-[11px] font-bold text-white hover:bg-brand-800"
                  >
                    {t('properties.view360Btn')}
                  </Link>
                  <Link
                    to={`/namuna-8/print/${child.propertyId}`}
                    className="inline-flex items-center gap-1 rounded-md border border-line bg-white px-2.5 py-1 text-[11px] font-semibold text-ink hover:bg-slate-50"
                  >
                    <Printer className="h-3 w-3" />
                    {t('properties.printExtractBtn')}
                  </Link>
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : eligibilityQuery.isLoading ? (
        <div className="rounded-2xl border border-line bg-card p-8 text-center text-xs text-muted">
          {t('partition.loadingLabel')}
        </div>
      ) : eligibilityQuery.data && user?.activeTenantId ? (
        <PartitionWizardEditor
          key={eligibilityQuery.data.propertyId}
          eligibility={eligibilityQuery.data}
          canApprovePartition={canApprovePartition}
          tenantId={user.activeTenantId}
          onSuccess={(res) => setCompletedResult(res)}
        />
      ) : activePropertyId && eligibilityQuery.isError ? (
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-6 text-xs font-semibold text-rose-900">
          {extractProblemValidation(eligibilityQuery.error, t('citizens.loadFailed')).summary}
        </div>
      ) : (
        <div className="rounded-2xl border border-dashed border-line bg-card p-10 text-center text-xs text-muted">
          {t('partition.selectPrompt')}
        </div>
      )}
    </div>
  );
}
