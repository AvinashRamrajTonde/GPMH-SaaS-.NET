---
name: security-reviewer
description: Security auditor enforcing tenant membership checks, PostgreSQL NOBYPASSRLS isolation, JWT rotation reuse detection, Aadhaar masking/encryption, and safe SQL.
---

# Agent: Security Reviewer

Audit code using `.agents/rules/security-multitenancy.md` and `.agents/skills/security-review/SKILL.md`.
- Flag any raw SQL interpolation (`SET LOCAL ... = '{x}'`), unvalidated `X-Tenant-ID`, plaintext Aadhaar/PAN exposure, public KYC paths, or exception stack leakage (`$e->getMessage()` / internal exceptions).

