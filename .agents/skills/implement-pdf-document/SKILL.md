---
name: implement-pdf-document
description: Implement QuestPDF server-side vector PDF generators for Namuna 8, 9, 9-Ka, 10, 11, and Property QR plates with embedded Noto Sans Devanagari fonts and zero page overflow.
---

# Implement QuestPDF Statutory Documents

**QuestPDF** (C# server-side) is the authoritative PDF generator for GPMH. Do NOT add Puppeteer or Chromium dependencies.

1. **Fonts**: Bundle and register `NotoSansDevanagari-Regular.ttf` and `NotoSansDevanagari-Bold.ttf` via `QuestPDF.Drawing.FontManager.RegisterFont`. Verify complex Devanagari conjuncts (`त्र`, `ज्ञ`, `क्ष`, `्र`, `र्`) render crisply without fallback boxes.
2. **Page Dimensions**:
   - `Namuna 8 Extract`: `PageSizes.A4.Landscape()`, 29 columns, clean repeating table headers across pages.
   - `Namuna 10 Dual Receipt`: `PageSizes.A4.Landscape()`, 5mm vertical / 6mm horizontal margins, strict single-page fit (Left Office Copy + Perforation + Right Taxpayer Copy).
   - `Namuna 9-Ka Demand Bill`: A5 or 2-in-1 A4 Landscape with `QRCoder` SVG/PNG NPCI UPI payload (`upi://pay?pa=...&pn=...&am=...&tn=...&cu=INR`) and 15-day notice warning.
3. **Official Sign-Offs**: End extracts/registers with tripartite signature blocks (लिपिक / ग्रामसेवक / सरपंच) and receipts with dual clerk/Gramsevak signature blocks.

