from __future__ import annotations

import logging
import re
import threading
import time
import urllib.parse

from app.config import Settings
from app.db.repositories import IncidentRepository, SettingsRepository
from app.k8s.client import K8sClient
from app.k8s.reports import (
    ClusterReporter,
    fmt_duration,
    node_condition,
    node_name,
    node_not_ready_reason,
    node_ready,
    node_roles,
    parse_ts,
)
from app.notifiers.telegram import TelegramNotifier
from app.services.oncall import OnCallService

log = logging.getLogger(__name__)

CLUSTER_COMMANDS = {"cluster info", "clusterinfo", "/clusterinfo", "/cluster", "/info", "info"}
CLUSTER_NODES_COMMANDS = {"cluster info nodes", "cluster nodes", "nodes detail", "/clusternodes"}
CLUSTER_APPS_COMMANDS = {"cluster info gamegaraj", "cluster info apps", "cluster gamegaraj", "gamegaraj", "apps"}
CLUSTER_ARGOCD_COMMANDS = {"cluster info argocd", "cluster argocd", "argocd"}
CLUSTER_EVENTS_COMMANDS = {"cluster info events", "cluster events", "events", "warnings"}
NODE_COMMANDS = {"nodes", "node", "node info", "/nodes", "/node"}
HELP_COMMANDS = {"cluster help", "cluster info help", "/help", "help", "/start", "yardım", "yardim"}


def normalize_cmd(text: str) -> str:
    cleaned = re.sub(r"@\w+", "", text or "")
    return " ".join(cleaned.strip().lower().split())


class NodeWatcher:
    """Watches Kubernetes node Ready transitions with debounce."""

    def __init__(self, k8s: K8sClient, reporter: ClusterReporter, oncall: OnCallService, debounce_checks: int) -> None:
        self.k8s = k8s
        self.reporter = reporter
        self.oncall = oncall
        self.debounce_checks = debounce_checks
        self.state: dict[str, dict] = {}
        self.pending: dict[str, int] = {}
        self.initialized = False

    def _pods_on(self, name: str) -> int:
        items = self.k8s.items(f"/api/v1/pods?fieldSelector=spec.nodeName%3D{urllib.parse.quote(name)}")
        return sum(1 for p in items if (p.get("status") or {}).get("phase") not in {"Succeeded", "Failed"})

    def check(self) -> None:
        if not self.k8s.available:
            return
        nodes = self.k8s.get_json("/api/v1/nodes").get("items", [])
        now = time.time()
        current = {node_name(n): n for n in nodes}

        if not self.initialized:
            for name, node in current.items():
                since = parse_ts(node_condition(node, "Ready").get("lastTransitionTime"))
                self.state[name] = {"ready": node_ready(node), "since": since.timestamp() if since else now}
            self.initialized = True
            log.info("watcher initialized: %s", ", ".join(f"{n}={'Ready' if s['ready'] else 'NotReady'}" for n, s in self.state.items()))
            return

        for name, node in current.items():
            ready = node_ready(node)
            known = self.state.get(name)
            if known is None:
                self.state[name] = {"ready": ready, "since": now}
                self.oncall.dispatch_general_report(
                    f"➕ Yeni node katıldı: {name} ({node_roles(node)})\nDurum: {'Ready 🟢' if ready else 'NotReady 🔴'}\n🕐 {self.reporter.now_tr()}"
                )
                continue
            if ready == known["ready"]:
                self.pending.pop(name, None)
                continue
            count = self.pending.get(name, 0) + 1
            if count < self.debounce_checks:
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
                    f"🕐 {self.reporter.now_tr()}"
                )
                self.oncall.resolve_node_incident(name, text)
            else:
                text = (
                    f"🔴 {name} ÇÖKTÜ (NotReady)\n"
                    f"Rol: {node_roles(node)}\n"
                    f"Sebep: {node_not_ready_reason(node)}\n"
                    f"Etkilenen pod: {self._pods_on(name)}\n"
                    f"⏱ Çalışma süresi: {duration}\n"
                    f"🕐 {self.reporter.now_tr()}"
                )
                self.oncall.dispatch_critical_alert(name, f"{name} NotReady", text)

        for name in list(self.state):
            if name not in current:
                del self.state[name]
                self.pending.pop(name, None)
                self.oncall.dispatch_general_report(f"➖ {name} cluster'dan çıkarıldı\n🕐 {self.reporter.now_tr()}")


class BotRunner:
    """Runs background node watcher, dynamic report scheduler, and Telegram polling."""

    def __init__(
        self,
        settings: Settings,
        k8s: K8sClient,
        reporter: ClusterReporter,
        app_settings: SettingsRepository,
        incidents: IncidentRepository,
        oncall: OnCallService,
        telegram: TelegramNotifier,
    ) -> None:
        self.settings = settings
        self.k8s = k8s
        self.reporter = reporter
        self.app_settings = app_settings
        self.incidents = incidents
        self.oncall = oncall
        self.telegram = telegram
        self.watcher = NodeWatcher(k8s, reporter, oncall, settings.node_debounce_checks)
        self._stop = threading.Event()
        self._last_report_ts = time.time()
        self._threads: list[threading.Thread] = []

    def start(self) -> None:
        self._threads = [
            threading.Thread(target=self._watcher_loop, name="node-watcher", daemon=True),
            threading.Thread(target=self._telegram_loop, name="telegram-poll", daemon=True),
        ]
        for t in self._threads:
            t.start()

    def stop(self) -> None:
        self._stop.set()

    def next_report_seconds(self) -> int | None:
        cfg = self.app_settings.get_all()
        try:
            hours = int(cfg.get("report_interval_hours", "6"))
        except ValueError:
            hours = 0
        if hours <= 0:
            return None
        elapsed = time.time() - self._last_report_ts
        return max(0, int(hours * 3600 - elapsed))

    def _watcher_loop(self) -> None:
        while not self._stop.is_set():
            try:
                self.watcher.check()
            except Exception as exc:
                log.warning("node check failed: %s", exc)

            try:
                self.oncall.check_escalations()
            except Exception as exc:
                log.warning("escalation check failed: %s", exc)

            cfg = self.app_settings.get_all()
            try:
                hours = int(cfg.get("report_interval_hours", "6"))
            except ValueError:
                hours = 0

            if hours > 0 and (time.time() - self._last_report_ts) >= hours * 3600:
                self._last_report_ts = time.time()
                if self.k8s.available:
                    try:
                        report = self.reporter.node_report(f"🕐 {hours} Saatlik Node Raporu")
                        self.oncall.dispatch_general_report(report)
                    except Exception as exc:
                        log.warning("scheduled report failed: %s", exc)

            self._stop.wait(self.settings.node_check_interval)

    def _help_text(self) -> str:
        return (
            "🤖 Komutlar\n"
            "• cluster info - DevOps özet raporu\n"
            "• cluster info nodes - node'lara göre pod dağılımı\n"
            f"• cluster info {self.settings.gamegaraj_namespace} - servisler, tag ve node'lar\n"
            "• cluster info argocd - ArgoCD uygulamaları\n"
            "• cluster info events - son 1sa Warning event'leri\n"
            "• nodes - sadece node sağlığı\n"
            "• logs <servis> - pod'un son 30 satır logu\n"
            "• /ack - açık nöbet olaylarını üzerine al\n"
            "• /myid - kişisel Telegram Chat ID'ni öğren\n"
            "• /oncall - aktif nöbetçiyi gör"
        )

    def _handle_command(self, chat_id: str, text: str) -> None:
        if text == "/myid":
            self.telegram.send(chat_id, "", f"🆔 Chat ID'niz: `{chat_id}`\nBu ID'yi Web UI'daki kullanıcı profilinize ekleyebilirsiniz.")
            return
        if text == "/oncall":
            info = self.oncall.get_active_oncall()
            p = info["primary"]["name"] if info["primary"] else "Atanmamış"
            b = info["backup"]["name"] if info["backup"] else "Atanmamış"
            mode = "🌙 Gece Nöbeti Aktif" if info["is_night"] else "☀️ Gündüz Modu"
            self.telegram.send(chat_id, "", f"{mode}\n👤 Birincil: {p}\n🛡 Yedek: {b}")
            return
        if text == "/ack":
            count = self.incidents.ack_all_open()
            self.telegram.send(chat_id, "", f"✅ {count} açık olay üzerine alındı (Ack)." if count else "Açık olay yok.")
            return

        if chat_id != self.settings.telegram_chat_id:
            return

        try:
            if text in CLUSTER_COMMANDS:
                self.telegram.send_parts(chat_id, self.reporter.cluster_info())
            elif text in CLUSTER_NODES_COMMANDS:
                self.telegram.send_parts(chat_id, self.reporter.cluster_info_nodes())
            elif text in CLUSTER_APPS_COMMANDS:
                self.telegram.send_parts(chat_id, self.reporter.cluster_info_apps())
            elif text in CLUSTER_ARGOCD_COMMANDS:
                self.telegram.send_parts(chat_id, self.reporter.cluster_info_argocd())
            elif text in CLUSTER_EVENTS_COMMANDS:
                self.telegram.send_parts(chat_id, self.reporter.cluster_info_events())
            elif text in NODE_COMMANDS:
                self.telegram.send(chat_id, "", self.reporter.node_report("🖥 Node Durumu"))
            elif text in HELP_COMMANDS:
                self.telegram.send(chat_id, "", self._help_text())
            elif text.startswith("logs ") or text.startswith("/logs "):
                self.telegram.send_parts(chat_id, self.reporter.pod_logs_report(text.split(" ", 1)[1]))
        except Exception as exc:
            log.warning("command '%s' failed: %s", text, exc)
            self.telegram.send(chat_id, "", f"bilgi alınamadı: {exc}")

    def _telegram_loop(self) -> None:
        if not self.telegram.is_configured():
            log.info("Telegram token yok; polling baslatilmadi")
            return
        offset = 0
        try:
            offset = self.telegram.drain_pending()
        except Exception as exc:
            log.warning("drain failed: %s", exc)

        while not self._stop.is_set():
            try:
                payload = self.telegram.get_updates(offset)
                if not payload.get("ok"):
                    self._stop.wait(3)
                    continue
                for update in payload.get("result") or []:
                    offset = int(update["update_id"]) + 1
                    msg = update.get("message") or {}
                    chat = str((msg.get("chat") or {}).get("id", ""))
                    if chat:
                        self._handle_command(chat, normalize_cmd(msg.get("text") or ""))
            except Exception as exc:
                log.warning("telegram poll error: %s", exc)
                self._stop.wait(5)
