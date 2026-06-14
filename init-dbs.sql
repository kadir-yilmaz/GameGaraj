-- Create databases if they do not exist
SELECT 'CREATE DATABASE "GameGarajCatalogDb"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'GameGarajCatalogDb')\gexec
SELECT 'CREATE DATABASE "discountdb"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'discountdb')\gexec
SELECT 'CREATE DATABASE "keycloak"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak')\gexec
