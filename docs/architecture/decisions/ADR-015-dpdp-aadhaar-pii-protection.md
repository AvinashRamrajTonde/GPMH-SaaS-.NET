# ADR-015: India DPDP Act 2023, Aadhaar Protection & Private KYC Document Storage

## Status
Accepted

## Context
Citizens (`citizens.aadhaar_no`) and Field Agents (`agent_documents`: Aadhaar front/back, PAN card, cancelled cheque) handle sensitive national identity records. Prior drafts queried raw 12-digit Aadhaar strings and risked storing public uploads.

## Decision
1. **Aadhaar Encryption & Blind-Index Lookup:** Never store plaintext 12-digit Aadhaar in cleartext searchable columns or logs. Encrypt full value via AEAD (`AES-256-GCM`, KMS key per environment), store a HMAC-SHA256 blind index for exact duplicate/search matching, and expose **only masked `XXXX-XXXX-1234`** in DTOs, PDFs, and UI lists.
2. **Private Object Storage for KYC & Signatures:** Property photos, Gramsevak signatures, and Agent KYC docs reside in private buckets/volumes (outside static `wwwroot`) served via short-lived authenticated pre-signed URLs or authorized backend stream endpoints.
3. **Consent & Minimization:** Store explicit consent timestamps and retain KYC verification proofs under statutory audit retention schedules.

