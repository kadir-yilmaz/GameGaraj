#!/usr/bin/env python3
"""Telegram bot: reply to 'cluster info' with node and workload status. No links."""

from __future__ import annotations

import json
import os
import re
import ssl
import time
import urllib.error
import urllib.parse
import urllib.request

TOKEN_FILE = os.environ.get("TELEGRAM_TOKEN_FILE", "/telegram/token")
CHAT_ID = os.environ.get("TELEGRAM_CHAT_ID", "").strip()
APP_NS = os.environ.get("GAMEGARAJ_NAMESPACE", "gamegaraj")
SA_DIR = "/var/run/secrets/kubernetes.io/serviceaccount"
K8S_HOST = os.environ["KUBERNETES_SERVICE_HOST"]
K8S_PORT = os.environ["KUBERNETES_SERVICE_PORT"]
K8S_BASE = f"https://{K8S_HOST}:{K8S_PORT}"

COMMANDS = {
    "cluster info",
    "clusterinfo",
    "/clusterinfo",
    "/cluster",
    "/info",
    "info",
}

with open(TOKEN_FILE, encoding="utf-8") as fh:
    BOT_TOKEN = fh.read().strip()

with open(f"{SA_DIR}/token", encoding="utf-8") as fh:
    SA_TOKEN = fh.read().strip()

SSL_CTX = ssl.create_default_context(cafile=f"{SA_DIR}/ca.crt")
TG_BASE = f"https://api.telegram.org/bot{BOT_TOKEN}"


def http_json(url: str, data: dict | None = None, headers: dict | None = None, context=None, timeout=60):
    body = None
    req_headers = dict(headers or {})
    if data is not None:
        body = urllib.parse.urlencode(data).encode()
        req_headers.setdefault("Content-Type", "application/x-www-form-urlencoded")
    req = urllib.request.Request(url, data=body, headers=req_headers)
    with urllib.request.urlopen(req, context=context, timeout=timeout) as resp:
        return json.load(resp)


def kget(path: str) -> dict:
    return http_json(
        K8S_BASE + path,
        headers={"Authorization": f"Bearer {SA_TOKEN}"},
        context=SSL_CTX,
        timeout=20,
    )


def node_ready(node: dict) -> bool:
    for cond in node.get("status", {}).get("conditions", []):
        if cond.get("type") == "Ready":
            return cond.get("status") == "True"
    return False


def node_roles(node: dict) -> str:
    labels = node.get("metadata", {}).get("labels", {})
    roles = [key.split("/", 1)[1] for key in labels if key.startswith("node-role.kubernetes.io/")]
    return ",".join(sorted(roles)) or "worker"


def pod_problems(pod: dict) -> str | None:
    phase = pod.get("status", {}).get("phase", "")
    if phase in {"Succeeded", "Completed"}:
        return None
    name = pod.get("metadata", {}).get("name", "?")
    ns = pod.get("metadata", {}).get("namespace", "?")
    if phase in {"Pending", "Failed", "Unknown"}:
        return f"{ns}/{name} {phase}"
    for cs in pod.get("status", {}).get("containerStatuses", []) or []:
        waiting = (cs.get("state") or {}).get("waiting") or {}
        reason = waiting.get("reason")
        if reason or not cs.get("ready", False):
            if reason in {"PodInitializing", "ContainerCreating"}:
                continue
            if reason:
                return f"{ns}/{name} {reason}"
            if phase != "Running":
                return f"{ns}/{name} {phase}"
    return None


def cluster_info() -> str:
    nodes = kget("/api/v1/nodes").get("items", [])
    lines = ["Cluster"]
    lines.append("")
    lines.append("Nodes")
    for node in sorted(nodes, key=lambda n: n.get("metadata", {}).get("name", "")):
        name = node.get("metadata", {}).get("name", "?")
        ready = node_ready(node)
        mark = "Ready" if ready else "NotReady"
        icon = "🟢" if ready else "🔴"
        lines.append(f"{icon} {name}  {mark}  {node_roles(node)}")

    lines.append("")
    lines.append(f"{APP_NS}")
    try:
        deps = kget(f"/apis/apps/v1/namespaces/{APP_NS}/deployments").get("items", [])
    except Exception:
        deps = []
    if not deps:
        lines.append("deployment yok")
    else:
        for dep in sorted(deps, key=lambda d: d.get("metadata", {}).get("name", "")):
            name = dep.get("metadata", {}).get("name", "?")
            spec = (dep.get("spec") or {}).get("replicas") or 0
            ready = (dep.get("status") or {}).get("readyReplicas") or 0
            icon = "🟢" if ready == spec and spec > 0 else "🟡"
            lines.append(f"{icon} {name}  {ready}/{spec}")

    problems = []
    for pod in kget("/api/v1/pods").get("items", []):
        issue = pod_problems(pod)
        if issue:
            problems.append(issue)

    lines.append("")
    lines.append("Sorunlar")
    if problems:
        for item in problems[:20]:
            lines.append(f"⚠️ {item}")
        if len(problems) > 20:
            lines.append(f"... +{len(problems) - 20}")
    else:
        lines.append("yok")

    text = "\n".join(lines)
    return text[:4000]


def send_message(chat_id: str, text: str) -> None:
    http_json(
        f"{TG_BASE}/sendMessage",
        data={"chat_id": chat_id, "text": text, "disable_web_page_preview": "true"},
        timeout=30,
    )


def normalize(text: str) -> str:
    cleaned = re.sub(r"@\w+", "", text or "")
    return " ".join(cleaned.strip().lower().split())


def drain_pending() -> int:
    payload = http_json(f"{TG_BASE}/getUpdates", data={"timeout": "0"}, timeout=20)
    updates = payload.get("result") or []
    if not updates:
        return 0
    return int(updates[-1]["update_id"]) + 1


def main() -> None:
    if not CHAT_ID:
        raise SystemExit("TELEGRAM_CHAT_ID is empty")
    offset = 0
    try:
        offset = drain_pending()
    except Exception as exc:
        print(f"drain failed: {exc}", flush=True)

    print(f"listening chat_id={CHAT_ID} offset={offset}", flush=True)
    while True:
        try:
            payload = http_json(
                f"{TG_BASE}/getUpdates",
                data={"timeout": "50", "offset": str(offset), "allowed_updates": json.dumps(["message"])},
                timeout=70,
            )
            if not payload.get("ok"):
                time.sleep(3)
                continue
            for update in payload.get("result") or []:
                offset = int(update["update_id"]) + 1
                message = update.get("message") or {}
                chat = str((message.get("chat") or {}).get("id", ""))
                if chat != CHAT_ID:
                    continue
                text = normalize(message.get("text") or "")
                if text not in COMMANDS:
                    continue
                try:
                    send_message(chat, cluster_info())
                except Exception as exc:
                    send_message(chat, f"cluster info alınamadı: {exc}")
        except Exception as exc:
            print(f"poll error: {exc}", flush=True)
            time.sleep(5)


if __name__ == "__main__":
    main()
