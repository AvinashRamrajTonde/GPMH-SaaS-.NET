#!/usr/bin/env python3
"""
Sync GitHub Project V2 Custom Fields for reconciled GPMH-SaaS-.NET issues (#5-#35).
Uses parameterized subprocess list arguments (no shell=True) and discovers board
options dynamically by name while falling back gracefully if new Sprint 6 / Epic 11-12
options have not yet been added in Project #2 UI.
"""

from __future__ import annotations

import json
import subprocess
import sys

PROJECT_ID = "PVT_kwHOB_vYis4BrWVp"
PROJECT_NUM = "2"
OWNER = "AvinashRamrajTonde"

# Reconciled 120-point backlog across Sprints 1-6 (#5-#35)
METADATA: dict[int, dict[str, object]] = {
    # Sprint 1 (23 pts)
    5:  {"sprint": "Sprint 1", "epic": "00-Foundation",         "priority": "P0-Critical", "role": "Fullstack-Agent", "points": 5},
    6:  {"sprint": "Sprint 1", "epic": "00-Foundation",         "priority": "P0-Critical", "role": "Backend-Agent",   "points": 5},
    7:  {"sprint": "Sprint 1", "epic": "00-Foundation",         "priority": "P1-High",     "role": "Backend-Agent",   "points": 3},
    8:  {"sprint": "Sprint 1", "epic": "01-External-LGD-API",   "priority": "P0-Critical", "role": "Backend-Agent",   "points": 3},
    9:  {"sprint": "Sprint 1", "epic": "01-External-LGD-API",   "priority": "P1-High",     "role": "DevOps-Agent",    "points": 2},
    24: {"sprint": "Sprint 1", "epic": "00-Foundation",         "priority": "P0-Critical", "role": "Backend-Agent",   "points": 5},
    # Sprint 2 (22 pts)
    10: {"sprint": "Sprint 2", "epic": "02-Tenant-Onboarding",  "priority": "P0-Critical", "role": "Frontend-Agent",  "points": 3},
    11: {"sprint": "Sprint 2", "epic": "02-Tenant-Onboarding",  "priority": "P0-Critical", "role": "Fullstack-Agent", "points": 5},
    12: {"sprint": "Sprint 2", "epic": "02-Tenant-Onboarding",  "priority": "P1-High",     "role": "Fullstack-Agent", "points": 3},
    25: {"sprint": "Sprint 2", "epic": "10-Village-Dashboard",  "priority": "P0-Critical", "role": "Frontend-Agent",  "points": 5},
    26: {"sprint": "Sprint 2", "epic": "02-Tenant-Onboarding",  "priority": "P1-High",     "role": "Fullstack-Agent", "points": 3},
    30: {"sprint": "Sprint 2", "epic": "09-Agent-Portal",       "priority": "P1-High",     "role": "Fullstack-Agent", "points": 3},
    # Sprint 3 (22 pts)
    13: {"sprint": "Sprint 3", "epic": "03-Formula-Masters",    "priority": "P0-Critical", "role": "Fullstack-Agent", "points": 3},
    14: {"sprint": "Sprint 3", "epic": "03-Formula-Masters",    "priority": "P1-High",     "role": "Backend-Agent",   "points": 3},
    15: {"sprint": "Sprint 3", "epic": "04-Namuna-8-Assessment","priority": "P0-Critical", "role": "Fullstack-Agent", "points": 5},
    16: {"sprint": "Sprint 3", "epic": "04-Namuna-8-Assessment","priority": "P1-High",     "role": "Frontend-Agent",  "points": 3},
    17: {"sprint": "Sprint 3", "epic": "04-Namuna-8-Assessment","priority": "P2-Medium",   "role": "Fullstack-Agent", "points": 3},
    27: {"sprint": "Sprint 3", "epic": "04-Namuna-8-Assessment","priority": "P1-High",     "role": "Fullstack-Agent", "points": 5},
    # Sprint 4 (10 pts)
    18: {"sprint": "Sprint 4", "epic": "05-Namuna-9-Demand",    "priority": "P0-Critical", "role": "Fullstack-Agent", "points": 5},
    19: {"sprint": "Sprint 4", "epic": "06-Namuna-9Ka-Bills",   "priority": "P0-Critical", "role": "Fullstack-Agent", "points": 5},
    # Sprint 5 (21 pts)
    20: {"sprint": "Sprint 5", "epic": "07-Namuna-10-Receipts", "priority": "P0-Critical", "role": "Fullstack-Agent", "points": 5},
    21: {"sprint": "Sprint 5", "epic": "07-Namuna-10-Receipts", "priority": "P1-High",     "role": "Frontend-Agent",  "points": 3},
    22: {"sprint": "Sprint 5", "epic": "08-SuperAdmin-HQ",      "priority": "P1-High",     "role": "Fullstack-Agent", "points": 3},
    23: {"sprint": "Sprint 5", "epic": "08-SuperAdmin-HQ",      "priority": "P2-Medium",   "role": "Fullstack-Agent", "points": 2},
    28: {"sprint": "Sprint 5", "epic": "09-Agent-Portal",       "priority": "P1-High",     "role": "Fullstack-Agent", "points": 5},
    29: {"sprint": "Sprint 5", "epic": "08-SuperAdmin-HQ",      "priority": "P1-High",     "role": "Fullstack-Agent", "points": 3},
    # Sprint 6 (22 pts - Phase 1 Completeness & Production Hardening)
    31: {"sprint": "Sprint 6", "epic": "07-Namuna-10-Receipts", "priority": "P0-Critical", "role": "Backend-Agent",   "points": 5},
    32: {"sprint": "Sprint 6", "epic": "11-Citizens-Migration", "priority": "P1-High",     "role": "Fullstack-Agent", "points": 5},
    33: {"sprint": "Sprint 6", "epic": "04-Namuna-8-Assessment","priority": "P1-High",     "role": "Fullstack-Agent", "points": 3},
    34: {"sprint": "Sprint 6", "epic": "12-Offline-Public-Verify","priority": "P1-High",   "role": "Fullstack-Agent", "points": 5},
    35: {"sprint": "Sprint 6", "epic": "05-Namuna-9-Demand",    "priority": "P1-High",     "role": "Backend-Agent",   "points": 4},
}


def run_args(args: list[str]) -> subprocess.CompletedProcess[str]:
    res = subprocess.run(args, capture_output=True, text=True, check=False)
    if res.returncode != 0:
        print(f"Error executing {' '.join(args)}:\n{res.stderr}", file=sys.stderr)
    return res


def run_graphql(query: str) -> dict:
    res = subprocess.run(["gh", "api", "graphql", "-f", f"query={query}"], capture_output=True, text=True, check=False)
    if res.returncode != 0:
        print(f"GraphQL Error:\n{res.stderr}", file=sys.stderr)
        sys.exit(1)
    return json.loads(res.stdout)


def load_board_metadata() -> tuple[dict[str, str], dict[str, dict[str, str]], dict[int, str]]:
    query = f"""
    query {{
      node(id: "{PROJECT_ID}") {{
        ... on ProjectV2 {{
          fields(first: 25) {{
            nodes {{
              ... on ProjectV2SingleSelectField {{
                id
                name
                options {{ id name }}
              }}
              ... on ProjectV2IterationField {{
                id
                name
                configuration {{ iterations {{ id title }} }}
              }}
              ... on ProjectV2Field {{
                id
                name
              }}
            }}
          }}
          items(first: 100) {{
            nodes {{
              id
              content {{
                ... on Issue {{ number }}
              }}
            }}
          }}
        }}
      }}
    }}
    """
    node = run_graphql(query)["data"]["node"]
    fields: dict[str, str] = {}
    options: dict[str, dict[str, str]] = {}

    for fld in node["fields"]["nodes"]:
        key = fld["name"].strip().lower()
        fields[key] = fld["id"]
        if "options" in fld:
            options[key] = {opt["name"]: opt["id"] for opt in fld["options"]}
        elif "configuration" in fld:
            options[key] = {it["title"]: it["id"] for it in fld["configuration"]["iterations"]}

    item_map = {
        it["content"]["number"]: it["id"]
        for it in node["items"]["nodes"]
        if it.get("content") and "number" in it["content"]
    }
    return fields, options, item_map


def edit_single_select(item_id: str, field_id: str, option_id: str) -> None:
    run_args([
        "gh", "project", "item-edit",
        "--id", item_id,
        "--project-id", PROJECT_ID,
        "--field-id", field_id,
        "--single-select-option-id", option_id,
    ])


def main() -> None:
    print(f"Syncing Project #{PROJECT_NUM} Custom Fields for issues #{min(METADATA)}–#{max(METADATA)}...")
    fields, options, item_map = load_board_metadata()

    for num in sorted(METADATA):
        if num not in item_map:
            print(f"Issue #{num} not present in Project #{PROJECT_NUM}; skipping.")
            continue

        item_id = item_map[num]
        meta = METADATA[num]
        print(f"Updating #{num}: {meta}")

        if "status" in fields and "Todo" in options.get("status", {}):
            edit_single_select(item_id, fields["status"], options["status"]["Todo"])

        if "sprint" in fields:
            sprint_id = options.get("sprint", {}).get(str(meta["sprint"]))
            if sprint_id:
                run_args([
                    "gh", "project", "item-edit",
                    "--id", item_id,
                    "--project-id", PROJECT_ID,
                    "--field-id", fields["sprint"],
                    "--iteration-id", sprint_id,
                ])
            else:
                print(f"  [WARN] Sprint iteration '{meta['sprint']}' not configured on Project #{PROJECT_NUM}.")

        for field_key in ("priority", "role", "epic"):
            val = str(meta[field_key])
            if field_key in fields:
                opt_id = options.get(field_key, {}).get(val)
                if opt_id:
                    edit_single_select(item_id, fields[field_key], opt_id)
                else:
                    print(f"  [WARN] Option '{val}' not configured for '{field_key}' on Project #{PROJECT_NUM}.")

        if "points" in fields:
            run_args([
                "gh", "project", "item-edit",
                "--id", item_id,
                "--project-id", PROJECT_ID,
                "--field-id", fields["points"],
                "--number", str(meta["points"]),
            ])

    print(f"Done. Inspect board: https://github.com/users/{OWNER}/projects/{PROJECT_NUM}")


if __name__ == "__main__":
    main()

