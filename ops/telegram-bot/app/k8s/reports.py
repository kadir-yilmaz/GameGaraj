from __future__ import annotations

import urllib.error
from datetime import datetime, timedelta, timezone

from app.k8s.client import K8sClient

MAX_MSG = 3900
PRESSURE_CONDITIONS = ("MemoryPressure", "DiskPressure", "PIDPressure", "NetworkUnavailable")
_BIN = {"Ki": 1024, "Mi": 1024**2, "Gi": 1024**3, "Ti": 1024**4, "Pi": 1024**5, "Ei": 1024**6}
_DEC = {"k": 1e3, "K": 1e3, "M": 1e6, "G": 1e9, "T": 1e12, "P": 1e15, "E": 1e18}


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
    return "?" if total <= 0 else f"%{part / total * 100:.0f}"


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


def is_job_pod(pod: dict) -> bool:
    for owner in (pod.get("metadata") or {}).get("ownerReferences", []) or []:
        if owner.get("kind") in {"Job", "CronJob"}:
            return True
    return False


def pod_state(pod: dict) -> tuple[str, bool]:
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


class ClusterReporter:
    """Generates human-readable Kubernetes cluster reports for Telegram and Web UI."""

    def __init__(self, k8s: K8sClient, app_ns: str, argocd_ns: str, tz: timezone) -> None:
        self.k8s = k8s
        self.app_ns = app_ns
        self.argocd_ns = argocd_ns
        self.tz = tz

    def now_tr(self) -> str:
        return datetime.now(self.tz).strftime("%d.%m.%Y %H:%M")

    def node_metrics(self) -> dict:
        try:
            items = self.k8s.get_json("/apis/metrics.k8s.io/v1beta1/nodes").get("items", [])
        except Exception:
            return {}
        return {m.get("metadata", {}).get("name"): m.get("usage", {}) for m in items}

    def pod_stats(self, pods: list) -> dict:
        stats: dict[str, dict] = {}
        for pod in pods:
            if (pod.get("metadata") or {}).get("deletionTimestamp"):
                continue
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

    def cluster_snapshot(self) -> dict:
        """Structured summary for the Web UI dashboard."""
        if not self.k8s.available:
            return {"available": False, "nodes": [], "running_pods": 0, "total_pods": 0}
        nodes = sorted(self.k8s.items("/api/v1/nodes"), key=node_name)
        pods = self.k8s.items("/api/v1/pods")
        metrics = self.node_metrics()
        stats = self.pod_stats(pods)
        node_list = []
        for n in nodes:
            name = node_name(n)
            alloc = (n.get("status") or {}).get("allocatable") or {}
            cpu_alloc = parse_cpu(alloc.get("cpu"))
            mem_alloc = parse_mem(alloc.get("memory"))
            usage = metrics.get(name, {})
            st = stats.get(name, {"pods": 0})
            node_list.append(
                {
                    "name": name,
                    "role": node_roles(n),
                    "ready": node_ready(n),
                    "pods": st["pods"],
                    "cpu_pct": round(parse_cpu(usage.get("cpu")) / cpu_alloc * 100, 1) if usage and cpu_alloc else None,
                    "mem_pct": round(parse_mem(usage.get("memory")) / mem_alloc * 100, 1) if usage and mem_alloc else None,
                }
            )
        running = sum(
            1
            for p in pods
            if (p.get("status") or {}).get("phase") == "Running"
            and not (p.get("metadata") or {}).get("deletionTimestamp")
        )
        return {
            "available": True,
            "nodes": node_list,
            "ready_nodes": sum(1 for n in node_list if n["ready"]),
            "total_nodes": len(node_list),
            "running_pods": running,
            "total_pods": len(pods),
        }

    def _node_line_compact(self, node: dict, usage: dict, stats: dict) -> str:
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

    def _node_block(self, node: dict, usage: dict, stats: dict) -> list[str]:
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

    def node_report(self, title: str) -> str:
        nodes = sorted(self.k8s.get_json("/api/v1/nodes").get("items", []), key=node_name)
        metrics = self.node_metrics()
        stats = self.pod_stats(self.k8s.items("/api/v1/pods"))
        ready_count = sum(1 for n in nodes if node_ready(n))
        head = "✅" if ready_count == len(nodes) else "⚠️"
        lines = [f"{title} · {self.now_tr()}", f"{head} {ready_count}/{len(nodes)} node Ready", ""]
        for node in nodes:
            lines.extend(self._node_block(node, metrics.get(node_name(node), {}), stats))
        return "\n".join(lines)

    def _recent_warnings(self, hours: int = 1) -> tuple[int, list[tuple[str, int, str]]]:
        events = self.k8s.items("/api/v1/events?fieldSelector=type%3DWarning")
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
        return total, [(x["ts"].astimezone(self.tz).strftime("%H:%M"), x["count"], x["line"]) for x in items]

    def _workload_counts(self, kind: str, items: list, spec_key: str, ready_key: str) -> tuple[int, int, list[str]]:
        healthy, bad = 0, []
        for it in items:
            meta, status = it.get("metadata") or {}, it.get("status") or {}
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

    def cluster_info(self) -> list[str]:
        try:
            ver = self.k8s.get_json("/version").get("gitVersion", "?")
        except Exception:
            ver = "?"
        nodes = sorted(self.k8s.items("/api/v1/nodes"), key=node_name)
        pods = self.k8s.items("/api/v1/pods")
        metrics = self.node_metrics()
        stats = self.pod_stats(pods)
        ready_nodes = sum(1 for n in nodes if node_ready(n))
        running = sum(1 for p in pods if (p.get("status") or {}).get("phase") == "Running" and not (p.get("metadata") or {}).get("deletionTimestamp"))
        pvcs = self.k8s.items("/api/v1/persistentvolumeclaims")
        bound_pvcs = sum(1 for p in pvcs if (p.get("status") or {}).get("phase") == "Bound")
        head_icon = "✅" if ready_nodes == len(nodes) else "⚠️"
        lines: list[str] = [
            f"📊 Cluster Info · {self.now_tr()} · {ver}",
            f"{head_icon} {ready_nodes}/{len(nodes)} Node Ready · {running} Running pod · {bound_pvcs}/{len(pvcs)} PVC",
            "",
            "🖥 Nodes",
        ]
        for node in nodes:
            lines.append(self._node_line_compact(node, metrics.get(node_name(node), {}), stats))

        d_ok, d_all, d_bad = self._workload_counts("Deploy", self.k8s.items("/apis/apps/v1/deployments"), "replicas", "readyReplicas")
        s_ok, s_all, s_bad = self._workload_counts("STS", self.k8s.items("/apis/apps/v1/statefulsets"), "replicas", "readyReplicas")
        ds_ok, ds_all, ds_bad = self._workload_counts("DS", self.k8s.items("/apis/apps/v1/daemonsets"), "", "numberReady")
        lines += ["", "⚙️ Workloads & Servisler", f"• K8s: Deploy {d_ok}/{d_all} · STS {s_ok}/{s_all} · DS {ds_ok}/{ds_all}"]
        lines.extend(f"  {b}" for b in (d_bad + s_bad + ds_bad)[:8])

        deps = sorted(self.k8s.items(f"/apis/apps/v1/namespaces/{self.app_ns}/deployments"), key=lambda d: d["metadata"]["name"])
        if deps:
            ok_deps = [d for d in deps if ((d.get("status") or {}).get("readyReplicas") or 0) >= ((d.get("spec") or {}).get("replicas") or 0) > 0]
            if len(ok_deps) == len(deps):
                lines.append(f"• {self.app_ns}: 🟢 {len(deps)}/{len(deps)} servis hazır")
            else:
                lines.append(f"• {self.app_ns}: 🟡 {len(ok_deps)}/{len(deps)} servis hazır")
                for d in deps:
                    spec = (d.get("spec") or {}).get("replicas") or 0
                    ready = (d.get("status") or {}).get("readyReplicas") or 0
                    if ready < spec:
                        lines.append(f"  🟡 {d['metadata']['name']} {ready}/{spec}")

        apps = sorted(self.k8s.items(f"/apis/argoproj.io/v1alpha1/namespaces/{self.argocd_ns}/applications"), key=lambda a: a["metadata"]["name"])
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

        app_issues, job_counts, restarts = [], {}, []
        for pod in pods:
            meta = pod.get("metadata") or {}
            state, ok = pod_state(pod)
            if state == "Terminating":
                continue
            if not ok:
                if is_job_pod(pod):
                    bucket = state.split(" ")[0]
                    job_counts[bucket] = job_counts.get(bucket, 0) + 1
                else:
                    app_issues.append(f"{meta.get('namespace')}/{meta.get('name')} {state}")
            rc, reason = pod_restarts(pod)
            if rc >= 3:
                restarts.append((rc, meta.get("name", "?"), reason))
        total_warn, warn_list = self._recent_warnings(hours=1)
        if app_issues or job_counts or restarts or warn_list:
            lines += ["", "🚨 Dikkat Gerekenler"]
            lines.extend(f"• ⚠️ {item}" for item in app_issues[:8])
            if job_counts:
                lines.append("• Job/eski podlar: " + ", ".join(f"{v} {k}" for k, v in sorted(job_counts.items())))
            if restarts:
                lines.append("• En çok restart: " + " · ".join(f"{n} {c}×" for c, n, _ in sorted(restarts, reverse=True)[:4]))
            if warn_list:
                lines.append(f"• Son 1sa uyarı ({total_warn} event):")
                for tm, cnt, txt in warn_list[:4]:
                    lines.append(f"  {tm} {'[' + str(cnt) + '×] ' if cnt > 1 else ''}{txt}")
        lines += ["", "💡 Detay: cluster info nodes · gamegaraj · argocd · events · cluster help"]
        return chunk_lines(lines)

    def cluster_info_nodes(self) -> list[str]:
        nodes = sorted(self.k8s.items("/api/v1/nodes"), key=node_name)
        pods = self.k8s.items("/api/v1/pods")
        metrics = self.node_metrics()
        stats = self.pod_stats(pods)
        by_node: dict[str, list] = {}
        unscheduled: list = []
        for pod in pods:
            nname = (pod.get("spec") or {}).get("nodeName")
            (by_node.setdefault(nname, []) if nname else unscheduled).append(pod)
        lines: list[str] = [f"🖥 Node & Pod Dağılımı · {self.now_tr()}"]
        for node in nodes:
            name = node_name(node)
            lines += ["", self._node_line_compact(node, metrics.get(name, {}), stats)]
            npods = by_node.get(name, [])
            if not npods:
                lines.append("  • Pod yok")
                continue
            by_ns: dict[str, list[tuple[str, str, bool]]] = {}
            for p in npods:
                st, ok = pod_state(p)
                if st == "Terminating":
                    continue
                by_ns.setdefault((p.get("metadata") or {}).get("namespace", "?"), []).append(((p.get("metadata") or {}).get("name", "?"), st, ok))
            for ns in sorted(by_ns):
                entries = by_ns[ns]
                ok_cnt = sum(1 for _, _, ok in entries if ok)
                bad = [(pn, st) for pn, st, ok in entries if not ok]
                if not bad:
                    lines.append(f"  • {ns} ({len(entries)}): 🟢{ok_cnt} Running")
                else:
                    lines.append(f"  • {ns} ({len(entries)}): 🟢{ok_cnt} ok · ⚠️{len(bad)} sorunlu")
                    lines.extend(f"    - {pn} [{st}]" for pn, st in bad[:5])
        if unscheduled:
            lines += ["", f"⏳ Node atanmamış ({len(unscheduled)} pod)"]
            for p in unscheduled[:8]:
                meta = p.get("metadata") or {}
                lines.append(f"  • {meta.get('namespace')}/{meta.get('name')} [{pod_state(p)[0]}]")
        return chunk_lines(lines)

    def cluster_info_apps(self) -> list[str]:
        deps = sorted(self.k8s.items(f"/apis/apps/v1/namespaces/{self.app_ns}/deployments"), key=lambda d: d["metadata"]["name"])
        pods = self.k8s.items(f"/api/v1/namespaces/{self.app_ns}/pods")
        lines: list[str] = [f"🎮 {self.app_ns} Servisleri · {self.now_tr()}", ""]
        if not deps:
            return lines + ["deployment yok"]
        for dep in deps:
            name = dep["metadata"]["name"]
            spec = (dep.get("spec") or {}).get("replicas") or 0
            ready = (dep.get("status") or {}).get("readyReplicas") or 0
            icon = "🟢" if ready == spec and spec > 0 else ("⚪" if spec == 0 else "🟡")
            containers = ((dep.get("spec") or {}).get("template") or {}).get("spec", {}).get("containers") or []
            tag = containers[0].get("image", "").rsplit(":", 1)[-1][:22] if containers else ""
            prefix = name + "-"
            nodes_used, bad_states = {}, []
            for p in pods:
                if not (p.get("metadata") or {}).get("name", "").startswith(prefix):
                    continue
                st, ok = pod_state(p)
                if st == "Terminating":
                    continue
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

    def cluster_info_argocd(self) -> list[str]:
        apps = sorted(self.k8s.items(f"/apis/argoproj.io/v1alpha1/namespaces/{self.argocd_ns}/applications"), key=lambda a: a["metadata"]["name"])
        lines: list[str] = [f"🐙 ArgoCD Uygulamaları · {self.now_tr()}", ""]
        if not apps:
            return lines + ["uygulama bulunamadı"]
        for app in apps:
            st = app.get("status") or {}
            sync = (st.get("sync") or {}).get("status", "?")
            health = (st.get("health") or {}).get("status", "?")
            rev = ((st.get("sync") or {}).get("revision") or "")[:7]
            icon = "🟢" if sync == "Synced" and health == "Healthy" else ("🟡" if health in {"Progressing", "Healthy"} else "🔴")
            lines.append(f"{icon} {app['metadata']['name']} · {sync}/{health}" + (f" · {rev}" if rev else ""))
        return chunk_lines(lines)

    def cluster_info_events(self) -> list[str]:
        total, warns = self._recent_warnings(hours=1)
        lines: list[str] = [f"📣 Son 1 Saat Warning Events ({total}) · {self.now_tr()}", ""]
        if not warns:
            return lines + ["Warning event yok ✅"]
        for tm, cnt, txt in warns[:20]:
            lines.append(f"• {tm} {'[' + str(cnt) + '×] ' if cnt > 1 else ''}{txt}")
        return chunk_lines(lines)

    def pod_logs_report(self, query: str) -> list[str]:
        q = query.strip().lower()
        if not q:
            return ["Kullanım: logs <servis-veya-pod>\nÖrn: logs webui"]
        ns_filter = None
        if "/" in q:
            ns_filter, q = q.split("/", 1)
        matches = [
            p
            for p in self.k8s.items("/api/v1/pods")
            if (not ns_filter or (p.get("metadata") or {}).get("namespace", "").lower() == ns_filter)
            and q in (p.get("metadata") or {}).get("name", "").lower()
        ]
        if not matches:
            return [f"'{query}' ile eşleşen pod bulunamadı."]
        matches.sort(
            key=lambda p: (
                0 if (p.get("metadata") or {}).get("namespace") == self.app_ns else 1,
                0 if (p.get("status") or {}).get("phase") == "Running" else 1,
                (p.get("metadata") or {}).get("name", ""),
            )
        )
        meta = matches[0]["metadata"]
        ns, name = meta["namespace"], meta["name"]
        try:
            raw = self.k8s.get_text(f"/api/v1/namespaces/{ns}/pods/{name}/log?tailLines=30")
        except urllib.error.HTTPError as exc:
            return [f"📄 {ns}/{name} log alınamadı ({exc.code})"]
        except Exception as exc:
            return [f"📄 {ns}/{name} log alınamadı: {exc}"]
        header = f"📄 {ns}/{name} (son 30 satır)" + (f" · toplam {len(matches)} poddan ilki" if len(matches) > 1 else "")
        return chunk_lines([header, "", *(raw.strip() or "(log boş)").splitlines()])
