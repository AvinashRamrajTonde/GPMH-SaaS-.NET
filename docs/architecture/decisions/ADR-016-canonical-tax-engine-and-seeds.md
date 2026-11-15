# ADR-016: Canonical Property Tax Formula (`TaxDivisor = 1000`) & Master Seed Resolution

## Status
Accepted

## Context
Legacy markdown specifications disagreed on usage weights, depreciation slabs, area slab taxes, water rates, notice deadlines, rebate percentages, FIFO order, and building tax divisor (`%` vs `per 1000`), even producing a 10x mathematical error (`₹611.53` vs `₹66`) in prose explanations of the Chincholi Khurd sample property.

## Decision
Lock **`docs/statutory/canonical-tax-specification.md`** as the sole authoritative formula & master seed specification:
1. Formula computes **both Land Value and Depreciated Building Value** divided by `TaxDivisor = 1000` (`(AreaSqm * LandRate + AreaSqm * RRRate * DepPct/100) * UsageWeight * TaxRate / 1000`), verified to yield exact **`₹66`** on Chincholi Khurd (`20.90 sqm`, Age `5`, Hut/Mud `760 / 5852`, `95%`, `0.5 / 1000`).
2. Depreciation lookup resolves boundary overlaps via `Age > AgeFrom && Age <= AgeTo` after Slab 1 (`0..2`).
3. Captured live seeds (`1.0 / 1.2 / 1.25` weights, `40/40/100` area slabs, `0/720/720` water taxes) seed new GPs by default and remain editable per GP in `#/formula` with audit diff tracking.
4. Demand notices use **15-day** due window, **10%** rebate/penalty default, and **`Notice/Penalty -> Oldest Arrears -> Current Year`** FIFO allocation.

