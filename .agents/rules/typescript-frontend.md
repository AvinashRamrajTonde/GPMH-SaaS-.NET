---
trigger: glob
globs: "**/frontend/**, clients/react-app/**/*.{ts,tsx}"
---

# Rule: React 19.3, TypeScript & Bilingual UI Standards

1. **Stack**: React 19.3 + Vite + TypeScript (`strict: true`, zero untyped `any`) + Tailwind CSS v4 + TanStack Query v5 + React Router 7 + `react-i18next` (`mr`, `hi`, `en`) + `lucide-react`.
2. **Runtime & LGD Integration**: Support runtime config or normalized `VITE_LGD_API_BASE_URL` (default `https://api.grampanchayatmh.in`, handling `/api/v1/` path cleanly without double slashes).
3. **Bilingual & Devanagari Standards**:
   - Zero hardcoded raw English UI strings — wrap all UI labels, table headers, toasts, and validations with `t(...)` in `mr`, `hi`, and `en` bundles.
   - Apply Noto Sans Devanagari and `tabular-nums` for rupee/ledger columns (`₹ 1,23,456.00`).
4. **UI States & Keyboard Workflow**: Explicit loading skeletons, RFC 9457 ProblemDetails error banners, empty states, and `Tab`/`Enter` keyboard-first fast flow for tax collection counters.
5. **Safety**: Never store refresh tokens in `localStorage` for web browsers (server uses `HttpOnly; Secure; SameSite=Strict` cookies; keep short-lived access token in memory). Never perform authoritative financial rounding solely in UI state.

