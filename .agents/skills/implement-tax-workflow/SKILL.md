---
name: implement-tax-workflow
description: Implement or verify Maharashtra Gram Panchayat Capital-Value tax assessment, depreciation slabs, area slabs, water tax, rebate/penalty, and strict FIFO collection allocation.
---

# Implement Maharashtra Statutory Tax Workflow

Follow the locked formula in `docs/statutory/canonical-tax-specification.md`:

1. **Capital Value Building Tax Formula**:
   - `AreaSqm = AreaSqft * 0.09290304m` (or provided metric measurement)
   - `LandValue = AreaSqm * ConstructionType.LandAnnualValueRate`
   - `DepreciatedBuildingValue = AreaSqm * ConstructionType.ConstructionReadyReckonerRate * (DepreciationFactorPct / 100m)`
   - `TotalCapitalValue = LandValue + DepreciatedBuildingValue`
   - `RawBuildingTax = (TotalCapitalValue * UsageWeightage.Weightage * ConstructionType.TaxRate) / FormulaSetting.TaxDivisor` (`1000`)
   - Round via `Math.Round(RawBuildingTax, 0, MidpointRounding.AwayFromZero)`.
2. **Depreciation Boundary Rule**:
   - `Age = AssessmentStartYear - ConstructionStartYear`
   - Slab 1 (`0..2`) matches `Age >= 0 && Age <= 2`; all subsequent slabs (`2..5`, `5..10`, ...) match `Age > AgeFrom && Age <= AgeTo`.
3. **Golden Test Check (Chincholi Khurd)**:
   - `20.90 sqm`, Land `760`, RR `5852`, Age `5` (`95%`), Usage `1.0`, TaxRate `0.5`, TaxDivisor `1000` => **`BuildingTax = 66`**, Lighting `40` + Health `40` + Sanitation `100` (`AreaSlab 1..300`) => **`TotalTax = 246`**.
4. **Strict FIFO Allocation Order**:
   - Allocate payment in order: `Notice/Warrant/Penalty Fees -> Oldest Arrears (head-wise) -> Current Year Demand -> Excess Credit`.
5. **Immutable Assessment Snapshots**: Save component breakdowns at assessment time so future Ready Reckoner changes never rewrite historical Namuna 9 demands.

