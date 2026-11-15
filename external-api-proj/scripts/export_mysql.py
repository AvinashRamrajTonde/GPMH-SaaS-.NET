#!/usr/bin/env python3
"""
export_mysql.py
Generates a MySQL / MariaDB compatible SQL dump file (panchayat_directory.sql)
from data/panchayat_directory.db for easy phpMyAdmin import on shared hosting.
"""

import os
import sqlite3

BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SQLITE_DB = os.path.join(BASE_DIR, 'data/panchayat_directory.db')
SQL_OUT = os.path.join(BASE_DIR, 'data/panchayat_directory.sql')

def export():
    print(f"Reading from SQLite: {SQLITE_DB}")
    conn = sqlite3.connect(SQLITE_DB)
    cur = conn.cursor()

    cur.execute("SELECT lgd_code, gp_name_en, gp_name_mr, district, district_code, tehsil, tehsil_code, pincode FROM panchayats")
    rows = cur.fetchall()

    print(f"Writing MySQL dump to: {SQL_OUT}")
    with open(SQL_OUT, 'w', encoding='utf-8') as f:
        f.write("-- Maharashtra Gram Panchayat Directory Database Dump\n")
        f.write("-- Character Set: utf8mb4 / Collation: utf8mb4_unicode_ci\n\n")
        f.write("SET FOREIGN_KEY_CHECKS = 0;\n")
        f.write("SET NAMES utf8mb4;\n\n")
        f.write("DROP TABLE IF EXISTS `panchayats`;\n")
        f.write("""CREATE TABLE `panchayats` (
    `lgd_code` VARCHAR(20) NOT NULL PRIMARY KEY,
    `gp_name_en` VARCHAR(150) NOT NULL,
    `gp_name_mr` VARCHAR(200) NOT NULL,
    `district` VARCHAR(100) NOT NULL,
    `district_code` VARCHAR(20) DEFAULT NULL,
    `tehsil` VARCHAR(100) NOT NULL,
    `tehsil_code` VARCHAR(20) DEFAULT NULL,
    `pincode` VARCHAR(10) DEFAULT NULL,
    INDEX `idx_district` (`district`),
    INDEX `idx_tehsil` (`district`, `tehsil`),
    INDEX `idx_pincode` (`pincode`),
    INDEX `idx_name_en` (`gp_name_en`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;\n\n""")

        f.write("LOCK TABLES `panchayats` WRITE;\n")
        batch_size = 500
        for i in range(0, len(rows), batch_size):
            batch = rows[i:i + batch_size]
            values = []
            for r in batch:
                lgd = "'" + r[0].replace("'", "''") + "'"
                en = "'" + r[1].replace("'", "''") + "'"
                mr = "'" + r[2].replace("'", "''") + "'"
                dist = "'" + r[3].replace("'", "''") + "'"
                d_code = "'" + r[4].replace("'", "''") + "'" if r[4] else "NULL"
                teh = "'" + r[5].replace("'", "''") + "'"
                t_code = "'" + r[6].replace("'", "''") + "'" if r[6] else "NULL"
                pin = "'" + r[7].replace("'", "''") + "'" if r[7] else "NULL"
                values.append(f"({lgd}, {en}, {mr}, {dist}, {d_code}, {teh}, {t_code}, {pin})")

            f.write("INSERT INTO `panchayats` (`lgd_code`, `gp_name_en`, `gp_name_mr`, `district`, `district_code`, `tehsil`, `tehsil_code`, `pincode`) VALUES\n")
            f.write(",\n".join(values) + ";\n")

        f.write("UNLOCK TABLES;\n")
        f.write("SET FOREIGN_KEY_CHECKS = 1;\n")

    conn.close()
    file_size_mb = os.path.getsize(SQL_OUT) / (1024 * 1024)
    print(f"Export completed successfully! Total records: {len(rows)}, SQL dump size: {file_size_mb:.2f} MB")

if __name__ == '__main__':
    export()

