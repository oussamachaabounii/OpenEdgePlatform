#!/usr/bin/env bash
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
  SELECT 'CREATE DATABASE edge_platform_control'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'edge_platform_control')\gexec
EOSQL
