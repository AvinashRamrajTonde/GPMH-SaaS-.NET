# ADR-017: Server-Side QuestPDF & Unified A4 Landscape Statutory Print Standard

## Status
Accepted (Supersedes old starter-kit ADR-007 & A3 Portrait/Landscape conflicts)

## Context
The old Node.js starter kit mandated CSS `@media print` + Headless Puppeteer (`ADR-007`) and conflicted with itself on whether Namuna 8 was A3 Landscape vs A4 Portrait and Namuna 10 was A4 Portrait vs A4 Landscape. Rural GP printers predominantly use standard **A4 sheets**.

## Decision
1. **QuestPDF Engine:** All official PDFs (`Namuna 8` 29-col extract, `Namuna 9` 2-page register, `Namuna 9-Ka` bills, `Namuna 10 & 11` dual receipts, Smart QR Nameplates, Audit Extracts) generate server-side using **QuestPDF** with embedded **Noto Sans Devanagari** fonts and `QRCoder`.
2. **Unified Page Geometry:** Standardize statutory registers and dual receipts onto **A4 Landscape (`297mm × 210mm`)** and bills onto **A5 / 2-in-1 A4 Landscape** so any standard rural A4 printer outputs crisp, zero-overflow statutory copies without Puppeteer/Chromium overhead.
3. **Dynamic UPI QR Elimination of File-Path Dependency:** Payment QRs are rendered dynamically via NPCI `upi://pay?pa={UpiId}&pn={GpName}&am={Amount}&tn={PropNo}&cu=INR` from verified GP `BankAccount.UpiId` records (static QR path columns removed from canonical tenant config).

