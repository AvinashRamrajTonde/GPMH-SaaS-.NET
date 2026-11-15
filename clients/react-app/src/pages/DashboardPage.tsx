import { Calculator, FileSpreadsheet, Landmark, ReceiptText } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const SQFT_TO_SQM = 0.09290304;
const SLAB_RATES = [
  { maxSqFt: 300, total: 180 },
  { maxSqFt: 700, total: 220 },
  { maxSqFt: Number.POSITIVE_INFINITY, total: 300 },
];

function resolveDepreciationFactor(age: number): number {
  if (age <= 2) return 1.0;
  if (age <= 5) return 0.95;
  if (age <= 10) return 0.85;
  if (age <= 20) return 0.75;
  if (age <= 30) return 0.6;
  if (age <= 40) return 0.45;
  if (age <= 50) return 0.35;
  if (age <= 60) return 0.25;
  return 0.15;
}

export function DashboardPage() {
  const { t } = useTranslation();

  // Seeded with Chincholi Khurd golden vector defaults
  const [lengthFt, setLengthFt] = useState(15);
  const [widthFt, setWidthFt] = useState(15);
  const [landRate, setLandRate] = useState(760);
  const [rrRate, setRrRate] = useState(5852);
  const [ageYears, setAgeYears] = useState(5);
  const [taxRate, setTaxRate] = useState(0.5);

  const preview = useMemo(() => {
    const areaSqFt = Math.max(0, lengthFt) * Math.max(0, widthFt);
    const areaSqm = Math.round(areaSqFt * SQFT_TO_SQM * 100) / 100;
    const factor = resolveDepreciationFactor(Math.max(0, ageYears));
    const capitalValue = areaSqm * landRate + areaSqm * rrRate * factor;
    const rawBuildingTax = (capitalValue * 1.0 * taxRate) / 1000;
    const buildingTax = Math.round(rawBuildingTax);
    const slabTax =
      areaSqFt === 0 ? 0 : (SLAB_RATES.find((s) => areaSqFt <= s.maxSqFt)?.total ?? 300);

    return {
      areaSqFt,
      areaSqm,
      factorPct: Math.round(factor * 100),
      buildingTax,
      slabTax,
      totalTax: buildingTax + slabTax,
    };
  }, [lengthFt, widthFt, landRate, rrRate, ageYears, taxRate]);

  const stages = [
    { code: 'Namuna 8', desc: '29-Col Assessment & Sub-Division (±0.1%, Zero Dues)', icon: FileSpreadsheet },
    { code: 'Namuna 9 / 9-Ka', desc: '15-Day Notice, 10% Rebate & NPCI UPI QR Bills', icon: Landmark },
    { code: 'Namuna 10 & 11', desc: 'Strict FIFO Receipt Allocation & Dual A4 Landscape PDF', icon: ReceiptText },
    { code: 'Namuna 5-C & 6', desc: 'Same-Tx General Cash Book & Cancellation Reversals', icon: Calculator },
  ];

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">{t('dashboard.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t('dashboard.lead')}</p>

        <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {stages.map((item) => {
            const Icon = item.icon;
            return (
              <div
                key={item.code}
                className="rounded-xl border border-slate-200 bg-slate-50/70 p-4"
              >
                <div className="flex items-center gap-2 text-emerald-700">
                  <Icon className="h-4 w-4" />
                  <span className="text-sm font-semibold">{item.code}</span>
                </div>
                <p className="mt-2 text-xs text-slate-600">{item.desc}</p>
              </div>
            );
          })}
        </div>
      </section>

      <section className="rounded-2xl border border-emerald-200 bg-white p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-bold text-slate-900">{t('dashboard.taxPreviewTitle')}</h2>
          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800">
            ADR-016 Locked
          </span>
        </div>
        <p className="mt-1 text-xs text-emerald-800">{t('dashboard.goldenHint')}</p>

        <div className="mt-5 grid gap-4 sm:grid-cols-3 lg:grid-cols-6">
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.lengthFt')}
            <input
              type="number"
              min={0}
              value={lengthFt}
              onChange={(e) => setLengthFt(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.widthFt')}
            <input
              type="number"
              min={0}
              value={widthFt}
              onChange={(e) => setWidthFt(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.landRate')}
            <input
              type="number"
              min={0}
              value={landRate}
              onChange={(e) => setLandRate(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.rrRate')}
            <input
              type="number"
              min={0}
              value={rrRate}
              onChange={(e) => setRrRate(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.buildingAge')}
            <input
              type="number"
              min={0}
              value={ageYears}
              onChange={(e) => setAgeYears(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
          <label className="text-xs font-medium text-slate-700">
            {t('dashboard.taxRate')}
            <input
              type="number"
              step="0.1"
              min={0}
              value={taxRate}
              onChange={(e) => setTaxRate(Number(e.target.value))}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>
        </div>

        <div className="mt-6 grid gap-4 rounded-xl bg-slate-900 p-4 text-white sm:grid-cols-4">
          <div>
            <p className="text-xs text-slate-400">Area / Retention</p>
            <p className="mt-1 text-lg font-bold">
              {preview.areaSqm.toFixed(2)} sq.m ({preview.factorPct}%)
            </p>
          </div>
          <div>
            <p className="text-xs text-slate-400">{t('dashboard.buildingTax')}</p>
            <p className="mt-1 text-lg font-bold text-emerald-300">₹ {preview.buildingTax}</p>
          </div>
          <div>
            <p className="text-xs text-slate-400">{t('dashboard.slabTax')}</p>
            <p className="mt-1 text-lg font-bold">₹ {preview.slabTax}</p>
          </div>
          <div>
            <p className="text-xs text-slate-400">{t('dashboard.totalTax')}</p>
            <p className="mt-1 text-2xl font-extrabold text-amber-300">₹ {preview.totalTax}</p>
          </div>
        </div>
      </section>
    </div>
  );
}

