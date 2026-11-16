import { Calculator, Play, Plus, Sparkles, Trash2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ValidationSummary } from '../../components/ValidationSummary';
import {
  type CalculatedPropertyTaxDto,
  type ConstructionTypeMasterDto,
  formulaApi,
  type MasterFloorTaxRequestPayload,
  type UsageWeightageDto,
  type WaterTaxMasterDto,
} from '../../services/formulaApi';
import { formatInr } from '../../utils/formatters';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';

interface FormulaSimulatorCardProps {
  constructionTypes: ConstructionTypeMasterDto[];
  usageWeightages: UsageWeightageDto[];
  waterTaxes: WaterTaxMasterDto[];
}

export function FormulaSimulatorCard({
  constructionTypes,
  usageWeightages,
  waterTaxes,
}: FormulaSimulatorCardProps) {
  const { t } = useTranslation();

  const [assessmentStartYear, setAssessmentStartYear] = useState(2026);
  const [floors, setFloors] = useState<MasterFloorTaxRequestPayload[]>([
    {
      constructionTypeCode: 'HUT_MUD',
      usageCode: 'RESIDENTIAL',
      constructionStartYear: 2021,
      lengthFt: 15,
      widthFt: 15,
    },
  ]);
  const [openAreaSqFt, setOpenAreaSqFt] = useState(0);
  const [waterConnectionCode, setWaterConnectionCode] = useState('NONE');
  const [otherTax, setOtherTax] = useState(0);

  const [calculating, setCalculating] = useState(false);
  const [result, setResult] = useState<CalculatedPropertyTaxDto | null>(null);
  const [errSummary, setErrSummary] = useState<string | null>(null);
  const [errItems, setErrItems] = useState<ParsedValidationItem[]>([]);

  async function runCalculation(payload: {
    assessmentStartYear: number;
    floors: MasterFloorTaxRequestPayload[];
    openAreaSqFt: number;
    waterConnectionCode: string;
    otherTax: number;
  }) {
    setErrSummary(null);
    setErrItems([]);
    setCalculating(true);
    try {
      const data = await formulaApi.calculateFromMasters({
        ...payload,
        openPlotConstructionCode: 'OPEN_PLOT',
      });
      setResult(data);
    } catch (err) {
      const parsed = extractProblemValidation(err, t('formula.simFailed'));
      setErrSummary(parsed.summary);
      setErrItems(parsed.items);
    } finally {
      setCalculating(false);
    }
  }

  function handleLoadGoldenCase() {
    const golden = {
      assessmentStartYear: 2026,
      floors: [
        {
          constructionTypeCode: 'HUT_MUD',
          usageCode: 'RESIDENTIAL',
          constructionStartYear: 2021,
          lengthFt: 15,
          widthFt: 15,
        },
      ],
      openAreaSqFt: 0,
      waterConnectionCode: 'NONE',
      otherTax: 0,
    };
    setAssessmentStartYear(golden.assessmentStartYear);
    setFloors(golden.floors);
    setOpenAreaSqFt(golden.openAreaSqFt);
    setWaterConnectionCode(golden.waterConnectionCode);
    setOtherTax(golden.otherTax);
    void runCalculation(golden);
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    void runCalculation({
      assessmentStartYear,
      floors,
      openAreaSqFt,
      waterConnectionCode,
      otherTax,
    });
  }

  return (
    <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Calculator className="h-5 w-5 text-brand-700" />
          <div>
            <h2 className="text-base font-extrabold text-ink">{t('formula.simTitle')}</h2>
            <p className="text-xs text-muted">{t('formula.simSubtitle')}</p>
          </div>
        </div>
        <button
          type="button"
          onClick={handleLoadGoldenCase}
          className="inline-flex items-center gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-bold text-amber-900 hover:bg-amber-100"
        >
          <Sparkles className="h-3.5 w-3.5" />
          {t('formula.simGoldenBtn')}
        </button>
      </div>

      <ValidationSummary summary={errSummary} items={errItems} />

      <form onSubmit={handleSubmit} className="space-y-4 text-xs">
        <div className="grid gap-3 sm:grid-cols-4">
          <label className="space-y-1 font-bold text-slate-700">
            <span>{t('formula.simAssessmentYear')}</span>
            <input
              type="number"
              value={assessmentStartYear}
              onChange={(e) => setAssessmentStartYear(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-700">
            <span>{t('formula.simOpenAreaSqFt')}</span>
            <input
              type="number"
              min="0"
              step="0.01"
              value={openAreaSqFt}
              onChange={(e) => setOpenAreaSqFt(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
            />
          </label>
          <label className="space-y-1 font-bold text-slate-700">
            <span>{t('properties.waterConnection')}</span>
            <select
              value={waterConnectionCode}
              onChange={(e) => setWaterConnectionCode(e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
            >
              {waterTaxes.map((w) => (
                <option key={w.id} value={w.code}>
                  {w.waterTypeName} ({formatInr(w.amount)})
                </option>
              ))}
            </select>
          </label>
          <label className="space-y-1 font-bold text-slate-700">
            <span>{t('formula.simOtherTax')}</span>
            <input
              type="number"
              min="0"
              step="0.01"
              value={otherTax}
              onChange={(e) => setOtherTax(Number(e.target.value))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 mono"
            />
          </label>
        </div>

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="font-bold text-slate-700">{t('formula.simFloorsHeader')}</span>
            <button
              type="button"
              onClick={() =>
                setFloors((prev) => [
                  ...prev,
                  {
                    constructionTypeCode: constructionTypes[0]?.code ?? 'RCC_PUCCA',
                    usageCode: usageWeightages[0]?.code ?? 'RESIDENTIAL',
                    constructionStartYear: 2020,
                    lengthFt: 20,
                    widthFt: 20,
                  },
                ])
              }
              className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 font-bold text-brand-800 hover:bg-brand-100"
            >
              <Plus className="h-3 w-3" />
              {t('formula.simAddFloorBtn')}
            </button>
          </div>

          {floors.map((floor, idx) => (
            <div
              key={idx}
              className="grid gap-2 sm:grid-cols-6 items-end rounded-xl border border-line bg-surface p-3"
            >
              <label className="space-y-1 font-bold text-slate-700 sm:col-span-2">
                <span>{t('formula.colTypeName')}</span>
                <select
                  value={floor.constructionTypeCode}
                  onChange={(e) =>
                    setFloors((prev) =>
                      prev.map((f, i) =>
                        i === idx ? { ...f, constructionTypeCode: e.target.value } : f,
                      ),
                    )
                  }
                  className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5"
                >
                  {constructionTypes.map((c) => (
                    <option key={c.id} value={c.code}>
                      {c.typeName} ({c.code})
                    </option>
                  ))}
                </select>
              </label>

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.colUsageName')}</span>
                <select
                  value={floor.usageCode}
                  onChange={(e) =>
                    setFloors((prev) =>
                      prev.map((f, i) => (i === idx ? { ...f, usageCode: e.target.value } : f)),
                    )
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

              <label className="space-y-1 font-bold text-slate-700">
                <span>{t('formula.simBuiltYear')}</span>
                <input
                  type="number"
                  value={floor.constructionStartYear}
                  onChange={(e) =>
                    setFloors((prev) =>
                      prev.map((f, i) =>
                        i === idx ? { ...f, constructionStartYear: Number(e.target.value) } : f,
                      ),
                    )
                  }
                  className="w-full rounded-lg border border-slate-300 px-2.5 py-1.5 mono"
                />
              </label>

              <div className="grid grid-cols-2 gap-1.5">
                <label className="space-y-1 font-bold text-slate-700">
                  <span>{t('formula.simLengthFt')}</span>
                  <input
                    type="number"
                    min="0"
                    step="0.1"
                    value={floor.lengthFt}
                    onChange={(e) =>
                      setFloors((prev) =>
                        prev.map((f, i) => (i === idx ? { ...f, lengthFt: Number(e.target.value) } : f)),
                      )
                    }
                    className="w-full rounded-lg border border-slate-300 px-2 py-1.5 mono"
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
                      setFloors((prev) =>
                        prev.map((f, i) => (i === idx ? { ...f, widthFt: Number(e.target.value) } : f)),
                      )
                    }
                    className="w-full rounded-lg border border-slate-300 px-2 py-1.5 mono"
                  />
                </label>
              </div>

              <div className="flex justify-end">
                {floors.length > 1 && (
                  <button
                    type="button"
                    onClick={() => setFloors((prev) => prev.filter((_, i) => i !== idx))}
                    className="rounded-lg border border-rose-200 bg-rose-50 p-1.5 text-rose-700 hover:bg-rose-100"
                    aria-label={t('formula.removeRowBtn')}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={calculating}
            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 disabled:opacity-60"
          >
            <Play className="h-3.5 w-3.5" />
            {calculating ? t('onboarding.submitting') : t('formula.simCalculateBtn')}
          </button>
        </div>
      </form>

      {result && (
        <div className="rounded-xl border border-brand-200 bg-brand-50/40 p-4 space-y-3 text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-brand-200 pb-2">
            <span className="font-bold text-brand-900">
              {result.ratePeriodName} · {t('formula.taxDivisor')}: {result.taxDivisor} ·{' '}
              {t('formula.tabAreaSlabs')}: {result.appliedAreaSlabRange}
            </span>
            <span className="text-lg font-extrabold text-brand-900 mono">
              {t('formula.simTotalTax')}: {formatInr(result.calculation.totalTax)}
            </span>
          </div>

          <div className="grid gap-2 sm:grid-cols-4 lg:grid-cols-8">
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.simBuildingTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.buildingTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.simOpenLandTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.openLandTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.colLightingTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.lightingTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.colHealthTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.healthTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.colSanitationTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.sanitationTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.colToiletTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.toiletTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.tabWaterTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.waterTax)}</p>
            </div>
            <div className="rounded-lg bg-white border border-brand-100 p-2">
              <p className="text-[10px] text-muted">{t('formula.simOtherTax')}</p>
              <p className="font-bold mono text-ink">{formatInr(result.calculation.otherTax)}</p>
            </div>
          </div>

          {result.calculation.floorBreakdowns.length > 0 && (
            <div className="overflow-x-auto rounded-lg border border-brand-100 bg-white">
              <table className="w-full text-left text-[11px]">
                <thead className="bg-brand-50 text-brand-900 font-bold">
                  <tr>
                    <th className="px-2.5 py-1.5">#</th>
                    <th className="px-2.5 py-1.5 text-right">{t('dashboard.sqMUnit')}</th>
                    <th className="px-2.5 py-1.5 text-right">{t('formula.simAge')}</th>
                    <th className="px-2.5 py-1.5 text-right">{t('formula.simRetainedPct')}</th>
                    <th className="px-2.5 py-1.5 text-right">{t('formula.simLandVal')}</th>
                    <th className="px-2.5 py-1.5 text-right">{t('formula.simDepBldgVal')}</th>
                    <th className="px-2.5 py-1.5 text-right">{t('formula.simCapitalVal')}</th>
                    <th className="px-2.5 py-1.5 text-right">{t('formula.simRawTax')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {result.calculation.floorBreakdowns.map((b) => (
                    <tr key={b.floorIndex}>
                      <td className="px-2.5 py-1.5 mono">{b.floorIndex + 1}</td>
                      <td className="px-2.5 py-1.5 text-right mono">{b.areaSqm.toFixed(2)}</td>
                      <td className="px-2.5 py-1.5 text-right mono">{b.buildingAge}</td>
                      <td className="px-2.5 py-1.5 text-right mono">{b.depreciationPercentage}%</td>
                      <td className="px-2.5 py-1.5 text-right mono">{formatInr(b.landValue)}</td>
                      <td className="px-2.5 py-1.5 text-right mono">{formatInr(b.depreciatedBuildingValue)}</td>
                      <td className="px-2.5 py-1.5 text-right mono font-semibold">{formatInr(b.floorCapitalValue)}</td>
                      <td className="px-2.5 py-1.5 text-right mono font-bold">{b.rawBuildingTax.toFixed(4)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </section>
  );
}

