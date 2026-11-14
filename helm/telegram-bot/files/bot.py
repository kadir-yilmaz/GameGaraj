#!/usr/bin/env python3
"""GameGaraj Telegram cluster bot.

Komutlar:
- cluster help           -> komut listesi
- cluster info           -> temiz DevOps cluster ozeti
- cluster info nodes     -> her node uzerindeki pod dagilimi ve durumlari
- cluster info gamegaraj -> gamegaraj servisleri, tag ve node dagilimi
- cluster info argocd    -> ArgoCD uygulamalari
- cluster info events    -> son 1 saat Warning event'leri ve sorunlu podlar
- nodes                  -> sadece node saglik durumu
- logs <servis|pod>      -> pod'un son 30 satir logu

Otomatik:
- Her saat basi           -> sadece node saglik raporu
- Node NotReady / Ready  -> anlik bildirim
"""

from __future__ import annotations

import json
import os
import re
import ssl
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

TOKEN_FILE = os.environ.get("TELEGRAM_TOKEN_FILE", "/telegram/token")
CHAT_ID = os.environ.get("TELEGRAM_CHAT_ID", "").strip()
APP_NS = os.environ.get("GAMEGARAJ_NAMESPACE", "gamegaraj")
ARGOCD_NS = os.environ.get("ARGOCD_NAMESPACE", "argocd")
TZ = timezone(timedelta(hours=float(os.environ.get("TZ_OFFSET_HOURS", "3"))))
CHECK_INTERVAL = int(os.environ.get("NODE_CHECK_INTERVAL", "30"))
DEBOUNCE_CHECKS = max(1, int(os.environ.get("NODE_DEBOUNCE_CHECKS", "2")))
HOURLY_REPORT = os.environ.get("HOURLY_REPORT", "true").lower() in {"1", "true", "yes"}

SA_DIR = "/var/run/secrets/kubernetes.io/serviceaccount"
K8S_HOST = os.environ["KUBERNETES_SERVICE_HOST"]
K8S_PORT = os.environ["KUBERNETES_SERVICE_PORT"]
K8S_BASE = f"https://{K8S_HOST}:{K8S_PORT}"

CLUSTER_COMMANDS = {"cluster info", "clusterinfo", "/clusterinfo", "/cluster", "/info", "info"}
CLUSTER_NODES_COMMANDS = {"cluster info nodes", "cluster nodes", "nodes detail", "/clusternodes"}
CLUSTER_APPS_COMMANDS = {"cluster info gamegaraj", "cluster info apps", "cluster gamegaraj", "gamegaraj", "apps"}
CLUSTER_ARGOCD_COMMANDS = {"cluster info argocd", "cluster argocd", "argocd"}
CLUSTER_EVENTS_COMMANDS = {"cluster info events", "cluster events", "events", "warnings"}
NODE_COMMANDS = {"nodes", "node", "node info", "/nodes", "/node"}
HELP_COMMANDS = {"cluster help", "cluster info help", "/help", "help", "/start", "yardım", "yardim"}

MAX_MSG = 3900
PRESSURE_CONDITIONS = ("MemoryPressure", "DiskPressure", "PIDPressure", "NetworkUnavailable")

with open(TOKEN_FILE, encoding="utf-8") as fh:
    BOT_TOKEN = fh.read().strip()

SSL_CTX = ssl.create_default_context(cafile=f"{SA_DIR}/ca.crt")
TG_BASE = f"https://api.telegram.org/bot{BOT_TOKEN}"


# ----------------------------------------------------------------- helpers
def log(msg: str) -> None:
    print(f"[{datetime.now(TZ):%Y-%m-%d %H:%M:%S}] {msg}", flush=True)


def http_json(url: str, data: dict | None = None, headers: dict | None = None, context=None, timeout=60):
    body = None
    req_headers = dict(headers or {})
    if data is not None:
        body = urllib.parse.urlencode(data).encode()
        req_headers.setdefault("Content-Type", "application/x-www-form-urlencoded")
    req = urllib.request.Request(url, data=body, headers=req_headers)
    with urllib.request.urlopen(req, context=context, timeout=timeout) as resp:
        return json.load(resp)


def http_text(url: str, headers: dict | None = None, context=None, timeout=20) -> str:
    req = urllib.request.Request(url, headers=dict(headers or {}))
    with urllib.request.urlopen(req, context=context, timeout=timeout) as resp:
        return resp.read().decode("utf-8", errors="replace")


def sa_token() -> str:
    with open(f"{SA_DIR}/token", encoding="utf-8") as fh:
        return fh.read().strip()


def kget(path: str) -> dict:
    return http_json(
        K8S_BASE + path,
        headers={"Authorization": f"Bearer {sa_token()}"},
        context=SSL_CTX,
        timeout=20,
    )


def kget_text(path: str) -> str:
    return http_text(
        K8S_BASE + path,
        headers={"Authorization": f"Bearer {sa_token()}"},
        context=SSL_CTX,
        timeout=20,
    )


def kitems(path: str) -> list:
    try:
        return kget(path).get("items", []) or []
    except Exception as exc:
        log(f"kget {path} failed: {exc}")
        return []


def now_tr() -> str:
    return datetime.now(TZ).strftime("%d.%m.%Y %H:%M")


def parse_ts(value: str | None) -> datetime | None:
    if not value:
        return None
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None


def fmt_duration(seconds: float) -> str:
    seconds = int(max(0, seconds))
    days, rem = divmod(seconds, 86400)
    hours, rem = divmod(rem, 3600)
    minutes, secs = divmod(rem, 60)
    if days:
        return f"{days}g {hours}sa"
    if hours:
        return f"{hours}sa {minutes}dk"
    if minutes:
        return f"{minutes}dk {secs}sn"
    return f"{secs}sn"


def age(value: str | None) -> str:
    ts = parse_ts(value)
    if not ts:
        return "?"
    return fmt_duration((datetime.now(timezone.utc) - ts).total_seconds())


def parse_cpu(value) -> float:
    if value is None:
        return 0.0
    s = str(value)
    try:
        if s.endswith("n"):
            return float(s[:-1]) / 1e9
        if s.endswith("u"):
            return float(s[:-1]) / 1e6
        if s.endswith("m"):
            return float(s[:-1]) / 1e3
        return float(s)
    except ValueError:
        return 0.0


_BIN = {"Ki": 1024, "Mi": 1024**2, "Gi": 1024**3, "Ti": 1024**4, "Pi": 1024**5, "Ei": 1024**6}
_DEC = {"k": 1e3, "K": 1e3, "M": 1e6, "G": 1e9, "T": 1e12, "P": 1e15, "E": 1e18}


def parse_mem(value) -> float:
    if value is None:
        return 0.0
    s = str(value)
    try:
        if s[-2:] in _BIN:
            return float(s[:-2]) * _BIN[s[-2:]]
        if s and s[-1] in _DEC:
            return float(s[:-1]) * _DEC[s[-1]]
        return float(s)
    except ValueError:
        return 0.0


def gib(value: float) -> str:
    return f"{value / 1024**3:.1f}"


def pct(part: float, total: float) -> str:
    if total <= 0:
        return "?"
    return f"%{part / total * 100:.0f}"


def bar_icon(ratio: float) -> str:
    if ratio >= 0.90:
        return "🔴"
    if ratio >= 0.75:
        return "🟡"
    return "🟢"


def chunk_lines(lines: list[str], limit: int = MAX_MSG) -> list[str]:
    chunks, cur, size = [], [], 0
    for line in lines:
        if size + len(line) + 1 > limit and cur:
            chunks.append("\n".join(cur))
            cur, size = [], 0
        cur.append(line[:limit])
        size += len(line) + 1
    if cur:
        chunks.append("\n".join(cur))
    return chunks


# -------------------------------------------------------------- node data
def node_name(node: dict) -> str:
    return node.get("metadata", {}).get("name", "?")


def node_condition(node: dict, ctype: str) -> dict:
    for cond in node.get("status", {}).get("conditions", []) or []:
        if cond.get("type") == ctype:
            return cond
    return {}


def node_ready(node: dict) -> bool:
    return node_condition(node, "Ready").get("status") == "True"


def node_roles(node: dict) -> str:
    labels = node.get("metadata", {}).get("labels", {})
    roles = [key.split("/", 1)[1] for key in labels if key.startswith("node-role.kubernetes.io/")]
    roles = [r for r in roles if r != "master"] or roles
    return ",".join(sorted(roles)) or "worker"


def node_pressures(node: dict) -> list[str]:
    return [c for c in PRESSURE_CONDITIONS if node_condition(node, c).get("status") == "True"]


def node_not_ready_reason(node: dict) -> str:
    cond = node_condition(node, "Ready")
    reason = cond.get("reason") or "Unknown"
    message = (cond.get("message") or "").strip()
    return f"{reason} - {message[:100]}" if message else reason


def node_metrics() -> dict:
    try:
        items = kget("/apis/metrics.k8s.io/v1beta1/nodes").get("items", [])
    except Exception:
        return {}
    return {m.get("metadata", {}).get("name"): m.get("usage", {}) for m in items}


def pod_stats(pods: list) -> dict:
    stats: dict[str, dict] = {}
    for pod in pods:
        node = (pod.get("spec") or {}).get("nodeName")
        phase = (pod.get("status") or {}).get("phase")
        if not node or phase in {"Succeeded", "Failed"}:
            continue
        s = stats.setdefault(node, {"pods": 0, "cpu_req": 0.0, "mem_req": 0.0})
        s["pods"] += 1
        for c in (pod.get("spec") or {}).get("containers", []) or []:
            req = (c.get("resources") or {}).get("requests") or {}
            s["cpu_req"] += parse_cpu(req.get("cpu"))
            s["mem_req"] += parse_mem(req.get("memory"))
    return stats


def node_line_compact(node: dict, usage: dict, stats: dict) -> str:
    name = node_name(node)
    ready = node_ready(node)
    cordoned = bool((node.get("spec") or {}).get("unschedulable"))
    alloc = (node.get("status") or {}).get("allocatable") or {}
    cpu_alloc = parse_cpu(alloc.get("cpu"))
    mem_alloc = parse_mem(alloc.get("memory"))
    pod_alloc = int(alloc.get("pods") or 0)
    ready_since = age(node_condition(node, "Ready").get("lastTransitionTime"))
    role = "cp" if "control-plane" in node_roles(node) else node_roles(node)

    if not ready:
        return f"🔴 {name} ({role}) · NotReady · {ready_since}'dır"

    st = stats.get(name, {"pods": 0, "cpu_req": 0.0, "mem_req": 0.0})
    icon = "🟠" if cordoned else "🟢"
    if usage:
        cpu_used = parse_cpu(usage.get("cpu"))
        mem_used = parse_mem(usage.get("memory"))
        res_txt = (
            f"CPU {pct(cpu_used, cpu_alloc)} ({cpu_used:.1f}/{cpu_alloc:.0f})"
            f" · RAM {pct(mem_used, mem_alloc)} ({gib(mem_used)}/{gib(mem_alloc)}G)"
        )
    else:
        res_txt = (
            f"CPU -/{cpu_alloc:.0f} · RAM -/{gib(mem_alloc)}G"
            f" (req {pct(st['cpu_req'], cpu_alloc)}/{pct(st['mem_req'], mem_alloc)})"
        )
    pressures = node_pressures(node)
    warn = f" ⚠️{','.join(pressures)}" if pressures else ""
    return f"{icon} {name} ({role}) · {res_txt} · {st['pods']}/{pod_alloc} pod{warn}"


def node_block(node: dict, usage: dict, stats: dict) -> list[str]:
    name = node_name(node)
    ready = node_ready(node)
    cordoned = bool((node.get("spec") or {}).get("unschedulable"))
    alloc = (node.get("status") or {}).get("allocatable") or {}
    cpu_alloc = parse_cpu(alloc.get("cpu"))
    mem_alloc = parse_mem(alloc.get("memory"))
    pod_alloc = int(alloc.get("pods") or 0)
    ready_since = age(node_condition(node, "Ready").get("lastTransitionTime"))

    if not ready:
        return [
            f"🔴 {name} ({node_roles(node)})",
            f"   ❌ NotReady · {ready_since}'dır",
            f"   Sebep: {node_not_ready_reason(node)}",
        ]

    icon = "🟠" if cordoned else "🟢"
    state = "Ready,SchedulingDisabled" if cordoned else "Ready"
    st = stats.get(name, {"pods": 0, "cpu_req": 0.0, "mem_req": 0.0})
    lines = [f"{icon} {name} ({node_roles(node)})"]
    if usage:
        cpu_used = parse_cpu(usage.get("cpu"))
        mem_used = parse_mem(usage.get("memory"))
        cpu_r = cpu_used / cpu_alloc if cpu_alloc else 0
        mem_r = mem_used / mem_alloc if mem_alloc else 0
        lines.append(
            f"   {bar_icon(cpu_r)} CPU {pct(cpu_used, cpu_alloc)} ({cpu_used:.1f}/{cpu_alloc:.0f} core)"
            f"  {bar_icon(mem_r)} RAM {pct(mem_used, mem_alloc)} ({gib(mem_used)}/{gib(mem_alloc)} GiB)"
        )
    else:
        lines.append(f"   CPU {cpu_alloc:.0f} core · RAM {gib(mem_alloc)} GiB (metrics yok)")
    lines.append(f"   Pod {st['pods']}/{pod_alloc} · {state} {ready_since}'dır")
    pressures = node_pressures(node)
    if pressures:
        lines.append(f"   ⚠️ {', '.join(pressures)}")
    return lines


# -------------------------------------------------------------- pod helpers
def is_job_pod(pod: dict) -> bool:
    for owner in (pod.get("metadata") or {}).get("ownerReferences", []) or []:
        if owner.get("kind") in {"Job", "CronJob"}:
            return True
    return False


def pod_state(pod: dict) -> tuple[str, bool]:
    """(durum metni, saglikli mi) doner."""
    meta = pod.get("metadata") or {}
    if meta.get("deletionTimestamp"):
        return "Terminating", False
    status = pod.get("status") or {}
    phase = status.get("phase", "Unknown")
    if phase in {"Succeeded", "Completed"}:
        return "Completed", True
    if phase in {"Pending", "Failed", "Unknown"}:
        reason = status.get("reason")
        for cs in status.get("containerStatuses", []) or []:
            w = (cs.get("state") or {}).get("waiting") or {}
            if w.get("reason"):
                reason = w["reason"]
        return (f"{phase} ({reason})" if reason else phase), False
    for cs in status.get("containerStatuses", []) or []:
        w = (cs.get("state") or {}).get("waiting") or {}
        reason = w.get("reason")
        if reason:
            return reason, reason in {"PodInitializing", "ContainerCreating"}
        if not cs.get("ready", False):
            return "NotReady", False
    return "Running", True


def pod_restarts(pod: dict) -> tuple[int, str]:
    total = 0
    last_reason = ""
    for cs in (pod.get("status") or {}).get("containerStatuses", []) or []:
        total += cs.get("restartCount", 0)
        term = (cs.get("lastState") or {}).get("terminated") or {}
        if term.get("reason"):
            last_reason = term["reason"]
    return total, last_reason


def workload_counts(kind: str, items: list, spec_key: str, ready_key: str) -> tuple[int, int, list[str]]:
    healthy = 0
    bad = []
    for it in items:
        meta = it.get("metadata") or {}
        status = it.get("status") or {}
        if kind == "DS":
            want = status.get("desiredNumberScheduled") or 0
        else:
            want = (it.get("spec") or {}).get(spec_key)
            want = 1 if want is None else want
        have = status.get(ready_key) or 0
        if have >= want:
            healthy += 1
        else:
            bad.append(f"🟡 {kind} {meta.get('namespace')}/{meta.get('name')} {have}/{want}")
    return healthy, len(items), bad


def recent_warnings(hours: int = 1) -> tuple[int, list[tuple[str, int, str]]]:
    events = kitems("/api/v1/events?fieldSelector=type%3DWarning")
    cutoff = datetime.now(timezone.utc) - timedelta(hours=hours)
    grouped: dict[tuple[str, str], dict] = {}
    total = 0
    for ev in events:
        ts = parse_ts(ev.get("lastTimestamp") or ev.get("eventTime") or (ev.get("metadata") or {}).get("creationTimestamp"))
        if not ts or ts < cutoff:
            continue
        total += 1
        obj = ev.get("involvedObject") or {}
        target = f"{obj.get('kind', '?')} {obj.get('namespace') or '-'}/{obj.get('name', '?')}"
        reason = ev.get("reason") or "Warning"
        key = (target, reason)
        entry = grouped.get(key)
        if not entry or ts > entry["ts"]:
            msg = (ev.get("message") or "").replace("\n", " ")[:75]
            grouped[key] = {"ts": ts, "count": (entry["count"] if entry else 0) + 1, "line": f"{target}: {reason} ({msg})"}
        else:
            entry["count"] += 1
    items = sorted(grouped.values(), key=lambda x: x["ts"], reverse=True)
    return total, [(x["ts"].astimezone(TZ).strftime("%H:%M"), x["count"], x["line"]) for x in items]


# ---------------------------------------------------------------- reports
def node_report(title: str) -> str:
    nodes = sorted(kget("/api/v1/nodes").get("items", []), key=node_name)
    metrics = node_metrics()
    stats = pod_stats(kitems("/api/v1/pods"))
    ready_count = sum(1 for n in nodes if node_ready(n))
    head = "✅" if ready_count == len(nodes) else "⚠️"

    lines = [f"{title} · {now_tr()}", f"{head} {ready_count}/{len(nodes)} node Ready", ""]
    for node in nodes:
        lines.extend(node_block(node, metrics.get(node_name(node), {}), stats))
    return "\n".join(lines)


def cluster_info() -> list[str]:
    try:
        ver = kget("/version").get("gitVersion", "?")
    except Exception:
        ver = "?"

    nodes = sorted(kget("/api/v1/nodes").get("items", []), key=node_name)
    pods = kitems("/api/v1/pods")
    metrics = node_metrics()
    stats = pod_stats(pods)
    ready_nodes = sum(1 for n in nodes if node_ready(n))

    running = sum(1 for p in pods if (p.get("status") or {}).get("phase") == "Running" and not (p.get("metadata") or {}).get("deletionTimestamp"))
    pvcs = kitems("/api/v1/persistentvolumeclaims")
    bound_pvcs = sum(1 for p in pvcs if (p.get("status") or {}).get("phase") == "Bound")

    head_icon = "✅" if ready_nodes == len(nodes) else "⚠️"
    lines: list[str] = [
        f"📊 Cluster Info · {now_tr()} · {ver}",
        f"{head_icon} {ready_nodes}/{len(nodes)} Node Ready · {running} Running pod · {bound_pvcs}/{len(pvcs)} PVC",
        "",
        "🖥 Nodes",
    ]
    for node in nodes:
        lines.append(node_line_compact(node, metrics.get(node_name(node), {}), stats))

    # Workloads + GameGaraj + ArgoCD
    d_ok, d_all, d_bad = workload_counts("Deploy", kitems("/apis/apps/v1/deployments"), "replicas", "readyReplicas")
    s_ok, s_all, s_bad = workload_counts("STS", kitems("/apis/apps/v1/statefulsets"), "replicas", "readyReplicas")
    ds_ok, ds_all, ds_bad = workload_counts("DS", kitems("/apis/apps/v1/daemonsets"), "", "numberReady")

    lines += ["", "⚙️ Workloads & Servisler"]
    lines.append(f"• K8s: Deploy {d_ok}/{d_all} · STS {s_ok}/{s_all} · DS {ds_ok}/{ds_all}")
    lines.extend(f"  {b}" for b in (d_bad + s_bad + ds_bad)[:8])

    deps = sorted(kitems(f"/apis/apps/v1/namespaces/{APP_NS}/deployments"), key=lambda d: d["metadata"]["name"])
    if deps:
        ok_deps = [d for d in deps if ((d.get("status") or {}).get("readyReplicas") or 0) >= ((d.get("spec") or {}).get("replicas") or 0) > 0]
        if len(ok_deps) == len(deps):
            lines.append(f"• {APP_NS}: 🟢 {len(deps)}/{len(deps)} servis hazır")
        else:
            lines.append(f"• {APP_NS}: 🟡 {len(ok_deps)}/{len(deps)} servis hazır")
            for d in deps:
                spec = (d.get("spec") or {}).get("replicas") or 0
                ready = (d.get("status") or {}).get("readyReplicas") or 0
                if ready < spec:
                    lines.append(f"  🟡 {d['metadata']['name']} {ready}/{spec}")

    apps = sorted(kitems(f"/apis/argoproj.io/v1alpha1/namespaces/{ARGOCD_NS}/applications"), key=lambda a: a["metadata"]["name"])
    if apps:
        bad_apps = []
        for a in apps:
            st = a.get("status") or {}
            sync = (st.get("sync") or {}).get("status", "?")
            health = (st.get("health") or {}).get("status", "?")
            if not (sync == "Synced" and health == "Healthy"):
                bad_apps.append(f"{a['metadata']['name']} ({sync}/{health})")
        if not bad_apps:
            lines.append(f"• ArgoCD: 🟢 {len(apps)}/{len(apps)} Synced/Healthy")
        else:
            lines.append(f"• ArgoCD: 🟡 {len(apps) - len(bad_apps)}/{len(apps)} Healthy · " + ", ".join(bad_apps[:4]))

    # Dikkat gerekenler
    app_issues = []
    job_counts: dict[str, int] = {}
    restarts = []
    for pod in pods:
        meta = pod.get("metadata") or {}
        ref = f"{meta.get('namespace')}/{meta.get('name')}"
        state, ok = pod_state(pod)
        if not ok:
            if is_job_pod(pod) or state == "Terminating":
                bucket = "Terminating" if state == "Terminating" else state.split(" ")[0]
                job_counts[bucket] = job_counts.get(bucket, 0) + 1
            else:
                app_issues.append(f"{ref} {state}")
        rc, reason = pod_restarts(pod)
        if rc >= 3:
            restarts.append((rc, meta.get("name", "?"), reason))

    total_warn, warn_list = recent_warnings(hours=1)
    if app_issues or job_counts or restarts or warn_list:
        lines += ["", "🚨 Dikkat Gerekenler"]
        for item in app_issues[:8]:
            lines.append(f"• ⚠️ {item}")
        if job_counts:
            summary = ", ".join(f"{v} {k}" for k, v in sorted(job_counts.items()))
            lines.append(f"• Job/eski podlar: {summary}")
        if restarts:
            top = " · ".join(f"{n} {c}×" for c, n, _ in sorted(restarts, reverse=True)[:4])
            lines.append(f"• En çok restart: {top}")
        if warn_list:
            lines.append(f"• Son 1sa uyarı ({total_warn} event):")
            for tm, cnt, txt in warn_list[:4]:
                prefix = f"[{cnt}×] " if cnt > 1 else ""
                lines.append(f"  {tm} {prefix}{txt}")

    lines += ["", "💡 Detay: cluster info nodes · gamegaraj · argocd · events · cluster help"]
    return chunk_lines(lines)


def cluster_info_nodes() -> list[str]:
    nodes = sorted(kget("/api/v1/nodes").get("items", []), key=node_name)
    pods = kitems("/api/v1/pods")
    metrics = node_metrics()
    stats = pod_stats(pods)

    by_node: dict[str, list] = {}
    unscheduled: list = []
    for pod in pods:
        nname = (pod.get("spec") or {}).get("nodeName")
        if nname:
            by_node.setdefault(nname, []).append(pod)
        else:
            unscheduled.append(pod)

    lines: list[str] = [f"🖥 Node & Pod Dağılımı · {now_tr()}"]
    for node in nodes:
        name = node_name(node)
        lines += ["", node_line_compact(node, metrics.get(name, {}), stats)]
        npods = by_node.get(name, [])
        if not npods:
            lines.append("  • Pod yok")
            continue

        by_ns: dict[str, list[tuple[str, str, bool]]] = {}
        for p in npods:
            ns = (p.get("metadata") or {}).get("namespace", "?")
            pname = (p.get("metadata") or {}).get("name", "?")
            st, ok = pod_state(p)
            by_ns.setdefault(ns, []).append((pname, st, ok))

        for ns in sorted(by_ns):
            entries = by_ns[ns]
            ok_cnt = sum(1 for _, _, ok in entries if ok)
            bad = [(pn, st) for pn, st, ok in entries if not ok]
            if not bad:
                lines.append(f"  • {ns} ({len(entries)}): 🟢{ok_cnt} Running")
            else:
                lines.append(f"  • {ns} ({len(entries)}): 🟢{ok_cnt} ok · ⚠️{len(bad)} sorunlu")
                for pn, st in bad[:5]:
                    lines.append(f"    - {pn} [{st}]")
                if len(bad) > 5:
                    lines.append(f"    ... +{len(bad) - 5}")

    if unscheduled:
        lines += ["", f"⏳ Node atanmamış ({len(unscheduled)} pod)"]
        for p in unscheduled[:8]:
            meta = p.get("metadata") or {}
            st, _ = pod_state(p)
            lines.append(f"  • {meta.get('namespace')}/{meta.get('name')} [{st}]")
        if len(unscheduled) > 8:
            lines.append(f"  ... +{len(unscheduled) - 8}")

    return chunk_lines(lines)


def cluster_info_apps() -> list[str]:
    deps = sorted(kitems(f"/apis/apps/v1/namespaces/{APP_NS}/deployments"), key=lambda d: d["metadata"]["name"])
    pods = kitems(f"/api/v1/namespaces/{APP_NS}/pods")
    lines: list[str] = [f"🎮 {APP_NS} Servisleri · {now_tr()}", ""]
    if not deps:
        lines.append("deployment yok")
        return lines

    for dep in deps:
        name = dep["metadata"]["name"]
        spec = (dep.get("spec") or {}).get("replicas") or 0
        ready = (dep.get("status") or {}).get("readyReplicas") or 0
        icon = "🟢" if ready == spec and spec > 0 else ("⚪" if spec == 0 else "🟡")
        containers = ((dep.get("spec") or {}).get("template") or {}).get("spec", {}).get("containers") or []
        tag = containers[0].get("image", "").rsplit(":", 1)[-1][:22] if containers else ""

        prefix = name + "-"
        nodes_used: dict[str, int] = {}
        bad_states: list[str] = []
        for p in pods:
            pname = (p.get("metadata") or {}).get("name", "")
            if not pname.startswith(prefix):
                continue
            st, ok = pod_state(p)
            if ok:
                nd = (p.get("spec") or {}).get("nodeName") or "?"
                nodes_used[nd] = nodes_used.get(nd, 0) + 1
            else:
                bad_states.append(st)

        where = ", ".join(f"{k}×{v}" for k, v in sorted(nodes_used.items()))
        extra = f" · [{where}]" if where else ""
        if bad_states:
            extra += f" · ⚠️{','.join(bad_states[:2])}"
        lines.append(f"{icon} {name} {ready}/{spec} · {tag}{extra}")
    return chunk_lines(lines)


def cluster_info_argocd() -> list[str]:
    apps = sorted(kitems(f"/apis/argoproj.io/v1alpha1/namespaces/{ARGOCD_NS}/applications"), key=lambda a: a["metadata"]["name"])
    lines: list[str] = [f"🐙 ArgoCD Uygulamaları · {now_tr()}", ""]
    if not apps:
        lines.append("uygulama bulunamadı")
        return lines
    for app in apps:
        st = app.get("status") or {}
        sync = (st.get("sync") or {}).get("status", "?")
        health = (st.get("health") or {}).get("status", "?")
        rev = ((st.get("sync") or {}).get("revision") or "")[:7]
        icon = "🟢" if sync == "Synced" and health == "Healthy" else ("🟡" if health in {"Progressing", "Healthy"} else "🔴")
        lines.append(f"{icon} {app['metadata']['name']} · {sync}/{health}" + (f" · {rev}" if rev else ""))
    return chunk_lines(lines)


def cluster_info_events() -> list[str]:
    total, warns = recent_warnings(hours=1)
    lines: list[str] = [f"📣 Son 1 Saat Warning Events ({total}) · {now_tr()}", ""]
    if not warns:
        lines.append("Warning event yok ✅")
        return lines
    for tm, cnt, txt in warns[:20]:
        prefix = f"[{cnt}×] " if cnt > 1 else ""
        lines.append(f"• {tm} {prefix}{txt}")
    if len(warns) > 20:
        lines.append(f"... +{len(warns) - 20} farklı uyarı")
    return chunk_lines(lines)


def pod_logs_report(query: str) -> list[str]:
    q = query.strip().lower()
    if not q:
        return ["Kullanım: logs <servis-veya-pod>\nÖrn: logs webui"]
    pods = kitems("/api/v1/pods")
    ns_filter = None
    if "/" in q:
        ns_filter, q = q.split("/", 1)

    matches = []
    for p in pods:
        meta = p.get("metadata") or {}
        ns = meta.get("namespace", "")
        name = meta.get("name", "")
        if ns_filter and ns.lower() != ns_filter:
            continue
        if q in name.lower():
            matches.append(p)

    if not matches:
        return [f"'{query}' ile eşleşen pod bulunamadı."]

    # Once Running pod'u tercih et
    matches.sort(key=lambda p: (
        0 if (p.get("metadata") or {}).get("namespace") == APP_NS else 1,
        0 if (p.get("status") or {}).get("phase") == "Running" else 1,
        (p.get("metadata") or {}).get("name", ""),
    ))
    pod = matches[0]
    meta = pod["metadata"]
    ns, name = meta["namespace"], meta["name"]
    try:
        raw = kget_text(f"/api/v1/namespaces/{ns}/pods/{name}/log?tailLines=30")
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")[:300]
        return [f"📄 {ns}/{name} log alınamadı ({exc.code}): {body}"]
    except Exception as exc:
        return [f"📄 {ns}/{name} log alınamadı: {exc}"]

    header = f"📄 {ns}/{name} (son 30 satır)"
    if len(matches) > 1:
        header += f" · toplam {len(matches)} poddan ilki"
    text = raw.strip() or "(log boş)"
    return chunk_lines([header, "", *text.splitlines()])


HELP_TEXT = (
    "🤖 Komutlar\n"
    "• cluster info - DevOps özet raporu\n"
    "• cluster info nodes - node'lara göre pod dağılımı\n"
    f"• cluster info {APP_NS} - servisler, tag ve node'lar\n"
    "• cluster info argocd - ArgoCD uygulamaları\n"
    "• cluster info events - son 1sa Warning event'leri\n"
    "• nodes - sadece node sağlığı\n"
    "• logs <servis> - pod'un son 30 satır logu\n"
    "• cluster help - bu yardım menüsü\n\n"
    "⏰ Otomatik\n"
    "• Her saat başı node raporu\n"
    "• Node çökünce / açılınca anlık bildirim"
)


# --------------------------------------------------------------- telegram
def send_message(chat_id: str, text: str) -> None:
    for chunk in chunk_lines(text.split("\n")):
        for attempt in range(3):
            try:
                http_json(
                    f"{TG_BASE}/sendMessage",
                    data={"chat_id": chat_id, "text": chunk, "disable_web_page_preview": "true"},
                    timeout=30,
                )
                break
            except Exception as exc:
                log(f"send failed (attempt {attempt + 1}): {exc}")
                time.sleep(2 * (attempt + 1))


def send_parts(chat_id: str, parts: list[str]) -> None:
    for part in parts:
        send_message(chat_id, part)


def normalize(text: str) -> str:
    cleaned = re.sub(r"@\w+", "", text or "")
    return " ".join(cleaned.strip().lower().split())


def drain_pending() -> int:
    payload = http_json(f"{TG_BASE}/getUpdates", data={"timeout": "0"}, timeout=20)
    updates = payload.get("result") or []
    if not updates:
        return 0
    return int(updates[-1]["update_id"]) + 1


def handle_command(chat: str, text: str) -> None:
    try:
        if text in CLUSTER_COMMANDS:
            send_parts(chat, cluster_info())
        elif text in CLUSTER_NODES_COMMANDS:
            send_parts(chat, cluster_info_nodes())
        elif text in CLUSTER_APPS_COMMANDS:
            send_parts(chat, cluster_info_apps())
        elif text in CLUSTER_ARGOCD_COMMANDS:
            send_parts(chat, cluster_info_argocd())
        elif text in CLUSTER_EVENTS_COMMANDS:
            send_parts(chat, cluster_info_events())
        elif text in NODE_COMMANDS:
            send_message(chat, node_report("🖥 Node Durumu"))
        elif text in HELP_COMMANDS:
            send_message(chat, HELP_TEXT)
        elif text.startswith("logs ") or text.startswith("/logs "):
            query = text.split(" ", 1)[1]
            send_parts(chat, pod_logs_report(query))
    except Exception as exc:
        log(f"command '{text}' failed: {exc}")
        send_message(chat, f"bilgi alınamadı: {exc}")


# ------------------------------------------------------------ node watcher
class NodeWatcher:
    """Node Ready durumunu izler; degisimleri DEBOUNCE_CHECKS kez dogrulayip bildirir."""

    def __init__(self) -> None:
        self.state: dict[str, dict] = {}
        self.pending: dict[str, int] = {}
        self.initialized = False

    def _pods_on(self, name: str) -> int:
        items = kitems(f"/api/v1/pods?fieldSelector=spec.nodeName%3D{urllib.parse.quote(name)}")
        return sum(1 for p in items if (p.get("status") or {}).get("phase") not in {"Succeeded", "Failed"})

    def check(self) -> None:
        nodes = kget("/api/v1/nodes").get("items", [])
        now = time.time()
        current = {node_name(n): n for n in nodes}

        if not self.initialized:
            for name, node in current.items():
                since = parse_ts(node_condition(node, "Ready").get("lastTransitionTime"))
                self.state[name] = {"ready": node_ready(node), "since": since.timestamp() if since else now}
            self.initialized = True
            log("watcher initialized: " + ", ".join(f"{n}={'Ready' if s['ready'] else 'NotReady'}" for n, s in self.state.items()))
            return

        for name, node in current.items():
            ready = node_ready(node)
            known = self.state.get(name)
            if known is None:
                self.state[name] = {"ready": ready, "since": now}
                send_message(CHAT_ID, f"➕ Yeni node katıldı: {name} ({node_roles(node)})\nDurum: {'Ready 🟢' if ready else 'NotReady 🔴'}\n🕐 {now_tr()}")
                continue
            if ready == known["ready"]:
                self.pending.pop(name, None)
                continue
            count = self.pending.get(name, 0) + 1
            if count < DEBOUNCE_CHECKS:
                self.pending[name] = count
                continue
            self.pending.pop(name, None)
            duration = fmt_duration(now - known["since"])
            known.update(ready=ready, since=now)
            if ready:
                text = (
                    f"🟢 {name} AÇILDI (Ready)\n"
                    f"Rol: {node_roles(node)}\n"
                    f"⏱ Kesinti süresi: {duration}\n"
                    f"🕐 {now_tr()}"
                )
            else:
                text = (
                    f"🔴 {name} ÇÖKTÜ (NotReady)\n"
                    f"Rol: {node_roles(node)}\n"
                    f"Sebep: {node_not_ready_reason(node)}\n"
                    f"Etkilenen pod: {self._pods_on(name)}\n"
                    f"⏱ Çalışma süresi: {duration}\n"
                    f"🕐 {now_tr()}"
                )
            log(text.replace("\n", " | "))
            send_message(CHAT_ID, text)

        for name in list(self.state):
            if name not in current:
                del self.state[name]
                self.pending.pop(name, None)
                send_message(CHAT_ID, f"➖ {name} cluster'dan çıkarıldı\n🕐 {now_tr()}")


def watcher_loop() -> None:
    watcher = NodeWatcher()
    last_hour = int(time.time() // 3600)
    while True:
        try:
            watcher.check()
        except Exception as exc:
            log(f"node check failed: {exc}")

        current_hour = int(time.time() // 3600)
        if HOURLY_REPORT and current_hour != last_hour:
            last_hour = current_hour
            try:
                send_message(CHAT_ID, node_report("🕐 Saatlik Node Raporu"))
            except Exception as exc:
                log(f"hourly report failed: {exc}")

        to_next_hour = 3600 - (time.time() % 3600) + 1
        time.sleep(max(1, min(CHECK_INTERVAL, to_next_hour)))


# -------------------------------------------------------------------- main
def main() -> None:
    if not CHAT_ID:
        raise SystemExit("TELEGRAM_CHAT_ID is empty")
    offset = 0
    try:
        offset = drain_pending()
    except Exception as exc:
        log(f"drain failed: {exc}")

    threading.Thread(target=watcher_loop, name="node-watcher", daemon=True).start()

    log(f"listening chat_id={CHAT_ID} offset={offset} hourly={HOURLY_REPORT} interval={CHECK_INTERVAL}s")
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
                handle_command(chat, normalize(message.get("text") or ""))
        except Exception as exc:
            log(f"poll error: {exc}")
            time.sleep(5)


if __name__ == "__main__":
    main()
