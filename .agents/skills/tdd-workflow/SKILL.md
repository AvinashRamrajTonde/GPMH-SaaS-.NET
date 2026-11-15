---
name: tdd-workflow
description: Test-Driven Development workflow for GPMH. Write failing unit/integration tests first, verify RED failure, implement the minimal statutory solution to reach GREEN, then refactor with 80%+ coverage across financial, multi-tenancy, and statutory rules.
metadata:
  origin: ECC-adapted
  domain: GPMH Maharashtra Accounts Code 2011
---

# Skill: GPMH Test-Driven Development Workflow

Ensure all feature development, bug fixes, and statutory engine modifications follow strict Test-Driven Development (TDD) principles.

---

## When to Activate
- Developing statutory tax calculation algorithms (Namuna 8).
- Implementing demand roll-over and snapshot logic (Namuna 9).
- Implementing dual-copy collection & FIFO allocation (Namuna 10).
- Building property mutation and 4-directional sub-division split logic.
- Adding API endpoints or modifying database transactions.

---

## The GPMH TDD Cycle

### 1. RED (Write Failing Test First)
Before writing any business logic, author an automated test specifying the expected behavior:
- Define exact statutory inputs (e.g., Land rate ₹760, RR rate ₹5,852, 20.90 sq.m, 0.50% rate).
- Assert expected statutory output using exact decimal comparison (e.g., Building tax = ₹66.00).
- Run test: `npm test -- <test-file>`
- Confirm test fails for the expected reason (e.g., function not implemented or calculation mismatch).

### 2. GREEN (Implement Minimal Solution)
Write only the necessary code to make the test pass:
- Adhere strictly to the controller-service pattern.
- Use `decimal.js` for financial arithmetic (`ROUND_HALF_UP`).
- Use `prisma.$transaction` for multi-record operations.
- Scope all database queries by `gpId`.
- Run test: confirm GREEN (exit code 0).

### 3. REFACTOR (Clean Code with Safe Regression Guard)
Clean up implementation while keeping all tests passing:
- Eliminate code duplication.
- Extract complex formulas into domain value objects.
- Ensure strict TypeScript typing (zero `any`).

---

## Mandatory Test Invariants for GPMH

### A. Financial Accuracy Invariants
```typescript
import Decimal from 'decimal.js';

test('Tax calculation must match Maharashtra Accounts Code 2011 exact formula', () => {
  // Building Capital Value = Area (sq.m) * RR Rate * Usage Factor * Depreciation
  // Building Tax = Building Capital Value * Tax Rate %
  const areaSqM = new Decimal('20.90');
  const rrRate = new Decimal('5852');
  const usageFactor = new Decimal('1.0');
  const depreciation = new Decimal('1.0');
  const taxRate = new Decimal('0.005');

  const buildingVal = areaSqM.mul(rrRate).mul(usageFactor).mul(depreciation);
  const buildingTax = buildingVal.mul(taxRate).toDecimalPlaces(0, Decimal.ROUND_HALF_UP);

  expect(buildingTax.toNumber()).toBe(66); // Exactly ₹66.00 as per statutory sample
});
```

### B. Multi-Tenancy Invariant
```typescript
test('Cross-tenant data leakage must be strictly prevented', async () => {
  const res = await request(app)
    .get('/api/properties/prop-123')
    .set('Authorization', `Bearer ${tenantAToken}`);

  // When prop-123 belongs to Tenant B, Tenant A must receive 404 Not Found, never 200 or 403
  expect(res.status).toBe(404);
});
```

### C. Zero-Dues Gating Invariant
```typescript
test('Property mutation must fail if outstanding dues exist', async () => {
  const propertyWithDues = { id: 1, totalBalance: new Decimal('142.00') };
  await expect(mutationService.initiateMutation(propertyWithDues))
    .rejects.toThrow('ZERO_DUES_REQUIRED');
});
```

---

## Completion Checklist
- [ ] Failing test written and verified RED.
- [ ] Minimal code implemented and verified GREEN.
- [ ] Code refactored with clean architecture and strict typing.
- [ ] Test coverage $\ge 80\%$ on the target module.
- [ ] Zero IEEE-754 float math in financial logic.

