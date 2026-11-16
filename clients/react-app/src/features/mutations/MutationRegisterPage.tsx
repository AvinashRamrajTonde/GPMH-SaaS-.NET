import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowRightLeft,
  CheckCircle2,
  Clock,
  Download,
  Eye,
  FileText,
  GitBranch,
  History,
  Plus,
  Search,
  ShieldAlert,
  X,
  XCircle,
} from 'lucide-react';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { ApiProblemError } from '../../services/apiClient';
import {
  mutationApi,
  type MutationDetailDto,
  type MutationListItemDto,
} from '../../services/mutationApi';
import { namuna8Api } from '../../services/namuna8Api';
import { formatDateTime } from '../../utils/formatters';

export function MutationRegisterPage() {
  const { t, i18n } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [propQuickSearch, setPropQuickSearch] = useState('');
  const [appliedPropSearch, setAppliedPropSearch] = useState('');

  const [statusFilter, setStatusFilter] = useState('');
  const [wardFilter, setWardFilter] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [page, setPage] = useState(1);

  const [pdfDownloading, setPdfDownloading] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [dossierId, setDossierId] = useState<string | null>(null);
  const [objectionTarget, setObjectionTarget] = useState<MutationListItemDto | null>(null);
  const [approveTarget, setApproveTarget] = useState<MutationListItemDto | null>(null);
  const [rejectTarget, setRejectTarget] = useState<MutationListItemDto | null>(null);

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor', 'Auditor'].includes(
        user.role,
      ),
  );
  const canWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor'].includes(user.role),
  );
  const canApprove = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin'].includes(user.role),
  );

  const mutationsQuery = useQuery({
    queryKey: [
      'mutations-list',
      user?.activeTenantId,
      statusFilter,
      appliedSearch,
      wardFilter,
      page,
    ],
    queryFn: () =>
      mutationApi.list({
        status: statusFilter || undefined,
        search: appliedSearch || undefined,
        wardNo: wardFilter || undefined,
        page,
        pageSize: 20,
      }),
    enabled: canRead,
  });

  const propsQuery = useQuery({
    queryKey: ['mutation-quick-props', user?.activeTenantId],
    queryFn: namuna8Api.listProperties,
    enabled: canRead,
  });

  const dossierQuery = useQuery({
    queryKey: ['mutation-dossier', dossierId],
    queryFn: () => mutationApi.getById(dossierId!),
    enabled: canRead && Boolean(dossierId),
  });

  const matchingProps = useMemo(() => {
    const q = appliedPropSearch.trim().toLowerCase();
    if (!q) return [];
    return (propsQuery.data ?? [])
      .filter(
        (p) =>
          p.status !== 'Partitioned' &&
          (p.wardNo.toLowerCase().includes(q) ||
            p.propertyNo.toLowerCase().includes(q) ||
            (p.primaryOwnerNameMr ?? '').toLowerCase().includes(q) ||
            (p.primaryOwnerNameEn ?? '').toLowerCase().includes(q)),
      )
      .slice(0, 8);
  }, [propsQuery.data, appliedPropSearch]);

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

  const handleDownloadPdf = async () => {
    setPdfDownloading(true);
    setActionError(null);
    try {
      await mutationApi.downloadRegisterPdf({ wardNo: wardFilter || undefined });
    } catch (err) {
      setActionError(err instanceof ApiProblemError ? err.problem.detail || err.message : t('print.downloadFailed'));
    } finally {
      setPdfDownloading(false);
    }
  };

  const refreshMutations = () => {
    void queryClient.invalidateQueries({ queryKey: ['mutations-list'] });
    void queryClient.invalidateQueries({ queryKey: ['properties'] });
    void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] });
  };

  const items = mutationsQuery.data?.items ?? [];
  const totalCount = mutationsQuery.data?.totalCount ?? 0;

  return (
    <div className="space-y-5 text-xs">
      {/* Header */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-xl font-extrabold text-ink flex items-center gap-2">
              <ArrowRightLeft className="h-5 w-5 text-brand-700" />
              {t('mutations.title')}
            </h1>
            <p className="text-muted">{t('mutations.subtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={handleDownloadPdf}
              disabled={pdfDownloading}
              className="inline-flex items-center gap-1.5 rounded-xl border border-line bg-surface px-3.5 py-2 font-bold text-slate-700 hover:bg-brand-50 disabled:opacity-50"
            >
              <Download className="h-4 w-4" />
              {pdfDownloading ? t('print.generatingPdf') : t('mutations.downloadPdfBtn')}
            </button>
            {canWrite && (
              <Link
                to="/namuna-8/mutation/new"
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800 shadow-xs"
              >
                <Plus className="h-4 w-4" />
                {t('mutations.newEntryBtn')}
              </Link>
            )}
          </div>
        </div>

        {/* Card 1: Quick Property Lookup for Initiating Transfer / Partition (09 template) */}
        <div className="rounded-xl border border-line bg-surface p-4 space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h2 className="font-bold text-ink">{t('mutations.quickPropTitle')}</h2>
              <p className="text-[11px] text-muted">{t('mutations.quickPropSubtitle')}</p>
            </div>
          </div>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              setAppliedPropSearch(propQuickSearch);
            }}
            className="flex flex-wrap gap-2"
          >
            <div className="relative flex-1 min-w-[220px]">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                value={propQuickSearch}
                onChange={(e) => setPropQuickSearch(e.target.value)}
                placeholder={t('mutations.quickPropPlaceholder')}
                className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-9 pr-3 outline-none focus:border-brand-600"
              />
            </div>
            <button
              type="submit"
              className="rounded-xl bg-slate-800 px-4 py-2 font-bold text-white hover:bg-slate-700"
            >
              {t('citizens.searchBtn')}
            </button>
            {appliedPropSearch && (
              <button
                type="button"
                onClick={() => {
                  setPropQuickSearch('');
                  setAppliedPropSearch('');
                }}
                className="rounded-xl border border-slate-200 bg-white px-3 py-2 font-bold text-slate-600 hover:bg-slate-50"
              >
                {t('properties.cancel')}
              </button>
            )}
          </form>

          {appliedPropSearch && matchingProps.length > 0 && (
            <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
              <table className="w-full text-left text-[11px]">
                <thead className="bg-slate-50 border-b border-slate-200 font-bold text-slate-700">
                  <tr>
                    <th className="px-3 py-2">{t('properties.colWardProp')}</th>
                    <th className="px-3 py-2">{t('properties.colOwner')}</th>
                    <th className="px-3 py-2">{t('properties.totalArea')}</th>
                    <th className="px-3 py-2 text-right">{t('properties.actions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {matchingProps.map((p) => (
                    <tr key={p.id}>
                      <td className="px-3 py-2 font-bold">
                        {p.wardNo} / {p.propertyNo}
                      </td>
                      <td className="px-3 py-2">
                        {p.primaryOwnerNameMr || p.primaryOwnerNameEn || t('properties.unassignedOwner')}
                      </td>
                      <td className="px-3 py-2">{p.totalAreaSqFt.toFixed(0)} sq.ft</td>
                      <td className="px-3 py-2 text-right space-x-2">
                        {canWrite && (
                          <>
                            <Link
                              to={`/namuna-8/mutation/new?propertyId=${encodeURIComponent(p.id)}`}
                              className="inline-flex items-center gap-1 rounded-lg bg-brand-700 px-2.5 py-1 font-bold text-white hover:bg-brand-800"
                            >
                              <ArrowRightLeft className="h-3 w-3" />
                              {t('mutations.initiateTransferBtn')}
                            </Link>
                            <Link
                              to={`/namuna-8/partition/${p.id}`}
                              className="inline-flex items-center gap-1 rounded-lg border border-line bg-surface px-2.5 py-1 font-bold text-slate-700 hover:bg-slate-100"
                            >
                              <GitBranch className="h-3 w-3" />
                              {t('properties.partitionBtn')}
                            </Link>
                          </>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </section>

      {actionError && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-semibold text-rose-800">
          {actionError}
        </div>
      )}

      {/* Card 2: Statutory Mutation Register */}
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-sm font-extrabold text-ink">
            {t('mutations.registerHeading')} ({totalCount})
          </h2>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              setAppliedSearch(searchInput);
              setPage(1);
            }}
            className="flex flex-wrap items-center gap-2"
          >
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              className="rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            >
              <option value="">{t('mutations.filterStatusAll')}</option>
              <option value="NoticePublished">{t('mutations.statusNoticePublished')}</option>
              <option value="ObjectionRecorded">{t('mutations.statusObjectionRecorded')}</option>
              <option value="Approved">{t('mutations.statusApproved')}</option>
              <option value="Rejected">{t('mutations.statusRejected')}</option>
            </select>

            <input
              type="text"
              value={wardFilter}
              onChange={(e) => {
                setWardFilter(e.target.value);
                setPage(1);
              }}
              placeholder={t('citizens.filterWardPlaceholder')}
              className="w-28 rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            />

            <input
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={t('mutations.searchPlaceholder')}
              className="w-56 rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            />
            <button
              type="submit"
              className="rounded-xl border border-brand-200 bg-brand-50 px-3.5 py-2 font-bold text-brand-800 hover:bg-brand-100"
            >
              {t('citizens.searchBtn')}
            </button>
          </form>
        </div>

        {mutationsQuery.isLoading ? (
          <div className="h-44 animate-pulse rounded-xl bg-slate-200" />
        ) : items.length === 0 ? (
          <div className="rounded-xl border border-dashed border-line p-10 text-center space-y-2">
            <ArrowRightLeft className="mx-auto h-7 w-7 text-slate-400" />
            <p className="font-bold text-slate-700">{t('mutations.emptyTitle')}</p>
            <p className="text-muted">{t('mutations.emptyDesc')}</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[960px] text-left">
              <thead>
                <tr className="border-b border-line text-[11px] font-bold uppercase text-muted">
                  <th className="py-2.5 pr-3">{t('mutations.colAppNo')}</th>
                  <th className="py-2.5 pr-3">{t('properties.colWardProp')}</th>
                  <th className="py-2.5 pr-3">{t('mutations.colTypeDoc')}</th>
                  <th className="py-2.5 pr-3">{t('mutations.colOldNewOwners')}</th>
                  <th className="py-2.5 pr-3">{t('mutations.colNoticeStatus')}</th>
                  <th className="py-2.5 pr-3">{t('mutations.colResolution')}</th>
                  <th className="py-2.5 text-right">{t('properties.actions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((m) => (
                  <tr key={m.id} className="hover:bg-slate-50/70">
                    <td className="py-3 pr-3 font-extrabold text-brand-800">{m.applicationNo}</td>
                    <td className="py-3 pr-3 font-bold text-ink">
                      {m.wardNo} / {m.propertyNo}
                    </td>
                    <td className="py-3 pr-3">
                      <span className="font-semibold text-ink">
                        {t(`mutations.type.${m.mutationType}`)}
                      </span>
                      {m.documentReferenceNo && (
                        <p className="text-[11px] text-muted">#{m.documentReferenceNo}</p>
                      )}
                    </td>
                    <td className="py-3 pr-3 max-w-[260px]">
                      <p className="text-[11px] text-slate-500 truncate">{m.previousOwnersSummaryMr}</p>
                      <p className="font-bold text-ink truncate">→ {m.proposedOwnersSummaryMr}</p>
                    </td>
                    <td className="py-3 pr-3">
                      <MutationStatusBadge mutation={m} />
                    </td>
                    <td className="py-3 pr-3 text-[11px]">
                      {m.resolutionNo ? (
                        <span className="font-semibold text-ink">
                          {m.resolutionNo} ({m.resolutionDate})
                        </span>
                      ) : (
                        <span className="text-muted">—</span>
                      )}
                    </td>
                    <td className="py-3 text-right whitespace-nowrap space-x-1">
                      <button
                        type="button"
                        onClick={() => setDossierId(m.id)}
                        className="inline-flex items-center gap-1 rounded-lg border border-line bg-surface px-2 py-1 text-[11px] font-bold text-slate-700 hover:bg-brand-50"
                      >
                        <Eye className="h-3 w-3" />
                        {t('mutations.dossierBtn')}
                      </button>

                      {canWrite && m.status !== 'Approved' && m.status !== 'Rejected' && (
                        <button
                          type="button"
                          onClick={() => setObjectionTarget(m)}
                          className="inline-flex items-center gap-1 rounded-lg border border-amber-300 bg-amber-50 px-2 py-1 text-[11px] font-bold text-amber-900 hover:bg-amber-100"
                        >
                          <ShieldAlert className="h-3 w-3" />
                          {t('mutations.objectionBtn')}
                        </button>
                      )}

                      {canApprove && m.status !== 'Approved' && m.status !== 'Rejected' && (
                        <>
                          <button
                            type="button"
                            onClick={() => setApproveTarget(m)}
                            className="inline-flex items-center gap-1 rounded-lg bg-emerald-700 px-2 py-1 text-[11px] font-bold text-white hover:bg-emerald-800"
                          >
                            <CheckCircle2 className="h-3 w-3" />
                            {t('mutations.approveBtn')}
                          </button>
                          <button
                            type="button"
                            onClick={() => setRejectTarget(m)}
                            className="inline-flex items-center gap-1 rounded-lg border border-rose-200 bg-rose-50 px-2 py-1 text-[11px] font-bold text-rose-800 hover:bg-rose-100"
                          >
                            <XCircle className="h-3 w-3" />
                            {t('mutations.rejectBtn')}
                          </button>
                        </>
                      )}

                      <Link
                        to={`/audit/PropertyMutation/${m.id}`}
                        className="inline-flex items-center gap-1 rounded-lg border border-line bg-surface px-2 py-1 text-[11px] font-bold text-slate-600 hover:bg-slate-100"
                      >
                        <History className="h-3 w-3" />
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {dossierId && (
        <MutationDossierModal
          dossier={dossierQuery.data}
          isLoading={dossierQuery.isLoading}
          language={i18n.language}
          onClose={() => setDossierId(null)}
        />
      )}

      {objectionTarget && (
        <ObjectionModal
          mutation={objectionTarget}
          onClose={() => setObjectionTarget(null)}
          onCompleted={() => {
            setObjectionTarget(null);
            refreshMutations();
          }}
        />
      )}

      {approveTarget && (
        <ApproveResolutionModal
          mutation={approveTarget}
          onClose={() => setApproveTarget(null)}
          onCompleted={() => {
            setApproveTarget(null);
            refreshMutations();
          }}
        />
      )}

      {rejectTarget && (
        <RejectModal
          mutation={rejectTarget}
          onClose={() => setRejectTarget(null)}
          onCompleted={() => {
            setRejectTarget(null);
            refreshMutations();
          }}
        />
      )}
    </div>
  );
}

function MutationStatusBadge({ mutation: m }: { mutation: MutationListItemDto }) {
  const { t } = useTranslation();
  if (m.status === 'Approved') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-0.5 text-[11px] font-bold text-emerald-900">
        <CheckCircle2 className="h-3 w-3" />
        {t('mutations.statusApproved')}
      </span>
    );
  }
  if (m.status === 'Rejected') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-slate-200 px-2.5 py-0.5 text-[11px] font-bold text-slate-800">
        <XCircle className="h-3 w-3" />
        {t('mutations.statusRejected')}
      </span>
    );
  }
  if (m.status === 'ObjectionRecorded') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-rose-100 px-2.5 py-0.5 text-[11px] font-bold text-rose-900">
        <ShieldAlert className="h-3 w-3" />
        {t('mutations.statusObjectionRecorded')}
      </span>
    );
  }
  return m.isNoticeWindowElapsed ? (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 border border-emerald-300 px-2.5 py-0.5 text-[11px] font-bold text-emerald-800">
      <Clock className="h-3 w-3" />
      {t('mutations.noticeReadyBadge')}
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-0.5 text-[11px] font-bold text-amber-900">
      <Clock className="h-3 w-3" />
      {t('mutations.noticeCountdownBadge', { days: m.daysRemaining })}
    </span>
  );
}

function MutationDossierModal({
  dossier,
  isLoading,
  language,
  onClose,
}: {
  dossier?: MutationDetailDto;
  isLoading: boolean;
  language: string;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [openingKey, setOpeningKey] = useState<string | null>(null);

  const handleOpenAsset = async (assetKey: string) => {
    setOpeningKey(assetKey);
    try {
      const url = await mutationApi.openProtectedAsset(assetKey);
      window.open(url, '_blank', 'noopener,noreferrer');
    } finally {
      setOpeningKey(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div className="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl bg-card p-6 shadow-xl space-y-4 text-xs">
        <div className="flex items-center justify-between border-b border-line pb-3">
          <h3 className="text-base font-extrabold text-ink">
            {t('mutations.dossierTitle')} {dossier ? `— ${dossier.applicationNo}` : ''}
          </h3>
          <button type="button" onClick={onClose} className="rounded-lg p-1 hover:bg-slate-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        {isLoading || !dossier ? (
          <div className="h-40 animate-pulse rounded-xl bg-slate-200" />
        ) : (
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="rounded-xl border border-line bg-surface p-3 space-y-1">
                <p className="font-bold text-muted">{t('mutations.previousOwnersHeader')}</p>
                {dossier.previousOwners.length === 0 ? (
                  <p className="text-muted">—</p>
                ) : (
                  dossier.previousOwners.map((o) => (
                    <p key={o.citizenId} className="font-semibold text-ink">
                      {o.fullNameMr} ({o.ownershipSharePct}%) {o.aadhaarMasked ? `· ${o.aadhaarMasked}` : ''}
                    </p>
                  ))
                )}
              </div>

              <div className="rounded-xl border border-emerald-200 bg-emerald-50/50 p-3 space-y-1">
                <p className="font-bold text-emerald-800">{t('mutations.proposedOwnersHeader')}</p>
                {dossier.proposedOwners.map((o) => (
                  <p key={o.citizenId} className="font-bold text-ink">
                    {o.fullNameMr} ({o.ownershipSharePct}%) {o.aadhaarMasked ? `· ${o.aadhaarMasked}` : ''}
                  </p>
                ))}
              </div>
            </div>

            <div className="rounded-xl border border-line bg-surface p-3 grid gap-2 sm:grid-cols-2">
              <div>
                <span className="text-muted">{t('mutations.noticePeriodWindow')}: </span>
                <span className="font-semibold">
                  {formatDateTime(dossier.noticeIssuedAtUtc, language)} →{' '}
                  {formatDateTime(dossier.noticeEndsAtUtc, language)}
                </span>
              </div>
              <div>
                <span className="text-muted">{t('mutations.colTypeDoc')}: </span>
                <span className="font-semibold">
                  {t(`mutations.type.${dossier.mutationType}`)} ({dossier.documentReferenceNo || '-'})
                </span>
              </div>
              {dossier.objectionSummary && (
                <div className="sm:col-span-2 rounded-lg border border-rose-200 bg-rose-50 p-2 text-rose-900">
                  <b>{t('mutations.objectionHeader')}:</b> {dossier.objectionSummary}
                </div>
              )}
              {dossier.rejectionReason && (
                <div className="sm:col-span-2 rounded-lg border border-rose-200 bg-rose-50 p-2 text-rose-900">
                  <b>{t('mutations.rejectionReasonLabel')}:</b> {dossier.rejectionReason}
                </div>
              )}
            </div>

            {dossier.supportingAssets.length > 0 && (
              <div className="space-y-2">
                <h4 className="font-bold text-ink">{t('mutations.supportingDocsHeader')}</h4>
                <div className="flex flex-wrap gap-2">
                  {dossier.supportingAssets.map((a) => (
                    <button
                      key={a.assetKey}
                      type="button"
                      disabled={openingKey === a.assetKey}
                      onClick={() => handleOpenAsset(a.assetKey)}
                      className="inline-flex items-center gap-1.5 rounded-xl border border-brand-200 bg-brand-50 px-3 py-1.5 font-bold text-brand-800 hover:bg-brand-100"
                    >
                      <FileText className="h-3.5 w-3.5" />
                      {a.fileName} ({Math.max(1, Math.round(a.sizeBytes / 1024))} KB)
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function ObjectionModal({
  mutation,
  onClose,
  onCompleted,
}: {
  mutation: MutationListItemDto;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [summary, setSummary] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!summary.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await mutationApi.recordObjection(mutation.id, summary.trim());
      onCompleted();
    } catch (err) {
      setError(err instanceof ApiProblemError ? err.problem.detail || err.message : t('citizens.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <form onSubmit={handleSubmit} className="w-full max-w-md rounded-2xl bg-card p-6 shadow-xl space-y-4 text-xs">
        <div className="flex items-center justify-between border-b border-line pb-2">
          <h3 className="text-sm font-extrabold text-ink">
            {t('mutations.objectionModalTitle')} — {mutation.applicationNo}
          </h3>
          <button type="button" onClick={onClose} className="rounded-lg p-1 hover:bg-slate-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.objectionSummaryLabel')} *</span>
          <textarea
            rows={3}
            required
            value={summary}
            onChange={(e) => setSummary(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>

        {error && <p className="font-semibold text-rose-700">{error}</p>}

        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-xl border border-line px-3 py-2 font-bold">
            {t('properties.cancel')}
          </button>
          <button
            type="submit"
            disabled={saving}
            className="rounded-xl bg-amber-600 px-4 py-2 font-bold text-white hover:bg-amber-700 disabled:opacity-60"
          >
            {saving ? t('onboarding.submitting') : t('mutations.objectionBtn')}
          </button>
        </div>
      </form>
    </div>
  );
}

function ApproveResolutionModal({
  mutation,
  onClose,
  onCompleted,
}: {
  mutation: MutationListItemDto;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [resolutionNo, setResolutionNo] = useState('');
  const [resolutionDate, setResolutionDate] = useState(new Date().toISOString().slice(0, 10));
  const [clearanceNotes, setClearanceNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await mutationApi.approve(mutation.id, {
        resolutionNo: resolutionNo.trim(),
        resolutionDate,
        objectionResolvedNotes: mutation.hasObjection ? clearanceNotes.trim() : null,
      });
      onCompleted();
    } catch (err) {
      setError(err instanceof ApiProblemError ? err.problem.detail || err.message : t('citizens.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <form onSubmit={handleSubmit} className="w-full max-w-md rounded-2xl bg-card p-6 shadow-xl space-y-4 text-xs">
        <div className="flex items-center justify-between border-b border-line pb-2">
          <h3 className="text-sm font-extrabold text-ink">
            {t('mutations.approveModalTitle')} — {mutation.applicationNo}
          </h3>
          <button type="button" onClick={onClose} className="rounded-lg p-1 hover:bg-slate-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        {!mutation.isNoticeWindowElapsed && (
          <div className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-amber-950">
            <AlertTriangle className="h-4 w-4 shrink-0 text-amber-700 mt-0.5" />
            <p className="font-semibold">
              {t('mutations.noticeGateWarning', { days: mutation.daysRemaining })}
            </p>
          </div>
        )}

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.resolutionNoLabel')} *</span>
          <input
            type="text"
            required
            value={resolutionNo}
            onChange={(e) => setResolutionNo(e.target.value)}
            placeholder="ठराव क्र. ९/१"
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.resolutionDateLabel')} *</span>
          <input
            type="date"
            required
            value={resolutionDate}
            onChange={(e) => setResolutionDate(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>

        {mutation.hasObjection && (
          <label className="block space-y-1">
            <span className="font-semibold text-rose-800">{t('mutations.objectionClearanceLabel')} *</span>
            <textarea
              rows={2}
              required
              value={clearanceNotes}
              onChange={(e) => setClearanceNotes(e.target.value)}
              className="w-full rounded-xl border border-rose-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
            />
          </label>
        )}

        {error && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 font-semibold text-rose-800">
            {error}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-xl border border-line px-3 py-2 font-bold">
            {t('properties.cancel')}
          </button>
          <button
            type="submit"
            disabled={saving}
            className="rounded-xl bg-emerald-700 px-4 py-2 font-bold text-white hover:bg-emerald-800 disabled:opacity-60"
          >
            {saving ? t('onboarding.submitting') : t('mutations.confirmApproveBtn')}
          </button>
        </div>
      </form>
    </div>
  );
}

function RejectModal({
  mutation,
  onClose,
  onCompleted,
}: {
  mutation: MutationListItemDto;
  onClose: () => void;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [resolutionNo, setResolutionNo] = useState('');
  const [resolutionDate, setResolutionDate] = useState(new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await mutationApi.reject(mutation.id, {
        rejectionReason: reason.trim(),
        resolutionNo: resolutionNo.trim(),
        resolutionDate,
      });
      onCompleted();
    } catch (err) {
      setError(err instanceof ApiProblemError ? err.problem.detail || err.message : t('citizens.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <form onSubmit={handleSubmit} className="w-full max-w-md rounded-2xl bg-card p-6 shadow-xl space-y-4 text-xs">
        <div className="flex items-center justify-between border-b border-line pb-2">
          <h3 className="text-sm font-extrabold text-ink">
            {t('mutations.rejectModalTitle')} — {mutation.applicationNo}
          </h3>
          <button type="button" onClick={onClose} className="rounded-lg p-1 hover:bg-slate-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.resolutionNoLabel')} *</span>
          <input
            type="text"
            required
            value={resolutionNo}
            onChange={(e) => setResolutionNo(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.resolutionDateLabel')} *</span>
          <input
            type="date"
            required
            value={resolutionDate}
            onChange={(e) => setResolutionDate(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>

        <label className="block space-y-1">
          <span className="font-semibold text-slate-700">{t('mutations.rejectionReasonLabel')} *</span>
          <textarea
            rows={2}
            required
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-surface px-3 py-2 outline-none focus:border-brand-600"
          />
        </label>

        {error && <p className="font-semibold text-rose-700">{error}</p>}

        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-xl border border-line px-3 py-2 font-bold">
            {t('properties.cancel')}
          </button>
          <button
            type="submit"
            disabled={saving}
            className="rounded-xl bg-rose-700 px-4 py-2 font-bold text-white hover:bg-rose-800 disabled:opacity-60"
          >
            {saving ? t('onboarding.submitting') : t('mutations.rejectBtn')}
          </button>
        </div>
      </form>
    </div>
  );
}

