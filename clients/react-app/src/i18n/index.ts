import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en.json';
import hi from './locales/hi.json';
import mr from './locales/mr.json';

const STORAGE_KEY = 'gpmh_lang';
export type SupportedLocale = 'mr' | 'hi' | 'en';

function resolveInitialLocale(): SupportedLocale {
  const saved = typeof window !== 'undefined' ? window.localStorage.getItem(STORAGE_KEY) : null;
  if (saved === 'mr' || saved === 'hi' || saved === 'en') {
    return saved;
  }
  return 'mr';
}

const initialLocale = resolveInitialLocale();
if (typeof document !== 'undefined') {
  document.documentElement.lang = initialLocale;
}

void i18n.use(initReactI18next).init({
  resources: {
    mr: { translation: mr },
    hi: { translation: hi },
    en: { translation: en },
  },
  lng: initialLocale,
  fallbackLng: 'mr',
  interpolation: {
    escapeValue: false,
  },
});

export function setAppLanguage(locale: SupportedLocale): void {
  void i18n.changeLanguage(locale);
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(STORAGE_KEY, locale);
  }
  if (typeof document !== 'undefined') {
    document.documentElement.lang = locale;
  }
}

export default i18n;

