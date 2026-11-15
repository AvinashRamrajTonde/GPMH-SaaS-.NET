#!/usr/bin/env bash
set -euo pipefail

# Provision non-superuser application database role that CANNOT bypass Row-Level Security (NOBYPASSRLS).
APP_PASSWORD="${GPMH_APP_DB_PASSWORD:-dev_gpmh_app_change_me}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'gpmh_app') THEN
            CREATE ROLE gpmh_app LOGIN PASSWORD '${APP_PASSWORD}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
        END IF;
    END
    \$\$;

    GRANT USAGE, CREATE ON SCHEMA public TO gpmh_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO gpmh_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO gpmh_app;
EOSQL

