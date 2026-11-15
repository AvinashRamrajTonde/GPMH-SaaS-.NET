#!/usr/bin/env python3
"""
Synchronize GitHub Issues (#5-#35) and Project V2 (#2) metadata safely.
Repository: AvinashRamrajTonde/GPMH-SaaS-.NET
Security/Reliability fixes over legacy script:
  - Uses parameterized subprocess argument lists (zero shell=True command injection)
  - Uses tempfile.NamedTemporaryFile instead of shared /tmp/issue_body.md
  - Reconciles issues #5-#35 (120 pts across Sprints 1-6) with docs/planning/github-plan.md
"""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from sync_all_custom_fields import METADATA, OWNER, PROJECT_ID, PROJECT_NUM, load_board_metadata, run_args, run_graphql

REPO = "AvinashRamrajTonde/GPMH-SaaS-.NET"
ISSUE_SPECS: dict[int, dict[str, object]] = {
    5:  {"title": "[FOUNDATION] Clean Architecture Solution Scaffold (.NET + React 19.3 + i18n)", "labels": ["sprint-1", "epic:foundation", "p0-critical", "role:fullstack"]},
    6:  {"title": "[FOUNDATION] Multi-Tenant Schema & Parameterized PostgreSQL FORCE RLS", "labels": ["sprint-1", "epic:foundation", "p0-critical", "role:backend"]},
    7:  {"title": "[FOUNDATION] Identity, 8+1 Role RBAC & Rotating Refresh Tokens", "labels": ["sprint-1", "epic:foundation", "p1-high", "role:backend"]},
    8:  {"title": "[LGD-API] Expose RESTful Public LGD Directory Endpoints", "labels": ["sprint-1", "epic:lgd-api", "p0-critical", "role:backend"]},
    9:  {"title": "[LGD-API] SQLite Indexing, Search Safety & Hosting Hardening", "labels": ["sprint-1", "epic:lgd-api", "p1-high", "role:devops"]},
    24: {"title": "[FOUNDATION] Immutable Audit Trail & Field-Level JSON Diff Tracking", "labels": ["sprint-1", "epic:foundation", "p0-critical", "role:backend"]},
    10: {"title": "[ONBOARDING] Dual-Mode LGD Discovery Frontend UI (Code & Cascading Dropdowns)", "labels": ["sprint-2", "epic:onboarding", "p0-critical", "role:frontend"]},
    11: {"title": "[ONBOARDING] Auto-Fill Mapping & Allow Edits Override Badge Engine", "labels": ["sprint-2", "epic:onboarding", "p0-critical", "role:fullstack"]},
    12: {"title": "[ONBOARDING] Statutory Bank Accounts (Dynamic UPI Config) & Default Formula Seeding", "labels": ["sprint-2", "epic:onboarding", "p1-high", "role:fullstack"]},
    25: {"title": "[DASHBOARD] Village Executive Dashboard & Quick Actions Hub", "labels": ["sprint-2", "epic:dashboard", "p0-critical", "role:frontend"]},
    26: {"title": "[ONBOARDING] SuperAdmin Direct GP Tenant Provisioning Portal", "labels": ["sprint-2", "epic:onboarding", "p1-high", "role:fullstack"]},
    30: {"title": "[AGENT] Field Agent Self-Registration & Status-Gated Login", "labels": ["sprint-2", "epic:agent", "p1-high", "role:fullstack"]},
    13: {"title": "[MASTERS] 12 Construction Types, Land Annual Rates & Ready Reckoner Master", "labels": ["sprint-3", "epic:masters", "p0-critical", "role:fullstack"]},
    14: {"title": "[MASTERS] 9 Depreciation Slabs (Age<=To), Usage (1.0/1.2/1.25), Slabs & Water", "labels": ["sprint-3", "epic:masters", "p1-high", "role:backend"]},
    15: {"title": "[NAMUNA-8] Multi-Floor Property Registration & Real-Time TaxDivisor=1000 Calculator", "labels": ["sprint-3", "epic:namuna-8", "p0-critical", "role:fullstack"]},
    16: {"title": "[NAMUNA-8] Statutory 29-Column Namuna 8 QuestPDF (A4 Landscape) & Property Print", "labels": ["sprint-3", "epic:namuna-8", "p1-high", "role:fullstack"]},
    17: {"title": "[NAMUNA-8] Property Mutation & Ownership Transfer Register", "labels": ["sprint-3", "epic:namuna-8", "p2-medium", "role:fullstack"]},
    27: {"title": "[NAMUNA-8] Property Sub-Division & Multi-Heir Partition Wizard (Zero-Dues & +-0.1%)", "labels": ["sprint-3", "epic:namuna-8", "p1-high", "role:fullstack"]},
    18: {"title": "[NAMUNA-9] 2-Page Spread Demand Register, Arrears Rollover, 10% Rebate & Formats A/B/C", "labels": ["sprint-4", "epic:namuna-9", "p0-critical", "role:fullstack"]},
    19: {"title": "[NAMUNA-9KA] Batch Demand Bills QuestPDF (2-in-1 A4 Landscape / A5) + 15-Day Notice + UPI QR", "labels": ["sprint-4", "epic:namuna-9ka", "p0-critical", "role:fullstack"]},
    20: {"title": "[NAMUNA-10] Multi-Tax POS Collection Modal & Strict FIFO Allocation Engine", "labels": ["sprint-5", "epic:namuna-10", "p0-critical", "role:fullstack"]},
    21: {"title": "[NAMUNA-10] Dual A4 Landscape QuestPDF Receipt (138/8/138mm) & Cancellation Reversal", "labels": ["sprint-5", "epic:namuna-10", "p1-high", "role:frontend"]},
    22: {"title": "[SUPERADMIN] Statewide Aggregated KPI Dashboard & 15-Min Audited Support Impersonation", "labels": ["sprint-5", "epic:superadmin", "p1-high", "role:fullstack"]},
    23: {"title": "[SUPERADMIN] Subscription Payment Approval, UTR Check & 365-Day License Lifecycle", "labels": ["sprint-5", "epic:superadmin", "p2-medium", "role:fullstack"]},
    28: {"title": "[AGENT] Agent Workspace Dashboard, Referral Link & Commission Ledger", "labels": ["sprint-5", "epic:agent", "p1-high", "role:fullstack"]},
    29: {"title": "[SUPERADMIN] SuperAdmin Agent Governance, KYC Approvals & Payout Disbursements", "labels": ["sprint-5", "epic:superadmin", "p1-high", "role:fullstack"]},
    31: {"title": "[ACCOUNTS] Downstream Namuna 5-C Cash Book & Namuna 6 Auto-Credit / Reversal Sync", "labels": ["sprint-6", "epic:namuna-10", "p0-critical", "role:backend"]},
    32: {"title": "[CITIZENS] Citizen 360 Master, Aadhaar Masking/Encryption & 26-Col Excel Bulk Import", "labels": ["sprint-6", "epic:citizens", "p1-high", "role:fullstack"]},
    33: {"title": "[NAMUNA-8] Namuna 11 Misc Receipt Dual Print & Smart QR Nameplate", "labels": ["sprint-6", "epic:namuna-8", "p1-high", "role:fullstack"]},
    34: {"title": "[SYNC-VERIFY] Public Extract Verification Portal & Offline Namuna 6 Sync Queue", "labels": ["sprint-6", "epic:sync", "p1-high", "role:fullstack"]},
    35: {"title": "[FY-ROLLOVER] Annual FY Rollover Batch Job, Period Locking & UPI UTR Reconciliation", "labels": ["sprint-6", "epic:namuna-9", "p1-high", "role:backend"]},
}


def build_body(num: int) -> str:
    meta = METADATA[num]
    return "\n".join([
        f"### Issue #{num} — {ISSUE_SPECS[num]['title']}",
        "",
        f"- **Sprint:** {meta['sprint']} | **Epic:** `{meta['epic']}` | **Priority:** `{meta['priority']}` | **Role:** `{meta['role']}` | **Points:** {meta['points']}",
        "- **Canonical Specs:** `docs/planning/github-plan.md`, `docs/statutory/canonical-tax-specification.md`, `contracts/db/canonical-entities.sql`",
        "",
        "### Verification Checklist",
        "- [ ] Implementation matches clean architecture boundaries (`dotnet test GPMH.sln` + `GPMH.ArchitectureTests`).",
        "- [ ] Multi-tenancy (`TenantId` filter + PostgreSQL `FORCE ROW LEVEL SECURITY`) verified.",
        "- [ ] Audit trail JSON diffs and trilingual (`mr`, `hi`, `en`) UI labels verified.",
    ])


def upsert_issue(num: int, title: str, labels: list[str], body: str, dry_run: bool) -> None:
    if dry_run:
        print(f"[DRY-RUN] Issue #{num}: {title} ({','.join(labels)})")
        return

    with tempfile.NamedTemporaryFile(mode="w", suffix=".md", encoding="utf-8", delete=True) as tmp:
        tmp.write(body)
        tmp.flush()

        check = subprocess.run(
            ["gh", "issue", "view", str(num), "--repo", REPO, "--json", "id"],
            capture_output=True, text=True, check=False,
        )
        if check.returncode == 0:
            print(f"Updating #{num}: {title}")
            run_args(["gh", "issue", "edit", str(num), "--repo", REPO, "--title", title, "--body-file", tmp.name])
        else:
            print(f"Creating issue for spec #{num}: {title}")
            run_args([
                "gh", "issue", "create",
                "--repo", REPO,
                "--title", title,
                "--label", ",".join(labels),
                "--body-file", tmp.name,
            ])
            time.sleep(0.5)


def ensure_project_membership(issue_numbers: list[int], dry_run: bool) -> None:
    if dry_run:
        return
    _, _, item_map = load_board_metadata()
    for num in issue_numbers:
        if num in item_map:
            continue
        view = subprocess.run(
            ["gh", "issue", "view", str(num), "--repo", REPO, "--json", "id", "--jq", ".id"],
            capture_output=True, text=True, check=False,
        )
        if view.returncode != 0:
            continue
        node_id = view.stdout.strip()
        mutation = f'mutation {{ addProjectV2ItemById(input: {{ projectId: "{PROJECT_ID}", contentId: "{node_id}" }}) {{ item {{ id }} }} }}'
        run_graphql(mutation)
        print(f"Added Issue #{num} to Project #{PROJECT_NUM}.")


def main() -> None:
    dry_run = "--apply" not in sys.argv
    if dry_run:
        print("Running in DRY-RUN mode (pass --apply to execute GitHub mutations).")

    for num in sorted(ISSUE_SPECS):
        spec = ISSUE_SPECS[num]
        upsert_issue(num, str(spec["title"]), list(spec["labels"]), build_body(num), dry_run)

    ensure_project_membership(sorted(ISSUE_SPECS), dry_run)
    if not dry_run:
        from sync_all_custom_fields import main as sync_fields
        sync_fields()
    print(f"Done. Target Repo: https://github.com/{REPO} | Board: https://github.com/users/{OWNER}/projects/{PROJECT_NUM}")


if __name__ == "__main__":
    main()

