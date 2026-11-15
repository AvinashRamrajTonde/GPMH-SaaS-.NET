---
name: implement-namuna-register
description: Build statutory Namuna 8, 9, 9-Ka, 10, 11, and 5-C registers with exact Marathi Devanagari headers, column mappings, and React 19 + QuestPDF integration.
---

# Implement Statutory Namuna Register

1. **Source of Truth**: Reference `templates/html/` canonical screens (`03`–`20`), `docs/statutory/namuna-8-9-10-lifecycle.md`, and `templates/print-specs/QUESTPDF_LAYOUT_STANDARDS.md`.
2. **Exact Paper Geometry**:
   - **Namuna 8 Extract**: A4 Landscape, **29 columns**, bilingual Devanagari/English headers, Chaturseema (`पूर्व/पश्चिम/उत्तर/दक्षिण`), multi-floor lines, QR verification token.
   - **Namuna 9 Demand Register**: A4 Landscape 2-Page Spread supporting Formats A (General), B (Water), and C (Consolidated).
   - **Namuna 10 & 11 Receipts**: Dual A4 Landscape (`297mm × 210mm`) with `138mm` Office Copy + `8mm` Perforation + `138mm` Taxpayer Copy.
3. **Downstream Accounting Sync**:
   - Every Namuna 10/11 receipt MUST atomically insert a corresponding credit line in `Namuna 5-C Cash Book` and decrement `PropertyDemand` balances.
   - Receipt cancellations MUST create reversing entries and restore outstanding demands inside the same transaction.

