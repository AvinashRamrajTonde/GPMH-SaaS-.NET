---
trigger: glob
globs: "**/Domain/**/*.cs, **/Tax*/**/*.cs, **/Collection/**/*.cs, **/Receipts/**/*.cs, **/Accounting/**/*.cs"
---

# Rule: Financial Arithmetic & Maharashtra Statutory Workflows

1. **Decimal Precision Only**: Use C# `decimal` exclusively for currency, area conversion, tax rates, weightages, and commissions. Never use `float` or `double`. PostgreSQL columns MUST use `numeric(14,2)` for money and `numeric(10,4)` for rates/weights/areas.
2. **Locked Building Tax Formula (`TaxDivisor = 1000`)**:
   - `LandCapitalValue = AreaSqm * LandAnnualValueRate`
   - `BuildingCapitalValue = AreaSqm * ConstructionReadyReckonerRate * DepreciationFactor`
   - `TotalCapitalValue = LandCapitalValue + BuildingCapitalValue`
   - `RawBuildingTax = (TotalCapitalValue * UsageWeightage * ConstructionTaxRate) / FormulaSetting.TaxDivisor`
   - Rounding: `MidpointRounding.AwayFromZero` to integer rupees (or configured precision per GP setting).
   - Depreciation Boundary: Match slab `0..2` inclusively; subsequent slabs use `Age > AgeFrom && Age <= AgeTo`.
3. **FIFO Collection Allocation Order (Strict)**:
   1. `NoticeFee` / `WarrantFee` / `Penalty` (Oldest first)
   2. `Arrears` (Oldest Assessment Year first, head-wise)
   3. `CurrentYear` Demand (head-wise)
   4. `ExcessCredit` (explicitly tracked, never dropped)
4. **Immutable Posted Records**: Posted Namuna 10/11 receipts and Namuna 5-C Cash Book entries are NEVER updated or hard-deleted. Corrections create reversing entries referencing the original ID + actor + statutory reason (`Gramsevak` / `SuperAdmin` only).
5. **Atomic Sequence Counters**: Use transactional row-level locking (`SELECT ... FOR UPDATE`) on `ReceiptCounters`, `VoucherCounters`, and `PropertyCounters` scoped by `(TenantId, FiscalYear)`.

