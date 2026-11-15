# Canonical Statutory Tax Formula & Rule Specification (Locked)
> **Authority:** Maharashtra Village Panchayats Act, 1958 & Maharashtra Village Panchayats Accounts Code, 2011  
> **Resolution Status:** Locked Single Source of Truth (resolves prior formula/slab conflicts across `new_requirements.md`, `business-rules.md`, `functional-requirements.md`, and `DEV_GUIDE.md`).

---

## 1. Building & Capital Value Property Tax Formula (`Namuna 8`)

For every floor/construction block $i$ on a property:

1. **Metric Area Conversion:**
   $$\text{AreaSqm}_i = \text{LengthFt}_i \times \text{WidthFt}_i \times 0.09290304$$
   *(If measured area in `sq.m` is supplied directly and verified, store both `sq.ft` and `sq.m` using `decimal`.)*
2. **Floor Capital Value (Land Component + Depreciated Construction Component):**
   $$\text{LandValue}_i = \text{AreaSqm}_i \times \text{ConstructionType.LandAnnualValueRate}$$
   $$\text{DepreciatedBuildingValue}_i = \text{AreaSqm}_i \times \text{ConstructionType.ConstructionReadyReckonerRate} \times \frac{\text{DepreciationFactorPct}}{100}$$
   $$\text{FloorCapitalValue}_i = \text{LandValue}_i + \text{DepreciatedBuildingValue}_i$$
3. **Floor Building Tax (`TaxDivisor = 1000`):**
   $$\text{RawBuildingTax}_i = \frac{\text{FloorCapitalValue}_i \times \text{UsageWeightage.Weightage} \times \text{ConstructionType.TaxRate}}{\text{FormulaSetting.TaxDivisor}}$$
   $$\text{BuildingTax} = \text{round\_half\_away\_from\_zero}\!\left(\sum_i \text{RawBuildingTax}_i\right)$$
4. **Open Plot Tax (`खुली जागा / OpenLandTax`):**
   $$\text{OpenLandTax} = \text{round\_half\_away\_from\_zero}\!\left(\frac{\text{OpenAreaSqm} \times \text{OpenPlotLandRate} \times \text{OpenPlotTaxRate}}{\text{FormulaSetting.TaxDivisor}}\right)$$
5. **Total Annual Assessment:**
   $$\text{TotalTax} = \text{BuildingTax} + \text{OpenLandTax} + \text{LightingTax} + \text{HealthTax} + \text{SanitationTax} + \text{ToiletTax} + \text{WaterTax} + \text{OtherTax}$$

### Verified Golden Test Vector (Chincholi Khurd, Property #1)
* Inputs (`2026-27`): Built-up `15 × 15 ft = 225 sq.ft` (`20.90 sq.m`), Construction Type `झोपडी किंवा मातीची इमारत` (`LandAnnualValueRate = 760`, `ConstructionReadyReckonerRate = 5852`, `TaxRate = 0.5`), Construction Year `2021-22` (Age = `5`), Usage `निवासी` (`Weightage = 1.0`), `TaxDivisor = 1000`, Area Slab `1–300 sq.ft`.
* Age `5` matches Slab `2–5` via `Age > AgeFrom && Age <= AgeTo` → `HutMudPercent = 95%` (`0.95`).
* Calculation:
  $$(20.90 \times 760 + 20.90 \times 5852 \times 0.95) \times 1.0 \times \frac{0.5}{1000} = (15884 + 116191.46) \times 0.0005 = 66.0377 \rightarrow \mathbf{₹66}$$
* Area Slab Taxes (`1–300 sq.ft`): Lighting `₹40` + Health `₹40` + Sanitation `₹100` + Toilet `₹0` = `₹180`.
* Water Tax: `नाही` (`₹0`).
* **Total Tax:** `₹66 + ₹180 + ₹0 = ₹246`.

---

## 2. Depreciation Boundary Rule (9 Slabs)

$$\text{BuildingAge} = \text{AssessmentYear.StartYear} - \text{Construction.StartYear}$$

* Slab 1 (`0..2`): matches when `BuildingAge >= 0 && BuildingAge <= 2` (`100%` retained value).
* Slabs 2–9 (`2..5`, `5..10`, `10..20`, `20..30`, `30..40`, `40..50`, `50..60`, `60..100`): match strictly via **`BuildingAge > AgeFrom && BuildingAge <= AgeTo`** (upper-inclusive, eliminating boundary ambiguity at exact years `5, 10, 20, 30, 40, 50, 60`). Ages above `100` clamp to the final slab.

---

## 3. Canonical Default GP Formula Seeds (Customizable per GP)

Seeded cleanly on tenant provisioning (see [`statutory-master-seeds.json`](file:///home/avi/Desktop/REACT-GPMH/AMRAVATI-GPMH/GPMH-SaaS-.NET/docs/statutory/statutory-master-seeds.json)):
* **Usage Weightages (3):** `निवासी = 1.00`, `औद्योगिक = 1.20`, `वाणिज्य = 1.25`.
* **Area Slab Taxes (3):** `1–300 sq.ft (L:40, H:40, S:100, T:0)`, `301–700 sq.ft (L:50, H:50, S:100, T:20)`, `701+ sq.ft (L:100, H:100, S:100, T:0)`.
* **Water Tax (3):** `नाही = ₹0`, `सामान्य पाणी कर = ₹720`, `विशेष पाणी कर = ₹720`.
* **Policy Defaults:** `TaxDivisor = 1000`, `ApplyLightingHealthOnOpenPlot = true`, `RoundArea = true`.

*(All rates remain configurable per-tenant by `Gramsevak`/`GP_Admin` inside `#/formula` with full `AuditLog` tracking.)*

---

## 4. Demand (`Namuna 9 / 9-Ka`), Collection (`Namuna 10`) & Mutation Rules

1. **Notice Period & Rebate/Penalty:**
   - Demand Bill (`Namuna 9-Ka`) statutory payment grace period: **15 days** from bill service date.
   - Early Payment Rebate / Late Penalty default: **10%** (customizable per GP resolution within `FormulaSetting`).
2. **Strict FIFO Payment Allocation (`Namuna 10`):**
   1. `NoticeFee` / `WarrantFee` / `Penalty`
   2. Oldest `Arrears` (sorted oldest Assessment Year first, head-wise)
   3. `CurrentYear` Demand (head-wise)
   4. `ExcessCredit` (explicitly carried forward, never lost)
3. **Downstream Accounting Sync (`Namuna 5-C & 6`):**
   - Creating a Namuna 10 or Namuna 11 receipt MUST atomically post a credit line to `Namuna 5-C Cash Book` and decrement `PropertyDemand`.
   - Receipt cancellation (restricted to `Gramsevak` / `SuperAdmin` with mandatory reason) NEVER deletes rows; it records an audited cancellation + reversing `Namuna 5-C` entry + restores demand balance.
4. **Property Mutation & Sub-Division Split (`Namuna 8 Partition`):**
   - **Zero-Dues Gate:** Pending arrears + current balance MUST equal `₹0` before approving ownership partition (`mutation/split`).
   - **Area Conservation:** Sum of partitioned child plot/built-up areas MUST equal parent area within **`±0.1%`** tolerance.
   - **Hierarchical Numbering:** Child properties receive deterministic suffixes (`P/1`, `P/2`, ..., `P/k`) linked via `ParentPropertyId`.

---

## 5. Authoritative QuestPDF Document Formats (No Puppeteer)

* **Namuna 8 Extract:** **A4 Landscape**, **29 statutory columns**, repeating headers, Noto Sans Devanagari font, dynamic QR verification link, tripartite signature footer.
* **Namuna 9 Register:** **A4 Landscape 2-Page Spread** (Page 1 Demand + Page 2 Collection; Formats A/B/C).
* **Namuna 9-Ka Demand Bill:** **A5** or **2-in-1 A4 Landscape** with central perforation line, dynamic NPCI `upi://pay?pa=...` QR, and 15-day statutory warning.
* **Namuna 10 & Namuna 11 Receipts:** **A4 Landscape Dual Copy** (`297mm × 210mm` = `138mm` Office Copy + `8mm` Perforation + `138mm` Taxpayer Copy), zero overflow.

