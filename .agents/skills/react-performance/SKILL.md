---
name: react-performance
description: React 18/19 and Vite performance optimization patterns for GPMH. Focuses on rendering wide statutory registers (Namuna 8, 9, 10), eliminating data-fetching waterfalls, memoizing tax computations, and ensuring zero-overflow print layouts.
metadata:
  origin: ECC-adapted
  domain: GPMH React/TypeScript Frontend
---

# Skill: GPMH React & Vite Performance Optimization

Techniques for keeping GPMH responsive when rendering complex bilingual tables, heavy tax calculation forms, and official print extracts.

---

## When to Activate
- Building or refactoring wide data registers (Namuna 8, Namuna 9 Demand Register, Namuna 10 Collection Register).
- Rendering multi-floor construction accordions in property registration forms.
- Designing `@media print` templates (Namuna 8-A, Namuna 9-क Demand Bills, Namuna 10 Dual-Receipts).
- Diagnosing slow page transitions or laggy Devanagari input typing.

---

## 1. Wide Register & Table Rendering (Namuna 8 / 9 / 10)

Statutory registers contain 20 to 30 columns and hundreds of village properties:

### A. Memoize Computed Rows
Never recompute tax formulas or format currency strings on every re-render:
```typescript
// PASS: Memoize row-level tax calculations
const memoizedProperties = useMemo(() => {
  return properties.map(prop => ({
    ...prop,
    totalAnnualTax: calculateAnnualTax(prop.constructions, prop.plotArea),
    formattedBalance: formatINR(prop.balance)
  }));
}, [properties]);
```

### B. Stable Callbacks in Action Cells
Avoid inline arrow functions in table action columns (`View`, `Edit`, `Print`, `Collect`):
```typescript
// FAIL: Causes every table row to re-render
<button onClick={() => handleCollect(row.id)}>पावती</button>

// PASS: Stable callback
const onCollect = useCallback((id: number) => {
  setSelectedPropertyId(id);
  setIsModalOpen(true);
}, []);
```

---

## 2. Eliminating Data-Fetching Waterfalls

Avoid sequential `await` calls that multiply network latency on dashboard or property detail pages:

```typescript
// FAIL: Waterfall fetching (doubles latency)
const property = await api.getProperty(id);
const taxAssessments = await api.getTaxAssessments(id);
const mutationHistory = await api.getMutations(id);

// PASS: Concurrent fetching via Promise.all or TanStack Query
const [property, taxAssessments, mutations] = await Promise.all([
  api.getProperty(id),
  api.getTaxAssessments(id),
  api.getMutations(id)
]);
```

---

## 3. High-Fidelity Zero-Overflow Print Performance

Statutory documents have strict page boundaries:
- **A4 Portrait**: Namuna 10 Dual-Receipt, Namuna 9-क Bills, Namuna 8-A Extract.
- **A3 Landscape**: Namuna 9 Demand Register, Namuna 26 Asset Register.

### Print CSS Rules:
```css
@media print {
  /* Prevent table rows from splitting across pages */
  tr, .card-row {
    page-break-inside: avoid;
    break-inside: avoid;
  }
  
  /* Suppress browser default header and footer */
  @page {
    margin: 8mm 6mm;
    size: A4 landscape; /* or portrait as specified */
  }

  /* Force exact background colors and borders */
  * {
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
  }
}
```

---

## 4. Devanagari Font Optimization
- Preload Noto Sans Devanagari font in `index.html`.
- Use `font-display: swap` to prevent Flash of Invisible Text (FOIT) during Marathi UI rendering.

