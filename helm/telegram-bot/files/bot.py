#!/usr/bin/env python3
"""GameGaraj Telegram cluster bot.

- 'cluster info'  -> DevOps odakli detayli cluster raporu
- 'nodes'         -> sadece node saglik raporu
- Her saat basi   -> node saglik raporu (otomatik)
- Node NotReady / Ready gecislerinde aninda bildirim (cokme / acilma)
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
NODE_COMMANDS = {"nodes", "node", "node info", "/nodes", "/node"}
HELP_COMMANDS = {"/help", "help", "/start", "yardım", "yardim"}

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


def sa_token() -> str:
    # Projected SA token periyodik yenilenir; her istekte dosyadan oku.
    with open(f"{SA_DIR}/token", encoding="utf-8") as fh:
        return fh.read().strip()


def kget(path: str) -> dict:
    return http_json(
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
    """CPU quantity -> core."""
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
    """Memory quantity -> byte."""
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


def node_ip(node: dict) -> str:
    for addr in node.get("status", {}).get("addresses", []) or []:
        if addr.get("type") == "InternalIP":
            return addr.get("address", "?")
    return "?"


def node_pressures(node: dict) -> list[str]:
    return [c for c in PRESSURE_CONDITIONS if node_condition(node, c).get("status") == "True"]


def node_not_ready_reason(node: dict) -> str:
    cond = node_condition(node, "Ready")
    reason = cond.get("reason") or "Unknown"
    message = (cond.get("message") or "").strip()
    return f"{reason} - {message[:120]}" if message else reason


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


def node_block(node: dict, usage: dict, stats: dict, detailed: bool) -> list[str]:
    name = node_name(node)
    ready = node_ready(node)
    cordoned = bool((node.get("spec") or {}).get("unschedulable"))
    alloc = (node.get("status") or {}).get("allocatable") or {}
    cpu_alloc = parse_cpu(alloc.get("cpu"))
    mem_alloc = parse_mem(alloc.get("memory"))
    pod_alloc = int(alloc.get("pods") or 0)
    ready_since = age(node_condition(node, "Ready").get("lastTransitionTime"))

    if not ready:
        icon, state = "🔴", "NotReady"
    elif cordoned:
        icon, state = "🟠", "Ready,SchedulingDisabled"
    else:
        icon, state = "🟢", "Ready"

    lines = [f"{icon} {name} ({node_roles(node)})"]
    if not ready:
        lines.append(f"   ❌ {state} · {ready_since}'dır")
        lines.append(f"   Sebep: {node_not_ready_reason(node)}")
        return lines

    st = stats.get(name, {"pods": 0, "cpu_req": 0.0, "mem_req": 0.0})
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
    if detailed:
        info = (node.get("status") or {}).get("nodeInfo") or {}
        lines.append(
            f"   Request: CPU {pct(st['cpu_req'], cpu_alloc)} · RAM {pct(st['mem_req'], mem_alloc)}"
        )
        lines.append(f"   IP {node_ip(node)} · {info.get('kubeletVersion', '?')}")
        lines.append(f"   {info.get('osImage', '?')} · kernel {info.get('kernelVersion', '?')}")
    pressures = node_pressures(node)
    if pressures:
        lines.append(f"   ⚠️ {', '.join(pressures)}")
    return lines


# ---------------------------------------------------------------- reports
def node_report(title: str) -> str:
    nodes = sorted(kget("/api/v1/nodes").get("items", []), key=node_name)
    metrics = node_metrics()
    stats = pod_stats(kitems("/api/v1/pods"))
    ready_count = sum(1 for n in nodes if node_ready(n))
    head = "✅" if ready_count == len(nodes) else "⚠️"

    lines = [f"{title} · {now_tr()}", f"{head} {ready_count}/{len(nodes)} node Ready", ""]
    for node in nodes:
        lines.extend(node_block(node, metrics.get(node_name(node), {}), stats, detailed=False))
    return "\n".join(lines)


def pod_problem(pod: dict) -> str | None:
    status = pod.get("status") or {}
    phase = status.get("phase", "")
    if phase in {"Succeeded", "Completed"}:
        return None
    meta = pod.get("metadata") or {}
    ref = f"{meta.get('namespace', '?')}/{meta.get('name', '?')}"
    if phase in {"Pending", "Failed", "Unknown"}:
        reason = status.get("reason")
        for cs in status.get("containerStatuses", []) or []:
            waiting = (cs.get("state") or {}).get("waiting") or {}
            if waiting.get("reason"):
                reason = waiting["reason"]
        return f"{ref} {phase}" + (f" ({reason})" if reason else "")
    for cs in status.get("containerStatuses", []) or []:
        waiting = (cs.get("state") or {}).get("waiting") or {}
        reason = waiting.get("reason")
        if reason in {"PodInitializing", "ContainerCreating"}:
            continue
        if reason:
            return f"{ref} {reason}"
    return None


def workload_lines(kind: str, items: list, spec_key: str, ready_key: str) -> tuple[int, int, list[str]]:
    total = len(items)
    healthy = 0
    bad = []
    for it in items:
        meta = it.get("metadata") or {}
        status = it.get("status") or {}
        if kind == "DaemonSet":
            want = status.get("desiredNumberScheduled") or 0
        else:
            want = (it.get("spec") or {}).get(spec_key)
            want = 1 if want is None else want
        have = status.get(ready_key) or 0
        if have >= want:
            healthy += 1
        else:
            bad.append(f"🟡 {kind} {meta.get('namespace')}/{meta.get('name')} {have}/{want}")
    return healthy, total, bad


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


def cluster_info() -> list[str]:
    lines: list[str] = [f"📊 Cluster Info · {now_tr()}"]
    try:
        ver = kget("/version")
        lines.append(f"Kubernetes {ver.get('gitVersion', '?')}")
    except Exception:
        pass

    # ---- Nodes
    nodes = sorted(kget("/api/v1/nodes").get("items", []), key=node_name)
    pods = kitems("/api/v1/pods")
    metrics = node_metrics()
    stats = pod_stats(pods)
    ready_count = sum(1 for n in nodes if node_ready(n))
    lines += ["", f"🖥 Nodes ({ready_count}/{len(nodes)} Ready)"]
    cpu_alloc = mem_alloc = cpu_used = mem_used = 0.0
    for node in nodes:
        usage = metrics.get(node_name(node), {})
        lines.extend(node_block(node, usage, stats, detailed=True))
        if node_ready(node):
            alloc = (node.get("status") or {}).get("allocatable") or {}
            cpu_alloc += parse_cpu(alloc.get("cpu"))
            mem_alloc += parse_mem(alloc.get("memory"))
            cpu_used += parse_cpu(usage.get("cpu"))
            mem_used += parse_mem(usage.get("memory"))
    if metrics:
        lines.append(
            f"Σ CPU {pct(cpu_used, cpu_alloc)} ({cpu_used:.1f}/{cpu_alloc:.0f} core)"
            f" · RAM {pct(mem_used, mem_alloc)} ({gib(mem_used)}/{gib(mem_alloc)} GiB)"
        )

    # ---- Pods
    phases: dict[str, int] = {}
    for pod in pods:
        ph = (pod.get("status") or {}).get("phase", "Unknown")
        phases[ph] = phases.get(ph, 0) + 1
    ns_count = len(kitems("/api/v1/namespaces"))
    lines += ["", f"📦 Pods ({len(pods)}) · {ns_count} namespace"]
    lines.append(" · ".join(f"{k} {v}" for k, v in sorted(phases.items())) or "pod yok")

    # ---- Workloads
    d_ok, d_all, d_bad = workload_lines("Deploy", kitems("/apis/apps/v1/deployments"), "replicas", "readyReplicas")
    s_ok, s_all, s_bad = workload_lines("STS", kitems("/apis/apps/v1/statefulsets"), "replicas", "readyReplicas")
    ds_ok, ds_all, ds_bad = workload_lines("DS", kitems("/apis/apps/v1/daemonsets"), "", "numberReady")
    lines += ["", "⚙️ Workloads"]
    lines.append(f"Deployment {d_ok}/{d_all} · StatefulSet {s_ok}/{s_all} · DaemonSet {ds_ok}/{ds_all}")
    lines.extend((d_bad + s_bad + ds_bad)[:15])

    # ---- GameGaraj namespace
    deps = sorted(kitems(f"/apis/apps/v1/namespaces/{APP_NS}/deployments"), key=lambda d: d["metadata"]["name"])
    lines += ["", f"🎮 {APP_NS}"]
    if not deps:
        lines.append("deployment yok")
    for dep in deps:
        spec = (dep.get("spec") or {}).get("replicas") or 0
        ready = (dep.get("status") or {}).get("readyReplicas") or 0
        icon = "🟢" if ready == spec and spec > 0 else ("⚪" if spec == 0 else "🟡")
        image = ""
        containers = ((dep.get("spec") or {}).get("template") or {}).get("spec", {}).get("containers") or []
        if containers:
            image = containers[0].get("image", "").rsplit(":", 1)[-1][:20]
        lines.append(f"{icon} {dep['metadata']['name']} {ready}/{spec}" + (f" · {image}" if image else ""))

    # ---- ArgoCD
    apps = kitems(f"/apis/argoproj.io/v1alpha1/namespaces/{ARGOCD_NS}/applications")
    if apps:
        lines += ["", "🐙 ArgoCD"]
        for app in sorted(apps, key=lambda a: a["metadata"]["name"]):
            st = app.get("status") or {}
            sync = (st.get("sync") or {}).get("status", "?")
            health = (st.get("health") or {}).get("status", "?")
            rev = ((st.get("sync") or {}).get("revision") or "")[:7]
            icon = "🟢" if sync == "Synced" and health == "Healthy" else ("🟡" if health in {"Progressing", "Healthy"} else "🔴")
            lines.append(f"{icon} {app['metadata']['name']} {sync}/{health}" + (f" · {rev}" if rev else ""))

    # ---- PVC
    pvcs = kitems("/api/v1/persistentvolumeclaims")
    if pvcs:
        bound = [p for p in pvcs if (p.get("status") or {}).get("phase") == "Bound"]
        lines += ["", f"💾 PVC {len(bound)}/{len(pvcs)} Bound"]
        for p in pvcs:
            phase = (p.get("status") or {}).get("phase")
            if phase != "Bound":
                lines.append(f"🟡 {p['metadata']['namespace']}/{p['metadata']['name']} {phase}")

    # ---- Restarts
    restarts = []
    for pod in pods:
        total = 0
        last_reason = ""
        for cs in (pod.get("status") or {}).get("containerStatuses", []) or []:
            total += cs.get("restartCount", 0)
            term = (cs.get("lastState") or {}).get("terminated") or {}
            if term.get("reason"):
                last_reason = term["reason"]
        if total > 0:
            meta = pod["metadata"]
            restarts.append((total, f"{meta['namespace']}/{meta['name']}", last_reason))
    if restarts:
        lines += ["", "🔁 En çok restart"]
        for total, ref, reason in sorted(restarts, reverse=True)[:5]:
            lines.append(f"{total}× {ref}" + (f" ({reason})" if reason else ""))

    # ---- Problems
    problems = [p for p in (pod_problem(pod) for pod in pods) if p]
    lines += ["", "🚨 Sorunlu podlar"]
    if problems:
        lines.extend(f"⚠️ {p}" for p in problems[:15])
        if len(problems) > 15:
            lines.append(f"... +{len(problems) - 15}")
    else:
        lines.append("yok ✅")

    # ---- Warning events (son 1 saat)
    events = kitems("/api/v1/events?fieldSelector=type%3DWarning")
    cutoff = datetime.now(timezone.utc) - timedelta(hours=1)
    recent = []
    for ev in events:
        ts = parse_ts(ev.get("lastTimestamp") or ev.get("eventTime") or (ev.get("metadata") or {}).get("creationTimestamp"))
        if ts and ts >= cutoff:
            obj = ev.get("involvedObject") or {}
            msg = (ev.get("message") or "").replace("\n", " ")[:90]
            recent.append((ts, f"{obj.get('kind', '?')} {obj.get('namespace') or '-'}/{obj.get('name', '?')} {ev.get('reason', '')}: {msg}"))
    lines += ["", f"📣 Warning event (son 1sa): {len(recent)}"]
    for ts, text in sorted(recent, reverse=True)[:6]:
        lines.append(f"{ts.astimezone(TZ):%H:%M} {text}")

    return chunk_lines(lines)


HELP_TEXT = (
    "Komutlar\n"
    "cluster info - DevOps cluster raporu\n"
    "nodes - node sağlık durumu\n\n"
    "Otomatik\n"
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
            for part in cluster_info():
                send_message(chat, part)
        elif text in NODE_COMMANDS:
            send_message(chat, node_report("🖥 Node Durumu"))
        elif text in HELP_COMMANDS:
            send_message(chat, HELP_TEXT)
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
