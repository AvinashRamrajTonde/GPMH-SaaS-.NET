---
name: design-accounting-tax-workflow
description: Designs or implements accounting, tax assessment, demand, collection, receipt, ledger, and statutory register workflows with auditability and financial correctness.
---

# Design an accounting or tax workflow

1. Obtain the approved business specification, statutory source, Namuna/register definition, financial-year rules, rates, effective dates, and permission matrix. Do not infer missing law or form fields.
2. Draw the lifecycle explicitly: source event -> assessment/demand -> bill/notice -> receipt/payment -> allocation -> posting -> reconciliation -> reversal/adjustment/closure as applicable.
3. Identify immutable records, editable drafts, approval transitions, sequence numbering, backdating, period locks, and audit requirements.
4. Define amount precision, calculation order, rounding, rate versioning, waivers, penalties, discounts, partial/advance payments, refunds, and overpayment treatment from approved requirements.
5. Model the accounting effect and balancing rules where the approved model requires double-entry accounting. Do not force an accounting model onto a register without confirmation.
6. Define idempotency keys and transaction boundaries so retries cannot create duplicate receipts or ledger postings.
7. Define concurrency behavior for two clerks collecting or editing the same account simultaneously.
8. Enforce tenant and role permissions on every operation. Sensitive corrections require actor, timestamp, reason, and original-record reference.
9. Add tests for calculation boundaries, financial year transitions, duplicate payments, partial payments, reversals, concurrent postings, and unauthorized operations.
10. Provide a traceability table from requirement/source -> domain rule -> implementation -> test.
