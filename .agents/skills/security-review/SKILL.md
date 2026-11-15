---
name: security-review
description: Audit C#/ASP.NET Core and React code for cross-tenant isolation, parameterized EF Core/SQL, DPDP Act 2023 Aadhaar masking & KYC encryption, JWT rotation, and CSRF/XSS defense.
---

# GPMH Security Audit Skill

Run before approving authentication, tenancy, file uploads, exports, or PII code:

1. **Tenant Selector Defense**: Verify `X-Tenant-ID` is NEVER accepted without validating active `TenantMembership` for `ClaimsPrincipal.UserId`. Confirm missing/unauthorized tenant returns HTTP `403 ProblemDetails`.
2. **Database Isolation**: Confirm EF Core Global Query Filter + PostgreSQL `FORCE ROW LEVEL SECURITY` (`set_config`) + `NOBYPASSRLS` role (`gpmh_app`) on all queries. Confirm `FromSqlRaw` NEVER uses string interpolation.
3. **PII / Aadhaar & KYC Docs**: Verify 12-digit Aadhaar is encrypted at rest and masked (`XXXX-XXXX-1234`) in API DTOs, logs, and Serilog scopes. Verify uploaded property photos and Agent Aadhaar/PAN KYC files sit in private object storage served via short-lived authenticated pre-signed URLs (never open `/uploads/*`).
4. **Refresh Token Rotation & Dual Storage**: Confirm 15m JWT access token in memory + 7d refresh token in `HttpOnly; Secure; SameSite=Strict` cookie for web, server-side SHA-256 hashed store, and reuse detection revoking the token family.
5. **Immutable Audit Table**: Confirm `REVOKE UPDATE, DELETE ON TABLE audit_logs FROM gpmh_app` is part of migrations and audit interceptor runs in the same transaction.

