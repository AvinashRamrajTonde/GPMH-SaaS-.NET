---
trigger: glob
globs: "**/*Pdf*.cs, **/Documents/**/*.cs, **/Print/**/*.cs, templates/print-specs/**"
---

# Rule: QuestPDF Statutory Document Generation Standards

1. **Primary Print Engine**: Use **QuestPDF** on the backend for all official receipts, notices, demand registers, and statutory extracts. Browser `@media print` may preview HTML prototypes only, never replace authoritative downloadable PDFs. (Puppeteer is NOT used.)
2. **Canonical Paper Sizes & Layouts (Locked)**:
   - **Namuna 8 Extract (`N8`)**: **A4 Landscape**, complete **29 statutory columns**, zero column clipping/overflow, Noto Sans Devanagari font embedded cleanly, QR verify link.
   - **Namuna 9 Demand Register**: **A4 Landscape 2-Page Spread** (Formats A General, B Water, C Consolidated).
   - **Namuna 9-Ka Demand Bill**: **A5** or **2-in-1 A4 Landscape** with central perforation line and dynamic `upi://pay?pa=...` QR code + 15-day notice warning.
   - **Namuna 10 & Namuna 11 Dual Receipts**: **A4 Landscape Dual Copy** (297mm × 210mm — Left 138mm Office/Daptar Copy | 8mm Perforation `✂️` | Right 138mm Payer Copy) with zero page overflow.
   - **Smart Property QR Plate**: 18"×9" single plate & 9-in-1 A4 sheet.
3. **Mandatory Devanagari & Signature Blocks**: Embed Noto Sans Devanagari TTF directly in QuestPDF font manager (`FontManager.RegisterFont`) so PDF rendering does not depend on host OS fonts, and end statutory registers with tripartite signatures (लिपिक, ग्रामसेवक, सरपंच).

