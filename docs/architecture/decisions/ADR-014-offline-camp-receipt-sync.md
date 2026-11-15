# ADR-014: Offline Tax Camp Sync & Monotonic Receipt Numbering (`Namuna 6` Book Ranges)

## Status
Accepted

## Context
Rural Maharashtra villages frequently lose 4G connectivity during Gram Sabha / Ward tax collection camps (`new_requirements.md §15`). Issuing `Namuna 10` tax receipts offline must never produce duplicate receipt numbers or sequence gaps, nor rely on dangerous Last-Write-Wins (LWW) overwrites on money.

## Decision
1. **Pre-Allocated Receipt Books (`Namuna 6` Range Model):** Before heading offline, an authorized clerk (`GP_Operator` / `Gramsevak`) checks out a numbered digital receipt book range (e.g., `Book B-07: Receipt 7001..7100` for current Fiscal Year) assigned uniquely to `(TenantId, FinancialYear, DeviceSessionId)` via an atomic DB transaction.
2. **Idempotent Offline Command Queue:** Offline receipts carry `(OperationId UUID, TenantId, BookNo, ReceiptSequence, PropertyId, ClientCapturedAtUtc)` and recalculate/validate head-wise FIFO splits server-side on sync.
3. **No LWW on Financial Records:** Duplicate retries matching `OperationId` return the existing committed receipt (idempotency); conflicts (e.g., property concurrently cancelled/split or year locked) surface an explicit server conflict outcome requiring `Gramsevak` resolution.

