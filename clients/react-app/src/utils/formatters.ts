export function formatInr(amount: number): string {
  return `₹ ${amount.toLocaleString('en-IN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export function resolveDateLocale(lang: string): string {
  if (lang === 'mr') return 'mr-IN-u-nu-latn';
  if (lang === 'hi') return 'hi-IN-u-nu-latn';
  return 'en-IN';
}

export function formatDateTime(iso: string | null | undefined, lang: string): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(resolveDateLocale(lang), {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function formatDateOnly(iso: string | null | undefined, lang: string): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString(resolveDateLocale(lang), {
    dateStyle: 'medium',
  });
}

const FONT_SIZE_STORAGE_KEY = 'gpmh_font_size';
export type FontSizeOption = 14 | 16 | 18;

export function getSavedFontSize(): FontSizeOption {
  if (typeof window === 'undefined') return 16;
  const raw = Number(window.localStorage.getItem(FONT_SIZE_STORAGE_KEY));
  if (raw === 14 || raw === 16 || raw === 18) return raw;
  return 16;
}

export function applyFontSize(px: FontSizeOption): void {
  if (typeof document !== 'undefined') {
    document.documentElement.style.fontSize = `${px}px`;
  }
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(FONT_SIZE_STORAGE_KEY, String(px));
  }
}

