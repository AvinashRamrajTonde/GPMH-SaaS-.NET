import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Building2,
  ChevronLeft,
  ChevronRight,
  Edit3,
  History,
  Plus,
  Search,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/useAuth';
import { type CitizenDto, EXEMPTION_CATEGORIES, namuna8Api } from '../../services/namuna8Api';
import { extractProblemValidation } from '../../utils/validation';
import { QuickAddCitizenModal } from './QuickAddCitizenModal';

export function CitizensPage() {
  const { t } = useTranslation();
  const { user, accessToken } = useAuth();
  const queryClient = useQueryClient();

  const [searchInput, setSearchInput] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [wardFilter, setWardFilter] = useState('');
  const [exemptionFilter, setExemptionFilter] = useState('');
  const [page, setPage] = useState(1);

  const [modalOpen, setModalOpen] = useState(false);
  const [editingCitizen, setEditingCitizen] = useState<CitizenDto | null>(null);

  const canRead = Boolean(
    accessToken &&
      user?.activeTenantId &&
      ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor', 'Auditor'].includes(user.role),
  );
  const canWrite = Boolean(
    user && ['SuperAdmin', 'Gramsevak', 'GP_Admin', 'GP_Operator', 'Surveyor'].includes(user.role),
  );

  const citizensQuery = useQuery({
    queryKey: ['citizens', user?.activeTenantId, appliedSearch, wardFilter, exemptionFilter, page],
    queryFn: () =>
      namuna8Api.listCitizens({
        search: appliedSearch || undefined,
        wardNo: wardFilter || undefined,
        exemptionCategory: exemptionFilter || undefined,
        page,
        pageSize: 15,
      }),
    enabled: canRead,
  });

  if (!canRead) {
    return (
      <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-6 text-xs text-amber-950 space-y-3">
        <div className="flex items-center gap-2 font-bold text-sm">
          <Users className="h-5 w-5 text-brand-700" />
          <h1>{t('citizens.title')}</h1>
        </div>
        <p>{t('citizens.signInRequired')}</p>
        <Link
          to="/login"
          className="inline-block rounded-lg bg-brand-700 px-4 py-2 font-bold text-white hover:bg-brand-800"
        >
          {t('nav.login')}
        </Link>
      </div>
    );
  }

  const totalCount = citizensQuery.data?.totalCount ?? 0;
  const pageSize = citizensQuery.data?.pageSize ?? 15;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const items = citizensQuery.data?.items ?? [];

  return (
    <div className="space-y-5">
      <section className="rounded-2xl border border-line bg-card p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <Users className="h-5 w-5 text-brand-700" />
              <h1 className="text-xl font-extrabold text-ink">{t('citizens.title')}</h1>
              <span className="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-[11px] font-bold text-emerald-800">
                <ShieldCheck className="h-3 w-3" />
                DPDP AES-GCM
              </span>
            </div>
            <p className="text-xs text-muted">{t('citizens.subtitle')}</p>
          </div>

          {canWrite && (
            <button
              type="button"
              onClick={() => {
                setEditingCitizen(null);
                setModalOpen(true);
              }}
              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2.5 text-xs font-bold text-white hover:bg-brand-800 shadow-xs"
            >
              <Plus className="h-4 w-4" />
              {t('citizens.addBtn')}
            </button>
          )}
        </div>

        {/* Search & Filters */}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setPage(1);
            setAppliedSearch(searchInput.trim());
          }}
          className="grid gap-3 sm:grid-cols-[1fr_150px_200px_auto] text-xs"
        >
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              type="search"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={t('citizens.searchPlaceholder')}
              className="w-full rounded-xl border border-line bg-surface py-2 pl-9 pr-3 focus:bg-white focus:border-brand-600 focus:outline-none"
            />
          </div>

          <input
            type="text"
            value={wardFilter}
            onChange={(e) => {
              setPage(1);
              setWardFilter(e.target.value);
            }}
            placeholder={t('citizens.filterWardPlaceholder')}
            className="rounded-xl border border-line bg-surface px-3 py-2 mono focus:bg-white focus:border-brand-600 focus:outline-none"
          />

          <select
            value={exemptionFilter}
            onChange={(e) => {
              setPage(1);
              setExemptionFilter(e.target.value);
            }}
            className="rounded-xl border border-line bg-surface px-3 py-2"
          >
            <option value="">{t('citizens.filterExemptionAll')}</option>
            {EXEMPTION_CATEGORIES.map((ex) => (
              <option key={ex} value={ex}>
                {t(`citizens.exemption.${ex}`)}
              </option>
            ))}
          </select>

          <button
            type="submit"
            className="rounded-xl border border-brand-200 bg-brand-50 px-4 py-2 font-bold text-brand-800 hover:bg-brand-100"
          >
            {t('citizens.searchBtn')}
          </button>
        </form>

        {citizensQuery.isError && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs text-rose-800">
            {extractProblemValidation(citizensQuery.error, t('citizens.loadFailed')).summary}
          </div>
        )}

        {citizensQuery.isLoading ? (
          <div className="space-y-2">
            {[1, 2, 3].map((n) => (
              <div key={n} className="h-12 animate-pulse rounded-xl bg-slate-200" />
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-300 bg-surface p-8 text-center space-y-2">
            <Users className="mx-auto h-8 w-8 text-slate-400" />
            <p className="text-sm font-bold text-ink">{t('citizens.emptyTitle')}</p>
            <p className="text-xs text-muted">{t('citizens.emptyDesc')}</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-line">
              <table className="w-full text-left text-xs">
                <thead className="bg-[var(--color-thead)] text-[11px] font-bold uppercase text-brand-900">
                  <tr>
                    <th className="px-3 py-2.5">{t('citizens.colCode')}</th>
                    <th className="px-3 py-2.5">{t('citizens.colName')}</th>
                    <th className="px-3 py-2.5">{t('citizens.colContactAadhaar')}</th>
                    <th className="px-3 py-2.5">{t('properties.wardNo')}</th>
                    <th className="px-3 py-2.5">{t('citizens.colCategoryExemption')}</th>
                    <th className="px-3 py-2.5 text-center">{t('citizens.colProperties')}</th>
                    <th className="px-3 py-2.5 text-right">{t('properties.actions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line bg-card">
                  {items.map((c) => (
                    <tr key={c.id} className="hover:bg-slate-50/70">
                      <td className="px-3 py-2.5 mono font-bold text-brand-800">{c.citizenCode}</td>
                      <td className="px-3 py-2.5">
                        <div className="font-bold text-ink">{c.fullNameMr}</div>
                        <div className="text-[11px] text-muted">
                          {c.fullNameEn} · {t(`citizens.gender${c.gender}`)}
                        </div>
                      </td>
                      <td className="px-3 py-2.5 space-y-0.5 mono">
                        <div className="text-ink">{c.mobileNo || '—'}</div>
                        <div className="text-[11px] font-semibold text-emerald-800">
                          {c.aadhaarMasked || t('citizens.aadhaarNotProvided')}
                        </div>
                      </td>
                      <td className="px-3 py-2.5 mono font-semibold text-slate-700">
                        {c.wardNo || '—'}
                      </td>
                      <td className="px-3 py-2.5 space-y-1">
                        <span className="inline-block rounded-full border border-slate-200 bg-slate-100 px-2 py-0.5 text-[10.5px] font-bold text-slate-700">
                          {c.socialCategory}
                        </span>{' '}
                        <span
                          className={`inline-block rounded-full border px-2 py-0.5 text-[10.5px] font-bold ${
                            c.exemptionCategory !== 'NotExempted'
                              ? 'border-amber-200 bg-amber-50 text-amber-900'
                              : 'border-slate-200 bg-surface text-slate-600'
                          }`}
                        >
                          {t(`citizens.exemption.${c.exemptionCategory}`)}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-center">
                        <span className="inline-flex items-center gap-1 rounded-full border border-sky-200 bg-sky-50 px-2.5 py-0.5 mono font-bold text-sky-900">
                          <Building2 className="h-3 w-3" />
                          {c.linkedPropertiesCount}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-right">
                        <div className="inline-flex items-center gap-1.5">
                          <Link
                            to={`/audit/Citizen/${c.id}`}
                            className="rounded-lg border border-line bg-surface p-1.5 text-slate-600 hover:text-brand-700"
                            title={t('properties.auditHistory')}
                          >
                            <History className="h-3.5 w-3.5" />
                          </Link>
                          {canWrite && (
                            <>
                              <button
                                type="button"
                                onClick={() => {
                                  setEditingCitizen(c);
                                  setModalOpen(true);
                                }}
                                className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-800 hover:bg-brand-100"
                              >
                                <Edit3 className="h-3 w-3" />
                                {t('formula.editRowBtn')}
                              </button>
                              <Link
                                to={`/namuna-8/add?citizenId=${encodeURIComponent(c.id)}`}
                                className="inline-flex items-center gap-1 rounded-lg bg-brand-700 px-2.5 py-1 text-[11px] font-bold text-white hover:bg-brand-800"
                              >
                                <Plus className="h-3 w-3" />
                                {t('citizens.linkPropBtn')}
                              </Link>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex items-center justify-between text-xs text-muted">
              <span>
                {t('citizens.showingSummary', { count: items.length, total: totalCount })}
              </span>
              <div className="inline-flex items-center gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="inline-flex items-center gap-1 rounded-lg border border-line bg-card px-2.5 py-1 font-bold text-ink disabled:opacity-50"
                >
                  <ChevronLeft className="h-3.5 w-3.5" />
                </button>
                <span className="mono font-bold text-ink">
                  {page} / {totalPages}
                </span>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  className="inline-flex items-center gap-1 rounded-lg border border-line bg-card px-2.5 py-1 font-bold text-ink disabled:opacity-50"
                >
                  <ChevronRight className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          </>
        )}
      </section>

      {modalOpen && (
        <QuickAddCitizenModal
          existing={editingCitizen}
          onClose={() => setModalOpen(false)}
          onSaved={() => {
            setModalOpen(false);
            void queryClient.invalidateQueries({ queryKey: ['citizens', user?.activeTenantId] });
          }}
        />
      )}
    </div>
  );
}
