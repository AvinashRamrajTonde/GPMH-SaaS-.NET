import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeft,
  Building2,
  CheckCircle2,
  Download,
  ExternalLink,
  FileSpreadsheet,
  Layers,
  Printer,
  QrCode,
  ShieldCheck,
} from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { QrMatrixSvg } from '../../components/QrMatrixSvg';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import { namuna8Api } from '../../services/namuna8Api';
import {
  type DigitalNameplateItemDto,
  type Namuna8ExtractPreviewDto,
  printApi,
} from '../../services/printApi';
import { formatInr } from '../../utils/formatters';

type PrintTab = 'extract' | 'nameplate';
type PlateTheme = 'navy' | 'blackgold' | 'green';

function sanitizeIds(ids: string[]): string[] | undefined {
  return ids.length > 0 ? ids : undefined;
}

export function Namuna8PrintPage() {
  const { id: paramId } = useParams<{ id?: string }>();
  const [searchParams] = useSearchParams();
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();

  const initialPropertyId = paramId || searchParams.get('propertyId') || '';
  const initialWard = searchParams.get('wardNo') || '';
  const initialTab: PrintTab = searchParams.get('tab') === 'nameplate' ? 'nameplate' : 'extract';

  const [activeTab, setActiveTab] = useState<PrintTab>(initialTab);
  const [wardFilter, setWardFilter] = useState(initialWard);
  const [selectedPropertyId, setSelectedPropertyId] = useState(initialPropertyId);
  const [selectedBatchIds, setSelectedBatchIds] = useState<string[]>([]);
  const [plateLayout, setPlateLayout] = useState<'bulk' | 'single'>(
    initialPropertyId ? 'single' : 'bulk',
  );
  const [plateTheme, setPlateTheme] = useState<PlateTheme>('navy');
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor', 'Auditor'].includes(
        user.role,
      ),
  );

  const propertiesQuery = useQuery({
    queryKey: ['print-properties-list', user?.activeTenantId],
    queryFn: namuna8Api.listProperties,
    enabled: canRead,
  });

  const distinctWards = useMemo(() => {
    const wards = new Set<string>();
    for (const p of propertiesQuery.data ?? []) {
      if (p.wardNo) wards.add(p.wardNo);
    }
    return Array.from(wards).sort((a, b) => a.localeCompare(b));
  }, [propertiesQuery.data]);

  const filteredPropsForSelector = useMemo(() => {
    const all = propertiesQuery.data ?? [];
    if (!wardFilter) return all;
    return all.filter((p) => p.wardNo === wardFilter);
  }, [propertiesQuery.data, wardFilter]);

  const filterParams = useMemo(
    () => ({
      propertyId: selectedPropertyId || undefined,
      wardNo: selectedPropertyId ? undefined : wardFilter || undefined,
      propertyIds: selectedPropertyId ? undefined : sanitizeIds(selectedBatchIds),
    }),
    [selectedPropertyId, wardFilter, selectedBatchIds],
  );

  const extractsQuery = useQuery({
    queryKey: ['namuna8-extract-preview', user?.activeTenantId, filterParams],
    queryFn: () => printApi.previewExtracts(filterParams),
    enabled: canRead && activeTab === 'extract',
  });

  const nameplatesQuery = useQuery({
    queryKey: ['namuna8-nameplates-preview', user?.activeTenantId, filterParams],
    queryFn: () => printApi.previewNameplates(filterParams),
    enabled: canRead && activeTab === 'nameplate',
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

  const toggleBatchId = (propId: string) => {
    setSelectedPropertyId('');
    setSelectedBatchIds((prev) =>
      prev.includes(propId) ? prev.filter((x) => x !== propId) : [...prev, propId],
    );
  };

  const handleDownloadPdf = async () => {
    setDownloading(true);
    setDownloadError(null);
    try {
      if (activeTab === 'extract') {
        if (filterParams.propertyId) {
          await printApi.downloadSingleExtractPdf(filterParams.propertyId);
        } else {
          await printApi.downloadBatchExtractPdf({
            wardNo: filterParams.wardNo,
            propertyIds: filterParams.propertyIds,
          });
        }
      } else {
        await printApi.downloadNameplatesPdf({
          ...filterParams,
          layout: plateLayout,
        });
      }
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setDownloadError(err.problem.detail || err.message);
      } else {
        setDownloadError(t('print.downloadFailed'));
      }
    } finally {
      setDownloading(false);
    }
  };

  const extracts = extractsQuery.data ?? [];
  const plates = nameplatesQuery.data ?? [];

  return (
    <div className="space-y-5 text-xs">
      {/* Header & Actions */}
      <section className="rounded-2xl border border-line bg-card p-5 shadow-xs space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <Link
              to="/properties"
              className="inline-flex items-center gap-1 text-[11px] font-bold text-brand-700 hover:underline"
            >
              <ArrowLeft className="h-3 w-3" />
              {t('properties.backToHub')}
            </Link>
            <div className="flex items-center gap-2">
              <Printer className="h-5 w-5 text-brand-700" />
              <h1 className="text-xl font-extrabold text-ink">{t('print.title')}</h1>
            </div>
            <p className="text-xs text-muted">{t('print.subtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex rounded-xl border border-line bg-surface p-1">
              <button
                type="button"
                onClick={() => setActiveTab('extract')}
                className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 font-bold transition ${
                  activeTab === 'extract'
                    ? 'bg-brand-700 text-white shadow-xs'
                    : 'text-slate-700 hover:bg-white'
                }`}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" />
                {t('print.tabExtract')}
              </button>
              <button
                type="button"
                onClick={() => setActiveTab('nameplate')}
                className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 font-bold transition ${
                  activeTab === 'nameplate'
                    ? 'bg-brand-700 text-white shadow-xs'
                    : 'text-slate-700 hover:bg-white'
                }`}
              >
                <QrCode className="h-3.5 w-3.5" />
                {t('print.tabNameplate')}
              </button>
            </div>

            <button
              type="button"
              onClick={handleDownloadPdf}
              disabled={
                downloading ||
                (activeTab === 'extract' ? extracts.length === 0 : plates.length === 0)
              }
              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white shadow-xs hover:bg-brand-800 disabled:opacity-50"
            >
              <Download className="h-3.5 w-3.5" />
              {downloading
                ? t('print.generatingPdf')
                : activeTab === 'extract'
                  ? t('print.downloadExtractBtn')
                  : t('print.downloadNameplateBtn')}
            </button>
          </div>
        </div>

        {/* Scope Filters Toolbar */}
        <div className="grid gap-3 border-t border-line pt-4 sm:grid-cols-2 lg:grid-cols-4">
          <label className="space-y-1">
            <span className="font-semibold text-slate-700">{t('print.wardFilterLabel')}</span>
            <select
              value={wardFilter}
              onChange={(e) => {
                setWardFilter(e.target.value);
                setSelectedPropertyId('');
                setSelectedBatchIds([]);
              }}
              className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            >
              <option value="">{t('print.allWardsOption')}</option>
              {distinctWards.map((w) => (
                <option key={w} value={w}>
                  {t('properties.wardNo')} {w}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1">
            <span className="font-semibold text-slate-700">{t('print.propertyFilterLabel')}</span>
            <select
              value={selectedPropertyId}
              onChange={(e) => {
                setSelectedPropertyId(e.target.value);
                setSelectedBatchIds([]);
              }}
              className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            >
              <option value="">{t('print.allFilteredBatchOption')}</option>
              {filteredPropsForSelector.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.wardNo}/{p.propertyNo} — {p.primaryOwnerNameMr || p.primaryOwnerNameEn || '-'}
                </option>
              ))}
            </select>
          </label>

          {activeTab === 'nameplate' && (
            <>
              <label className="space-y-1">
                <span className="font-semibold text-slate-700">{t('print.plateLayoutLabel')}</span>
                <select
                  value={plateLayout}
                  onChange={(e) => setPlateLayout(e.target.value as 'bulk' | 'single')}
                  className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
                >
                  <option value="bulk">{t('print.layoutBulk6In1')}</option>
                  <option value="single">{t('print.layoutSinglePlaque')}</option>
                </select>
              </label>

              <label className="space-y-1">
                <span className="font-semibold text-slate-700">{t('print.plateThemeLabel')}</span>
                <select
                  value={plateTheme}
                  onChange={(e) => setPlateTheme(e.target.value as PlateTheme)}
                  className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
                >
                  <option value="navy">{t('print.themeNavy')}</option>
                  <option value="blackgold">{t('print.themeBlackGold')}</option>
                  <option value="green">{t('print.themeGreen')}</option>
                </select>
              </label>
            </>
          )}

          {activeTab === 'extract' && !selectedPropertyId && filteredPropsForSelector.length > 0 && (
            <div className="sm:col-span-2 space-y-1">
              <span className="font-semibold text-slate-700">
                {t('print.multiSelectHint', { count: selectedBatchIds.length })}
              </span>
              <div className="flex max-h-20 flex-wrap gap-1.5 overflow-y-auto rounded-xl border border-slate-200 bg-surface p-2">
                {filteredPropsForSelector.map((p) => {
                  const checked = selectedBatchIds.includes(p.id);
                  return (
                    <button
                      key={p.id}
                      type="button"
                      onClick={() => toggleBatchId(p.id)}
                      className={`rounded-lg border px-2 py-0.5 text-[11px] font-semibold transition ${
                        checked
                          ? 'border-brand-700 bg-brand-700 text-white'
                          : 'border-slate-200 bg-white text-slate-700 hover:border-brand-300'
                      }`}
                    >
                      {p.wardNo}/{p.propertyNo}
                    </button>
                  );
                })}
              </div>
            </div>
          )}
        </div>

        {downloadError && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-xs font-semibold text-rose-800">
            {downloadError}
          </div>
        )}
      </section>

      {/* Content Area */}
      {activeTab === 'extract' ? (
        <Namuna8ExtractPreviewSection extracts={extracts} isLoading={extractsQuery.isLoading} />
      ) : (
        <DigitalNameplatesPreviewSection
          plates={plates}
          isLoading={nameplatesQuery.isLoading}
          layout={plateLayout}
          theme={plateTheme}
        />
      )}
    </div>
  );
}

function Namuna8ExtractPreviewSection({
  extracts,
  isLoading,
}: {
  extracts: Namuna8ExtractPreviewDto[];
  isLoading: boolean;
}) {
  const { t } = useTranslation();
  if (isLoading) {
    return <div className="h-64 animate-pulse rounded-2xl bg-slate-200" />;
  }

  if (extracts.length === 0) {
    return (
      <div className="rounded-2xl border border-dashed border-line bg-card p-10 text-center space-y-2">
        <Building2 className="mx-auto h-7 w-7 text-slate-400" />
        <p className="font-bold text-slate-700">{t('print.emptyPreviewTitle')}</p>
        <p className="text-muted">{t('print.emptyPreviewDesc')}</p>
      </div>
    );
  }

  const head = extracts[0];
  const isSingle = extracts.length === 1;

  return (
    <section className="rounded-2xl border border-slate-900 bg-white p-5 shadow-xs space-y-4 text-slate-900">
      <p className="text-center text-[10px] text-slate-500">
        (टीप :- सदर मालमत्ता आकारणी नक्कल केवळ ग्रामपंचायत कर आकारणी बाबत असून इतर कोणत्याही हक्काबाबत हा दस्त कायदेशीर पुरावा सांगत नाही, नमूद मोजमापे तांत्रिक दृष्ट्या प्रमाणित तथा प्रमाणबद्ध केलेली नाहीत)
      </p>

      {/* Top Official Banner matching 08 template */}
      <div className="grid border border-slate-900 md:grid-cols-4">
        <div className="border-b border-slate-900 p-3 md:border-b-0 md:border-r space-y-0.5 text-[11px]">
          <p className="font-bold text-brand-700">महाराष्ट्र शासन • ग्रामविकास विभाग</p>
          <p className="font-extrabold text-sm">ग्रामपंचायत: {head.panchayatNameMr}</p>
          <p>
            ता.: {head.tehsilName} | जि.: {head.districtName}
          </p>
          <p className="text-[10px] text-slate-500">
            LGD: {head.lgdCode} {head.pincode ? `• ${head.pincode}` : ''}
          </p>
        </div>

        <div className="border-b border-slate-900 p-3 md:col-span-2 md:border-b-0 md:border-r flex flex-col items-center justify-center text-center space-y-1">
          <p className="font-bold text-brand-700">महाराष्ट्र ग्रामपंचायत लेखा संहिता २०११ — नियम ३२ (१)</p>
          <h2 className="text-base font-extrabold">नमुना ८ — कर आकारणी नोंदवही उतारा</h2>
          <p className="text-[11px]">
            कालावधी: {head.ratePeriodName} • आर्थिक वर्ष: <b>{head.assessmentYearLabel}</b>
          </p>
          <p className="text-[10px] font-semibold text-slate-600">
            दस्तऐवज क्र.: {head.documentRefNo} • दिनांक: {head.issuedDateDisplay}
          </p>
        </div>

        <div className="flex flex-col items-center justify-center gap-1 p-3">
          {head.verifyQrSvg && (
            <img
              src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(head.verifyQrSvg)}`}
              alt="Namuna 8 Extract Verify QR"
              className="h-20 w-20 border border-slate-200 rounded bg-white p-0.5"
            />
          )}
          <Link
            to={`/verify/extract/${encodeURIComponent(head.verifyToken)}`}
            target="_blank"
            className="inline-flex items-center gap-1 text-[10px] font-bold text-brand-700 hover:underline"
          >
            {t('print.openVerifyPageLink')}
            <ExternalLink className="h-3 w-3" />
          </Link>
        </div>
      </div>

      {/* 4 Summary Boxes when single property */}
      {isSingle && (
        <div className="grid gap-2 border border-slate-900 p-3 sm:grid-cols-2 lg:grid-cols-4 text-[11px]">
          <div className="space-y-0.5">
            <p className="font-bold text-brand-700">१. मालमत्ता व धारक तपशील</p>
            <p>
              <b>मिळकत क्र.:</b> {head.propertyNo} (प्रभाग {head.wardNo})
            </p>
            <p>
              <b>मालक:</b> {head.ownersSummaryMr}
            </p>
            <p>
              <b>भोगवटादार:</b> {head.occupierName}
            </p>
            <p className="text-[10px] text-slate-500">
              आधार: {head.primaryOwnerAadhaarMasked ?? '-'} | मो.:{' '}
              {head.primaryOwnerMobileMasked ?? '-'}
            </p>
          </div>
          <div className="space-y-0.5">
            <p className="font-bold text-brand-700">२. बांधकाम व क्षेत्रफळ तपशील</p>
            <p>
              <b>रस्ता / गल्ली:</b> {head.streetOrLaneName || '-'}
            </p>
            <p>
              <b>वर्णन:</b> {head.propertyDescriptionMr}
            </p>
            <p>
              <b>बांधकाम वर्ष:</b> {head.constructionYearsDisplay}
            </p>
            <p className="font-bold">
              {head.totalAreaSqFt.toFixed(2)} sq.ft ({head.totalAreaSqM.toFixed(2)} sq.m)
            </p>
          </div>
          <div className="space-y-0.5">
            <p className="font-bold text-brand-700">३. नागरी सुविधा व कर</p>
            <p>
              <b>नळ जोडणी:</b> {head.waterConnectionLabelMr}
            </p>
            <p>
              <b>कर माफी:</b> {head.isTaxExempt ? t('properties.yes') : t('properties.no')}
            </p>
            <p>
              <b>स्थिती:</b> {head.status}
            </p>
            <p className="font-extrabold text-brand-800 text-xs">
              एकूण चालू कर: {formatInr(head.totalAnnualTax)}
            </p>
          </div>
          <div className="space-y-0.5">
            <p className="font-bold text-brand-700">४. चतुर्सिमा</p>
            <p>
              <b>पूर्व:</b> {head.boundaryEast || '-'}
            </p>
            <p>
              <b>पश्चिम:</b> {head.boundaryWest || '-'}
            </p>
            <p>
              <b>उत्तर:</b> {head.boundaryNorth || '-'}
            </p>
            <p>
              <b>दक्षिण:</b> {head.boundarySouth || '-'}
            </p>
          </div>
        </div>
      )}

      {/* 29-Column Statutory Register Table */}
      <div className="overflow-x-auto border border-slate-900">
        <table className="min-w-[1280px] w-full border-collapse text-[10px]">
          <thead className="bg-slate-100 text-center font-bold">
            <tr>
              <th rowSpan={2} className="border border-slate-900 p-1">१. अ.क्र.</th>
              <th rowSpan={2} className="border border-slate-900 p-1">२. रस्ता / गल्ली</th>
              <th rowSpan={2} className="border border-slate-900 p-1">३. गट / सर्व्हे</th>
              <th rowSpan={2} className="border border-slate-900 p-1">४. मिळकत क्र.</th>
              <th rowSpan={2} className="border border-slate-900 p-1">५. मालमत्ता धारक</th>
              <th rowSpan={2} className="border border-slate-900 p-1">६. भोगवटादार</th>
              <th rowSpan={2} className="border border-slate-900 p-1">७. वर्णन</th>
              <th rowSpan={2} className="border border-slate-900 p-1">८. बांधकाम वर्ष</th>
              <th rowSpan={2} className="border border-slate-900 p-1">९. क्षेत्रफळ चौ.फू. (चौ.मी.)</th>
              <th colSpan={3} className="border border-slate-900 p-1">रेडीरेकनर दर (रू.)</th>
              <th rowSpan={2} className="border border-slate-900 p-1">१३. घसारा%</th>
              <th rowSpan={2} className="border border-slate-900 p-1">१४. भारांक</th>
              <th rowSpan={2} className="border border-slate-900 p-1">१५. भांडवली मूल्य</th>
              <th rowSpan={2} className="border border-slate-900 p-1">१६. कर दर</th>
              <th colSpan={7} className="border border-slate-900 p-1">कराची रक्कम (रुपये)</th>
              <th colSpan={5} className="border border-slate-900 p-1">फेरफार / अपिल (रुपये)</th>
              <th rowSpan={2} className="border border-slate-900 p-1">२९. शेरा / आदेश</th>
            </tr>
            <tr>
              <th className="border border-slate-900 p-1">१०. जमीन</th>
              <th className="border border-slate-900 p-1">११. इमारत</th>
              <th className="border border-slate-900 p-1">१२. बांधकाम</th>
              <th className="border border-slate-900 p-1">१७. घरपट्टी</th>
              <th className="border border-slate-900 p-1">१८. दिवाबत्ती</th>
              <th className="border border-slate-900 p-1">१९. आरोग्य</th>
              <th className="border border-slate-900 p-1">२०. पाणीपट्टी</th>
              <th className="border border-slate-900 p-1">२१. सफाई</th>
              <th className="border border-slate-900 p-1">२२. शौ.कर</th>
              <th className="border border-slate-900 bg-amber-100 p-1">२३. एकूण कर</th>
              <th className="border border-slate-900 p-1">२४. घर</th>
              <th className="border border-slate-900 p-1">२५. दिवा</th>
              <th className="border border-slate-900 p-1">२६. आरो</th>
              <th className="border border-slate-900 p-1">२७. पाणी</th>
              <th className="border border-slate-900 p-1">२८. एकूण</th>
            </tr>
            <tr className="bg-slate-200 font-extrabold">
              {Array.from({ length: 29 }, (_, idx) => (
                <td
                  key={idx}
                  className={`border border-slate-900 py-0.5 ${idx + 1 === 23 ? 'bg-amber-200' : ''}`}
                >
                  {idx + 1}
                </td>
              ))}
            </tr>
          </thead>
          <tbody>
            {extracts.map((r) => (
              <tr key={r.propertyId} className="hover:bg-slate-50">
                <td className="border border-slate-900 p-1 text-center">{r.srNo}</td>
                <td className="border border-slate-900 p-1">{r.streetOrLaneName || '-'}</td>
                <td className="border border-slate-900 p-1 text-center">{r.citySurveyOrGutNo || '-'}</td>
                <td className="border border-slate-900 p-1 text-center font-bold">
                  {r.wardNo}/{r.propertyNo}
                </td>
                <td className="border border-slate-900 p-1 font-bold">{r.ownersSummaryMr}</td>
                <td className="border border-slate-900 p-1">{r.occupierName || '-'}</td>
                <td className="border border-slate-900 p-1">{r.propertyDescriptionMr}</td>
                <td className="border border-slate-900 p-1 text-center">{r.constructionYearsDisplay}</td>
                <td className="border border-slate-900 p-1 text-right">
                  {r.totalAreaSqFt.toFixed(2)} ({r.totalAreaSqM.toFixed(2)})
                </td>
                <td className="border border-slate-900 p-1 text-right">{r.openLandRrRate || '-'}</td>
                <td className="border border-slate-900 p-1 text-right">{r.buildingLandRate || '-'}</td>
                <td className="border border-slate-900 p-1 text-right">{r.constructionRrRate || '-'}</td>
                <td className="border border-slate-900 p-1 text-center">{r.depreciationPercentDisplay}</td>
                <td className="border border-slate-900 p-1 text-center">{r.usageWeightageDisplay}</td>
                <td className="border border-slate-900 p-1 text-right font-bold">
                  {Math.round(r.totalCapitalValue).toLocaleString('en-IN')}
                </td>
                <td className="border border-slate-900 p-1 text-center">{r.taxRateDisplay}</td>
                <td className="border border-slate-900 p-1 text-right">{Math.round(r.buildingTaxComponent)}</td>
                <td className="border border-slate-900 p-1 text-right">{Math.round(r.lightingTax)}</td>
                <td className="border border-slate-900 p-1 text-right">{Math.round(r.healthTax)}</td>
                <td className="border border-slate-900 p-1 text-right">{Math.round(r.waterTax)}</td>
                <td className="border border-slate-900 p-1 text-right">{Math.round(r.sanitationTax)}</td>
                <td className="border border-slate-900 p-1 text-right">{Math.round(r.toiletTax)}</td>
                <td className="border border-slate-900 bg-amber-50 p-1 text-right font-extrabold">
                  {Math.round(r.totalAnnualTax)}
                </td>
                <td className="border border-slate-900 p-1 text-right">{r.revisionHouseTax ?? '-'}</td>
                <td className="border border-slate-900 p-1 text-right">{r.revisionLightingTax ?? '-'}</td>
                <td className="border border-slate-900 p-1 text-right">{r.revisionHealthTax ?? '-'}</td>
                <td className="border border-slate-900 p-1 text-right">{r.revisionWaterTax ?? '-'}</td>
                <td className="border border-slate-900 p-1 text-right">{r.revisionTotalTax ?? '-'}</td>
                <td className="border border-slate-900 p-1">{r.remarks}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function DigitalNameplatesPreviewSection({
  plates,
  isLoading,
  layout,
  theme,
}: {
  plates: DigitalNameplateItemDto[];
  isLoading: boolean;
  layout: 'bulk' | 'single';
  theme: PlateTheme;
}) {
  const { t } = useTranslation();
  if (isLoading) {
    return <div className="h-64 animate-pulse rounded-2xl bg-slate-200" />;
  }

  if (plates.length === 0) {
    return (
      <div className="rounded-2xl border border-dashed border-line bg-card p-10 text-center space-y-2">
        <Layers className="mx-auto h-7 w-7 text-slate-400" />
        <p className="font-bold text-slate-700">{t('print.emptyPreviewTitle')}</p>
        <p className="text-muted">{t('print.emptyPreviewDesc')}</p>
      </div>
    );
  }

  const themeClass =
    theme === 'blackgold'
      ? 'border-amber-400 bg-slate-950 text-amber-100'
      : theme === 'green'
        ? 'border-emerald-700 bg-emerald-950 text-emerald-50'
        : 'border-brand-800 bg-slate-900 text-white';

  return (
    <div
      className={
        layout === 'single' ? 'space-y-6' : 'grid gap-4 md:grid-cols-2 xl:grid-cols-3'
      }
    >
      {plates.map((plate) => (
        <article
          key={plate.propertyId}
          className={`relative rounded-2xl border-4 p-4 shadow-md space-y-3 ${themeClass}`}
        >
          {/* Mounting Rivets */}
          <span className="absolute left-2 top-2 text-[9px] opacity-50">✕</span>
          <span className="absolute right-2 top-2 text-[9px] opacity-50">✕</span>
          <span className="absolute bottom-2 left-2 text-[9px] opacity-50">✕</span>
          <span className="absolute bottom-2 right-2 text-[9px] opacity-50">✕</span>

          <div className="border-b border-white/20 pb-2 text-center space-y-0.5">
            <p className="text-[10px] font-semibold text-amber-300">
              महाराष्ट्र शासन — ग्रामविकास विभाग
            </p>
            <h3 className="text-sm font-extrabold">ग्रामपंचायत {plate.panchayatNameMr}</h3>
            <p className="text-[10px] opacity-80">
              ता. {plate.tehsilName}, जि. {plate.districtName} • LGD: {plate.lgdCode}
            </p>
          </div>

          <div className="flex flex-wrap items-start gap-4">
            <div className="flex flex-col items-center gap-1.5 rounded-xl bg-white p-2 text-slate-900">
              {plate.verifyQrSvg && (
                <img
                  src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(plate.verifyQrSvg)}`}
                  alt={`Verify QR ${plate.propertyNo}`}
                  className="h-24 w-24"
                />
              )}
              <Link
                to={`/verify/extract/${encodeURIComponent(plate.verifyToken)}`}
                target="_blank"
                className="inline-flex items-center gap-1 text-[10px] font-bold text-brand-700 hover:underline"
              >
                <ShieldCheck className="h-3 w-3" />
                {t('print.scanVerifyLabel')}
              </Link>

              {plate.upiPayUri && (
                <div className="border-t border-slate-200 pt-1.5 flex flex-col items-center">
                  <QrMatrixSvg value={plate.upiPayUri} sizePx={68} />
                  <span className="text-[9px] font-bold text-blue-700">UPI: {plate.upiId}</span>
                </div>
              )}
            </div>

            <div className="flex-1 min-w-[180px] space-y-2">
              <div className="flex flex-wrap gap-2">
                <div className="rounded-lg bg-white/10 px-2.5 py-1">
                  <span className="block text-[9px] opacity-70">{t('properties.wardNo')}</span>
                  <span className="font-extrabold text-sm">{plate.wardNo}</span>
                </div>
                <div className="rounded-lg bg-white/10 px-2.5 py-1">
                  <span className="block text-[9px] opacity-70">{t('properties.propertyNo')}</span>
                  <span className="font-extrabold text-sm">{plate.propertyNo}</span>
                </div>
              </div>

              <div>
                <span className="block text-[10px] opacity-75">मिळकतधारक (Owner)</span>
                <p className="text-sm font-extrabold">{plate.ownerNameMr}</p>
                <p className="text-[11px] opacity-80">{plate.ownerNameEn}</p>
                {plate.coOwnerNamesMr && (
                  <p className="text-[10px] opacity-75">सहमालक: {plate.coOwnerNamesMr}</p>
                )}
              </div>

              <div className="grid grid-cols-2 gap-2 text-[11px] border-t border-white/15 pt-2">
                <div>
                  <span className="block text-[9px] opacity-70">वापर / प्रकार</span>
                  <span className="font-semibold">{plate.constructionAndUsageMr}</span>
                </div>
                <div>
                  <span className="block text-[9px] opacity-70">{t('properties.totalArea')}</span>
                  <span className="font-semibold">
                    {plate.totalAreaSqFt.toFixed(0)} sq.ft ({plate.totalAreaSqM.toFixed(2)} sq.m)
                  </span>
                </div>
              </div>

              <div className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-500/20 px-2.5 py-1 text-[11px] font-bold text-emerald-200">
                <CheckCircle2 className="h-3.5 w-3.5" />
                {plate.isTaxExempt
                  ? t('properties.exemptBadge')
                  : `${t('properties.colAnnualTax')}: ${formatInr(plate.totalAnnualTax)}`}
              </div>
            </div>
          </div>

          <div className="flex items-center justify-between border-t border-white/15 pt-2 text-[10px] opacity-80">
            <span>UID: {plate.uniqueCode}</span>
            <span>वेळेवर कर भरा, ग्रामविकास साधा!</span>
          </div>
        </article>
      ))}
    </div>
  );
}

