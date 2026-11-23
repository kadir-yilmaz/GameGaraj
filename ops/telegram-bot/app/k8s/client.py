from __future__ import annotations

import json
import logging
import os
import ssl
import urllib.error
import urllib.parse
import urllib.request

log = logging.getLogger(__name__)
SA_DIR = "/var/run/secrets/kubernetes.io/serviceaccount"


class K8sClient:
    """Minimal Kubernetes API client using the pod's ServiceAccount."""

    def __init__(self) -> None:
        host = os.environ.get("KUBERNETES_SERVICE_HOST", "")
        port = os.environ.get("KUBERNETES_SERVICE_PORT", "")
        ca_path = f"{SA_DIR}/ca.crt"
        self.available = bool(host and port and os.path.exists(ca_path))
        self.base_url = f"https://{host}:{port}" if self.available else ""
        self.ssl_ctx = ssl.create_default_context(cafile=ca_path) if self.available else None

    def _sa_token(self) -> str:
        with open(f"{SA_DIR}/token", encoding="utf-8") as fh:
            return fh.read().strip()

    def get_json(self, path: str, timeout: int = 20) -> dict:
        if not self.available:
            raise RuntimeError("Kubernetes ServiceAccount ortamı yok")
        req = urllib.request.Request(
            self.base_url + path,
            headers={"Authorization": f"Bearer {self._sa_token()}"},
        )
        with urllib.request.urlopen(req, context=self.ssl_ctx, timeout=timeout) as resp:
            return json.load(resp)

    def get_text(self, path: str, timeout: int = 20) -> str:
        if not self.available:
            raise RuntimeError("Kubernetes ServiceAccount ortamı yok")
        req = urllib.request.Request(
            self.base_url + path,
            headers={"Authorization": f"Bearer {self._sa_token()}"},
        )
        with urllib.request.urlopen(req, context=self.ssl_ctx, timeout=timeout) as resp:
            return resp.read().decode("utf-8", errors="replace")

    def items(self, path: str) -> list:
        try:
            return self.get_json(path).get("items", []) or []
        except Exception as exc:
            log.warning("kget %s failed: %s", path, exc)
            return []

    def force_delete_pod(self, namespace: str, name: str, timeout: int = 15) -> bool:
        """Force-delete a pod stuck in Terminating (gracePeriodSeconds=0)."""
        if not self.available:
            return False
        ns_q = urllib.parse.quote(namespace)
        name_q = urllib.parse.quote(name)
        url = f"{self.base_url}/api/v1/namespaces/{ns_q}/pods/{name_q}?gracePeriodSeconds=0"
        body = json.dumps({"gracePeriodSeconds": 0, "propagationPolicy": "Background"}).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=body,
            method="DELETE",
            headers={
                "Authorization": f"Bearer {self._sa_token()}",
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(req, context=self.ssl_ctx, timeout=timeout):
                return True
        except urllib.error.HTTPError as exc:
            if exc.code == 404:
                return True
            log.warning("force_delete_pod %s/%s failed: HTTP %s", namespace, name, exc.code)
            return False
        except Exception as exc:
            log.warning("force_delete_pod %s/%s failed: %s", namespace, name, exc)
            return False
