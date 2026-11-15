---
name: release-readiness
description: Performs pre-release checks for a .NET and TypeScript SaaS application, including migrations, security, tests, deployment compatibility, and observability.
---

# SaaS release readiness

1. Identify the release scope, services, frontend assets, configuration changes, feature flags, and external dependencies.
2. Run formatting/analyzers, backend tests/build, frontend typecheck/lint/tests/build, and relevant integration/e2e tests.
3. Review API compatibility and generated TypeScript clients.
4. Review migrations for destructive changes, locks, backfills, startup ordering, and rollback/forward-fix strategy.
5. Review authentication, tenant isolation, permission changes, secrets, CORS, rate limits, file access, and audit behavior.
6. Verify structured logs, traces, health checks, dashboards, alerts, and failure/runbook coverage for changed components.
7. Verify backup/restore and recovery requirements for data-affecting releases. Do not execute production restore or destructive operations.
8. Identify rollout strategy: compatible deployment order, feature flags, canary/gradual rollout, and rollback limitations.
9. Produce a release checklist with PASS / FAIL / NOT RUN, evidence, owner/action, and residual risks.
10. Do not deploy or modify shared/production infrastructure without explicit approval.
