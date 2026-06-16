# GameGaraj Kubernetes Config Assets

This folder contains non-secret configuration files that the GitHub Actions
deployment workflow turns into Kubernetes ConfigMaps.

Generated ConfigMaps:

- `gamegaraj-keycloak-realm` from `realm-init.json`
- `gamegaraj-initdbs` from `init-dbs.sql`
- `gamegaraj-redis-sentinel` from `redis/sentinel/*.conf`

Secrets are intentionally not stored here. Put secret values in GitHub
repository secrets and let `.github/workflows/k3s-config-sync.yml`
sync them into the `gamegaraj-secrets` Kubernetes Secret.

`realm-init.json` uses Keycloak import placeholders such as `${ADMIN_EMAIL}`
and `${ADMIN_PASSWORD}`. The Keycloak container that imports this file must
receive matching environment variables from secrets; otherwise the placeholders
will not be resolved during a fresh realm import.
