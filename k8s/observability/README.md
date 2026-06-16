# Kubernetes Dashboard

This folder contains the GameGaraj admin access manifest for Kubernetes
Dashboard. The Dashboard application itself should be installed from the
official Helm chart so it stays compatible with current Kubernetes versions.

## Install Dashboard

Run these commands on the K3s server or from a machine that has access to the
cluster kubeconfig:

The GitHub Actions workflow installs the chart directly from the Dashboard
GitHub release package because the legacy Helm repository index can return 404.

Manual install command:

```bash
helm upgrade --install kubernetes-dashboard \
  https://github.com/kubernetes/dashboard/releases/download/kubernetes-dashboard-7.14.0/kubernetes-dashboard-7.14.0.tgz \
  --namespace kubernetes-dashboard \
  --create-namespace

kubectl apply -f k8s/observability/dashboard-admin.yaml
```

## Open The UI

The GitHub Actions workflow `.github/workflows/k3s-dashboard-install.yml`
installs Dashboard and exposes it with NodePort `30443` by default.

Default browser URL:

```text
https://192.168.1.56:30443
```

If you prefer not to expose a NodePort, use a local proxy:

```bash
kubectl -n kubernetes-dashboard port-forward svc/kubernetes-dashboard-kong-proxy 8443:443
```

Then open:

```text
https://localhost:8443
```

If your chart version exposes a different service name, list services with:

```bash
kubectl -n kubernetes-dashboard get svc
```

## Get Login Token

```bash
kubectl -n kubernetes-dashboard get secret gamegaraj-dashboard-admin-token \
  -o jsonpath="{.data.token}" | base64 -d
```

Use that token on the Dashboard login screen.

## Security Note

`gamegaraj-dashboard-admin` is bound to `cluster-admin`. This is convenient for
a home lab and full cluster inspection, but it is very powerful. Do not expose
Dashboard directly to the internet. Prefer `kubectl port-forward`, VPN, or SSH
tunnel access.
