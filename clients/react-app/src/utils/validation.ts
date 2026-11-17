import { ApiProblemError } from '../services/apiClient';

export interface ParsedValidationItem {
  key: string;
  rawField: string;
  message: string;
}

export interface ParsedProblemResult {
  summary: string;
  items: ParsedValidationItem[];
  fieldErrors: Record<string, string>;
}

export function normalizeServerErrorKey(rawKey: string): string {
  if (!rawKey) return 'form';
  const stripped = rawKey.replace(/^Wizard\./i, '');
  const withDots = stripped.replace(/\[(\d+)\]\./g, '.$1.');
  return withDots
    .split('.')
    .map((seg) => (seg.length > 0 ? seg[0].toLowerCase() + seg.slice(1) : seg))
    .join('.');
}

export function extractProblemValidation(
  err: unknown,
  fallbackMessage: string,
): ParsedProblemResult {
  if (err instanceof ApiProblemError) {
    const errorsMap = err.problem.errors ?? {};
    const items: ParsedValidationItem[] = [];
    const fieldErrors: Record<string, string> = {};

    for (const [rawField, messages] of Object.entries(errorsMap)) {
      const normalized = normalizeServerErrorKey(rawField);
      const list = Array.isArray(messages) ? messages : [];
      if (list.length > 0 && !(normalized in fieldErrors)) {
        fieldErrors[normalized] = list[0];
      }
      for (const msg of list) {
        if (msg && msg.trim().length > 0) {
          items.push({ key: normalized, rawField, message: msg });
        }
      }
    }

    const summary =
      items.length > 0
        ? err.problem.detail || err.problem.title || fallbackMessage
        : err.problem.detail || err.message || fallbackMessage;

    return { summary, items, fieldErrors };
  }

  const msg = err instanceof Error ? err.message : fallbackMessage;
  return { summary: msg, items: [], fieldErrors: {} };
}

export interface PasswordStrengthResult {
  hasMinLen: boolean;
  hasUpper: boolean;
  hasLower: boolean;
  hasDigit: boolean;
  isValid: boolean;
}

export function evaluatePasswordStrength(password: string): PasswordStrengthResult {
  const hasMinLen = password.length >= 8;
  const hasUpper = /[A-Z]/.test(password);
  const hasLower = /[a-z]/.test(password);
  const hasDigit = /\d/.test(password);
  return {
    hasMinLen,
    hasUpper,
    hasLower,
    hasDigit,
    isValid: hasMinLen && hasUpper && hasLower && hasDigit,
  };
}

export function generateStrongPassword(): string {
  const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
  const lower = 'abcdefghijkmnopqrstuvwxyz';
  const digits = '23456789';
  const symbols = '@#$!';
  const all = upper + lower + digits;
  const pick = (pool: string) => pool[Math.floor(Math.random() * pool.length)];
  let pwd = pick(upper) + pick(lower) + pick(digits) + pick(symbols);
  for (let i = 0; i < 8; i++) {
    pwd += pick(all);
  }
  return pwd;
}

