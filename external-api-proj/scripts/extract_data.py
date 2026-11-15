#!/usr/bin/env python3
"""
extract_data.py
Extracts essential Gram Panchayat directory data from meripanchayat_data.db
into a lightweight, indexed SQLite database for the external public PHP API.

Target fields:
- LGD Code (local_body_code)
- GP Name English (gp_name_en)
- GP Name Marathi (gp_name_mr)
- District (clean name, stripped of [code])
- Tehsil / Taluka (clean name, stripped of [code])
- Pincode (pincode)
"""

import os
import re
import sqlite3
import sys

SOURCE_DB = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../meripanchayat_data.db'))
TARGET_DB = os.path.abspath(os.path.join(os.path.dirname(__file__), '../data/panchayat_directory.db'))

def clean_bracket_name(val):
    if not val:
        return '', ''
    match = re.match(r'^(.*?)\s*\[(\d+)\]$', val.strip())
    if match:
        return match.group(1).strip(), match.group(2).strip()
    return val.strip(), ''

def extract():
    print(f"Opening source database: {SOURCE_DB}")
    if not os.path.exists(SOURCE_DB):
        print(f"Error: Source database not found at {SOURCE_DB}")
        sys.exit(1)

    src_conn = sqlite3.connect(SOURCE_DB)
    src_cur = src_conn.cursor()

    if os.path.exists(TARGET_DB):
        os.remove(TARGET_DB)

    print(f"Creating target database: {TARGET_DB}")
    tgt_conn = sqlite3.connect(TARGET_DB)
    tgt_cur = tgt_conn.cursor()

    tgt_cur.execute("PRAGMA journal_mode = WAL;")
    tgt_cur.execute("PRAGMA synchronous = NORMAL;")

    tgt_cur.execute("""
        CREATE TABLE panchayats (
            lgd_code TEXT PRIMARY KEY,
            gp_name_en TEXT NOT NULL,
            gp_name_mr TEXT NOT NULL,
            district TEXT NOT NULL,
            district_code TEXT,
            tehsil TEXT NOT NULL,
            tehsil_code TEXT,
            pincode TEXT
        );
    """)

    src_cur.execute("""
        SELECT local_body_code, gp_name_en, gp_name_mr, gp_name_raw, district_name, block_name, pincode
        FROM gp_profiles
        WHERE district_name IS NOT NULL AND district_name != ''
    """)

    rows = src_cur.fetchall()
    print(f"Found {len(rows)} valid records in source database.")

    insert_records = []
    for row in rows:
        lgd_code = str(row[0]).strip()
        gp_en = (row[1] or row[3] or '').strip()
        gp_mr = (row[2] or '').strip()
        if not gp_mr:
            gp_mr = gp_en # Fallback to English name if Marathi translation is absent

        raw_dist = row[4] or ''
        raw_block = row[5] or ''
        dist_name, dist_code = clean_bracket_name(raw_dist)
        tehsil_name, tehsil_code = clean_bracket_name(raw_block)

        pincode = (row[6] or '').strip()

        insert_records.append((
            lgd_code,
            gp_en,
            gp_mr,
            dist_name,
            dist_code,
            tehsil_name,
            tehsil_code,
            pincode
        ))

    tgt_cur.executemany("""
        INSERT INTO panchayats (
            lgd_code, gp_name_en, gp_name_mr, district, district_code, tehsil, tehsil_code, pincode
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    """, insert_records)

    print("Creating performance indexes...")
    tgt_cur.execute("CREATE INDEX idx_panchayats_district ON panchayats(district);")
    tgt_cur.execute("CREATE INDEX idx_panchayats_tehsil ON panchayats(district, tehsil);")
    tgt_cur.execute("CREATE INDEX idx_panchayats_pincode ON panchayats(pincode);")
    tgt_cur.execute("CREATE INDEX idx_panchayats_name_en ON panchayats(gp_name_en COLLATE NOCASE);")

    tgt_conn.commit()

    tgt_cur.execute("VACUUM;")
    tgt_cur.execute("PRAGMA optimize;")
    tgt_cur.execute("PRAGMA journal_mode = DELETE;")

    # Verify counts
    tgt_cur.execute("SELECT COUNT(*) FROM panchayats")
    total_count = tgt_cur.fetchone()[0]

    tgt_cur.execute("SELECT COUNT(DISTINCT district) FROM panchayats")
    dist_count = tgt_cur.fetchone()[0]

    tgt_cur.execute("SELECT COUNT(DISTINCT tehsil) FROM panchayats")
    tehsil_count = tgt_cur.fetchone()[0]

    tgt_conn.close()
    src_conn.close()

    db_size = os.path.getsize(TARGET_DB) / (1024 * 1024)
    print("\nExtraction Summary:")
    print(f"Total Panchayats: {total_count}")
    print(f"Distinct Districts: {dist_count}")
    print(f"Distinct Tehsils: {tehsil_count}")
    print(f"Database Size: {db_size:.2f} MB")
    print(f"Target DB path: {TARGET_DB}")

if __name__ == '__main__':
    extract()

