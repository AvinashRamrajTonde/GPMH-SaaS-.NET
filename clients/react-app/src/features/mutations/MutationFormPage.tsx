import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  ArrowRightLeft,
  Building2,
  FileUp,
  GitBranch,
  Plus,
  Search,
  Trash2,
  UserPlus,
  Users,
} from 'lucide-react';
import { useState, type ChangeEvent, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ValidationSummary } from '../../components/ValidationSummary';
import { useAuth } from '../../context/useAuth';
import {
  MUTATION_TYPES,
  mutationApi,
  type UploadedMutationAssetDto,
} from '../../services/mutationApi';
import { type CitizenDto, namuna8Api } from '../../services/namuna8Api';
import { extractProblemValidation, type ParsedValidationItem } from '../../utils/validation';
import { QuickAddCitizenModal } from '../citizens/QuickAddCitizenModal';

interface ProposedOwnerDraft {
  citizen: CitizenDto;
  sharePct: string;
  isPrimary: boolean;
}

export function MutationFormPage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();

  const [propertyId, setPropertyId] = useState(searchParams.get('propertyId') || '');
  const [mutationType, setMutationType] = useState<string>('SaleDeed');
  const [documentReferenceNo, setDocumentReferenceNo] = useState('');
  const [documentDate, setDocumentDate] = useState(new Date().toISOString().slice(0, 10));
  const [remarks, setRemarks] = useState('');

  const [citizenSearch, setCitizenSearch] = useState('');
  const [proposedOwners, setProposedOwners] = useState<ProposedOwnerDraft[]>([]);
  const [quickAddOpen, setQuickAddOpen] = useState(false);

  const [uploadedAssets, setUploadedAssets] = useState<UploadedMutationAssetDto[]>([]);
  const [uploading, setUploading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [validationSummary, setValidationSummary] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<ParsedValidationItem[]>([]);

  const canWrite = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor'].includes(user.role),
  );

  const propsListQuery = useQuery({
    queryKey: ['mutation-form-properties', user?.activeTenantId],
    queryFn: namuna8Api.listProperties,
    enabled: canWrite,
  });

  const propDetailQuery = useQuery({
    queryKey: ['mutation-target-property', propertyId],
    queryFn: () => namuna8Api.getPropertyDetail(propertyId),
    enabled: canWrite && Boolean(propertyId),
  });

  const citizensQuery = useQuery({
    queryKey: ['mutation-citizen-picker', user?.activeTenantId, citizenSearch],
    queryFn: () => namuna8Api.listCitizens({ search: citizenSearch || undefined, pageSize: 15 }),
    enabled: canWrite,
  });

  if (!canWrite) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <p>{t('properties.signInRequired')}</p>
        <Link to="/login" className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white">
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  const activeProps = (propsListQuery.data ?? []).filter((p) => p.status !== 'Partitioned');
  const selectedProp = propDetailQuery.data;
  const shareTotal = proposedOwners.reduce((acc, o) => acc + (Number(o.sharePct) || 0), 0);

  const addProposedOwner = (citizen: CitizenDto) => {
    setProposedOwners((prev) => {
      if (prev.some((x) => x.citizen.id === citizen.id)) return prev;
      const next = [
        ...prev,
        {
          citizen,
          sharePct: prev.length === 0 ? '100' : '0',
          isPrimary: prev.length === 0,
        },
      ];
      if (next.length === 2 && Number(next[0].sharePct) === 100) {
        next[0].sharePct = '50';
        next[1].sharePct = '50';
      }
      return next;
    });
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
    } catch (err) {
      const parsed = extractProblemValidation(err, t('mutations.uploadFailed'));
      setValidationSummary(parsed.summary);
      setValidationErrors(parsed.items);
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: ParsedValidationItem[] = [];
    if (!propertyId) {
      errs.push({
        key: 'propertyId',
        rawField: 'PropertyId',
        message: t('mutations.errPropertyRequired'),
      });
    }
    if (proposedOwners.length === 0) {
      errs.push({
        key: 'proposedOwners',
        rawField: 'ProposedOwners',
        message: t('properties.errOwnerRequired'),
      });
    } else if (Math.abs(shareTotal - 100) > 0.01) {
      errs.push({
        key: 'proposedOwners',
        rawField: 'ProposedOwners',
        message: t('properties.errShareSum100', { sum: shareTotal.toFixed(2) }),
      });
    }

    if (errs.length > 0) {
      setValidationSummary(t('validation.summaryTitle'));
      setValidationErrors(errs);
      return;
    }

    setSubmitting(true);
    setValidationSummary(null);
    setValidationErrors([]);
    try {
      await mutationApi.initiate({
        propertyId,
        mutationType,
        proposedOwners: proposedOwners.map((o) => ({
          citizenId: o.citizen.id,
          ownershipSharePct: Number(o.sharePct) || 0,
          isPrimaryOwner: o.isPrimary,
        })),
        documentReferenceNo: documentReferenceNo.trim() || null,
        documentDate: documentDate || null,
        remarks: remarks.trim() || null,
        supportingAssetKeys: uploadedAssets.map((a) => a.assetKey),
      });
      await queryClient.invalidateQueries({ queryKey: ['mutations-list'] });
      await queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] });
      navigate('/namuna-8/mutation');
    } catch (err) {
      const parsed = extractProblemValidation(err, t('citizens.saveFailed'));
      setValidationSummary(parsed.summary);
      setValidationErrors(parsed.items);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5 text-xs">
      {/* Header & Mode Tabs */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="space-y-1">
            <Link
              to="/namuna-8/mutation"
              className="inline-flex items-center gap-1 text-[11px] font-bold text-brand-700 hover:underline"
            >
              <ArrowLeft className="h-3 w-3" />
              {t('mutations.backToRegister')}
            </Link>
            <h1 className="text-xl font-extrabold text-ink flex items-center gap-2">
              <ArrowRightLeft className="h-5 w-5 text-brand-700" />
              {t('mutations.formTitle')}
            </h1>
          </div>

          {propertyId && (
            <Link
              to={`/namuna-8/partition/${propertyId}`}
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3.5 py-2 font-bold text-slate-700 hover:bg-brand-50"
            >
              <GitBranch className="h-3.5 w-3.5" />
              {t('mutations.switchToSplitTab')}
            </Link>
          )}
        </div>

        <ValidationSummary summary={validationSummary} items={validationErrors} />

        {/* Target Property Selector */}
        <label className="block space-y-1">
          <span className="font-bold text-slate-700">{t('mutations.selectPropertyLabel')} *</span>
          <select
            required
            value={propertyId}
            onChange={(e) => setPropertyId(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2.5 outline-none focus:border-brand-600"
          >
            <option value="">{t('mutations.selectPropertyPlaceholder')}</option>
            {activeProps.map((p) => (
              <option key={p.id} value={p.id}>
                {t('properties.wardNo')} {p.wardNo} / {p.propertyNo} —{' '}
                {p.primaryOwnerNameMr || p.primaryOwnerNameEn || t('properties.unassignedOwner')}
              </option>
            ))}
          </select>
        </label>

        {selectedProp && (
          <div className="grid gap-3 rounded-xl border border-line bg-surface p-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <span className="text-[10px] font-bold uppercase text-muted">
                {t('properties.colWardProp')}
              </span>
              <p className="font-extrabold text-ink flex items-center gap-1">
                <Building2 className="h-3.5 w-3.5 text-brand-700" />
                {selectedProp.wardNo} / {selectedProp.propertyNo}
              </p>
            </div>
            <div>
              <span className="text-[10px] font-bold uppercase text-muted">
                {t('properties.totalArea')}
              </span>
              <p className="font-bold text-ink">{selectedProp.totalAreaSqFt.toFixed(2)} sq.ft</p>
            </div>
            <div className="sm:col-span-2">
              <span className="text-[10px] font-bold uppercase text-muted">
                {t('mutations.previousOwnersHeader')}
              </span>
              <p className="font-bold text-ink">
                {selectedProp.owners.length === 0
                  ? t('properties.unassignedOwner')
                  : selectedProp.owners
                      .map((o) => `${o.fullNameMr} (${o.ownershipSharePct}%)`)
                      .join(', ')}
              </p>
            </div>
          </div>
        )}
      </section>

      {/* Mutation Type & Document Reference */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <h2 className="text-sm font-extrabold text-ink">{t('mutations.deedDetailsSection')}</h2>

        <div className="grid gap-3 sm:grid-cols-3">
          <label className="space-y-1">
            <span className="font-semibold text-slate-700">{t('mutations.mutationTypeLabel')} *</span>
            <select
              value={mutationType}
              onChange={(e) => setMutationType(e.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            >
              {MUTATION_TYPES.map((mt) => (
                <option key={mt} value={mt}>
                  {t(`mutations.type.${mt}`)}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1">
            <span className="font-semibold text-slate-700">{t('mutations.docRefNoLabel')}</span>
            <input
              type="text"
              value={documentReferenceNo}
              onChange={(e) => setDocumentReferenceNo(e.target.value)}
              placeholder="खरेदीखत क्र. 12 / आदेश क्र."
              className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            />
          </label>

          <label className="space-y-1">
            <span className="font-semibold text-slate-700">{t('mutations.docDateLabel')}</span>
            <input
              type="date"
              value={documentDate}
              onChange={(e) => setDocumentDate(e.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            />
          </label>
        </div>

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.remarksLabel')}</span>
          <textarea
            rows={2}
            value={remarks}
            onChange={(e) => setRemarks(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>
      </section>

      {/* Proposed New Owners Picker */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4 text-brand-700" />
            <h2 className="text-sm font-extrabold text-ink">{t('mutations.proposedOwnersSection')}</h2>
            <span
              className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold ${
                Math.abs(shareTotal - 100) <= 0.01
                  ? 'bg-emerald-100 text-emerald-900'
                  : 'bg-amber-100 text-amber-900'
              }`}
            >
              {shareTotal.toFixed(0)}% / 100%
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

        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={citizenSearch}
            onChange={(e) => setCitizenSearch(e.target.value)}
            placeholder={t('properties.searchCitizenPlaceholder')}
            className="w-full rounded-xl border border-slate-200 bg-surface py-2 pl-9 pr-3 outline-none focus:border-brand-600"
          />
        </div>

        {(citizensQuery.data?.items ?? []).length > 0 && (
          <div className="flex max-h-36 flex-wrap gap-1.5 overflow-y-auto rounded-xl border border-line bg-surface p-2">
            {(citizensQuery.data?.items ?? []).map((c) => {
              const already = proposedOwners.some((o) => o.citizen.id === c.id);
              return (
                <button
                  key={c.id}
                  type="button"
                  disabled={already}
                  onClick={() => addProposedOwner(c)}
                  className={`inline-flex items-center gap-1 rounded-lg border px-2.5 py-1 text-[11px] font-semibold ${
                    already
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                      : 'border-slate-200 bg-white text-slate-800 hover:border-brand-400'
                  }`}
                >
                  <Plus className="h-3 w-3" />
                  {c.fullNameMr} ({c.fullNameEn}) {c.aadhaarMasked ? `· ${c.aadhaarMasked}` : ''}
                </button>
              );
            })}
          </div>
        )}

        {proposedOwners.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line p-4 text-center text-muted">
            {t('properties.noOwnersSelected')}
          </p>
        ) : (
          <div className="space-y-2">
            {proposedOwners.map((owner, idx) => (
              <div
                key={owner.citizen.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line bg-surface px-3.5 py-2.5"
              >
                <div>
                  <p className="font-bold text-ink">
                    {owner.citizen.fullNameMr} · {owner.citizen.fullNameEn}
                  </p>
                  <p className="text-[11px] text-muted">{owner.citizen.citizenCode}</p>
                </div>

                <div className="flex items-center gap-3">
                  <label className="flex items-center gap-1 font-semibold">
                    <span>{t('properties.sharePctLabel')}</span>
                    <input
                      type="number"
                      step="0.01"
                      min="0.01"
                      max="100"
                      value={owner.sharePct}
                      onChange={(e) =>
                        setProposedOwners((prev) =>
                          prev.map((x, i) => (i === idx ? { ...x, sharePct: e.target.value } : x)),
                        )
                      }
                      className="w-20 rounded-lg border border-slate-200 bg-white px-2 py-1 text-right font-bold"
                    />
                  </label>

                  <label className="inline-flex items-center gap-1 font-bold text-brand-800 cursor-pointer">
                    <input
                      type="radio"
                      name="primary-proposed-owner"
                      checked={owner.isPrimary}
                      onChange={() =>
                        setProposedOwners((prev) =>
                          prev.map((x, i) => ({ ...x, isPrimary: i === idx })),
                        )
                      }
                    />
                    {t('properties.primaryBadge')}
                  </label>

                  <button
                    type="button"
                    onClick={() =>
                      setProposedOwners((prev) => {
                        const filtered = prev.filter((_, i) => i !== idx);
                        if (filtered.length > 0 && !filtered.some((f) => f.isPrimary)) {
                          filtered[0].isPrimary = true;
                        }
                        return filtered;
                      })
                    }
                    className="rounded-lg p-1 text-rose-700 hover:bg-rose-50"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Supporting Confidential Documents Upload & Submit */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-sm font-extrabold text-ink">{t('mutations.supportingDocsHeader')}</h2>
            <p className="text-[11px] text-muted">{t('mutations.supportingDocsHint')}</p>
          </div>

          <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-xl border border-brand-200 bg-brand-50 px-3.5 py-2 font-bold text-brand-800 hover:bg-brand-100">
            <FileUp className="h-4 w-4" />
            {uploading ? t('onboarding.submitting') : t('mutations.uploadDocBtn')}
            <input
              type="file"
              accept=".pdf,.png,.jpg,.jpeg,.webp"
              onChange={handleFileUpload}
              className="hidden"
            />
          </label>
        </div>

        {uploadedAssets.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {uploadedAssets.map((a) => (
              <span
                key={a.assetKey}
                className="inline-flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-1.5 font-semibold text-emerald-900"
              >
                {a.fileName} ({Math.max(1, Math.round(a.sizeBytes / 1024))} KB)
                <button
                  type="button"
                  onClick={() =>
                    setUploadedAssets((prev) => prev.filter((x) => x.assetKey !== a.assetKey))
                  }
                  className="text-rose-700 hover:text-rose-900"
                >
                  <Trash2 className="h-3 w-3" />
                </button>
              </span>
            ))}
          </div>
        )}

        <div className="flex justify-end gap-3 border-t border-line pt-4">
          <Link
            to="/namuna-8/mutation"
            className="rounded-xl border border-line px-4 py-2 font-bold text-slate-700 hover:bg-slate-50"
          >
            {t('properties.cancel')}
          </Link>
          <button
            type="submit"
            disabled={submitting}
            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-5 py-2.5 font-extrabold text-white hover:bg-brand-800 disabled:opacity-60"
          >
            <ArrowRightLeft className="h-4 w-4" />
            {submitting ? t('onboarding.submitting') : t('mutations.submitNoticeBtn')}
          </button>
        </div>
      </section>

      {quickAddOpen && (
        <QuickAddCitizenModal
          onClose={() => setQuickAddOpen(false)}
          onSaved={(savedCitizen) => {
            addProposedOwner(savedCitizen);
            setQuickAddOpen(false);
            void queryClient.invalidateQueries({ queryKey: ['mutation-citizen-picker'] });
          }}
        />
      )}
    </form>
  );
}

