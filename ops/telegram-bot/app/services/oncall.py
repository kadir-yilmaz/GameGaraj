from __future__ import annotations

from datetime import datetime, time as dtime, timezone

from app.config import Settings
from app.db.repositories import (
    AlertLogRepository,
    IncidentRepository,
    ScheduleRepository,
    SettingsRepository,
    UserRepository,
)
from app.notifiers.email import EmailNotifier
from app.notifiers.ntfy import NtfyNotifier
from app.notifiers.telegram import TelegramNotifier


def _parse_hhmm(value: str, fallback: str) -> dtime:
    raw = (value or fallback).strip()
    try:
        hh, mm = raw.split(":", 1)
        return dtime(hour=int(hh) % 24, minute=int(mm) % 60)
    except Exception:
        hh, mm = fallback.split(":", 1)
        return dtime(hour=int(hh), minute=int(mm))


class OnCallService:
    """Handles night-window evaluation, active on-call lookup, alert routing, and escalations."""

    def __init__(
        self,
        settings: Settings,
        users: UserRepository,
        schedule: ScheduleRepository,
        app_settings: SettingsRepository,
        incidents: IncidentRepository,
        logs: AlertLogRepository,
        telegram: TelegramNotifier,
        email: EmailNotifier,
        ntfy: NtfyNotifier,
    ) -> None:
        self.settings = settings
        self.users = users
        self.schedule = schedule
        self.app_settings = app_settings
        self.incidents = incidents
        self.logs = logs
        self.telegram = telegram
        self.email = email
        self.ntfy = ntfy

    def is_night_window(self, now: datetime | None = None) -> bool:
        cfg = self.app_settings.get_all()
        start = _parse_hhmm(cfg.get("night_start", "22:00"), "22:00")
        end = _parse_hhmm(cfg.get("night_end", "08:00"), "08:00")
        cur = (now or datetime.now(self.settings.tz)).time()
        if start == end:
            return False
        if start < end:
            return start <= cur < end
        return cur >= start or cur < end

    def get_active_oncall(self, now: datetime | None = None) -> dict:
        local_now = now or datetime.now(self.settings.tz)
        date_str = local_now.strftime("%Y-%m-%d")
        override = self.schedule.get_override(date_str)
        if override:
            primary_id = override.get("primary_user_id")
            backup_id = override.get("backup_user_id")
            source = f"override:{date_str}"
        else:
            day = self.schedule.get_day(local_now.weekday())
            primary_id = day.get("primary_user_id")
            backup_id = day.get("backup_user_id")
            source = f"weekly:{local_now.weekday()}"

        primary = self.users.get(primary_id) if primary_id else None
        backup = self.users.get(backup_id) if backup_id else None
        if primary and not primary.get("active"):
            primary = None
        if backup and not backup.get("active"):
            backup = None

        return {
            "is_night": self.is_night_window(local_now),
            "day_of_week": local_now.weekday(),
            "date": date_str,
            "source": source,
            "primary": primary,
            "backup": backup,
        }

    def _send_and_log(
        self,
        level: str,
        channel: str,
        recipient: str,
        subject: str,
        body: str,
        priority: int = 5,
        tags: list[str] | None = None,
    ) -> tuple[bool, str]:
        if channel == "email":
            ok, err = self.email.send(recipient, subject, body)
        elif channel == "telegram":
            ok, err = self.telegram.send(recipient, subject, body)
        elif channel == "ntfy":
            ok, err = self.ntfy.send(recipient, subject, body, priority=priority, tags=tags)
        else:
            ok, err = False, f"Bilinmeyen kanal: {channel}"
        self.logs.add(level=level, channel=channel, recipient=recipient, message=f"{subject}\n{body}".strip(), success=ok, error=err)
        return ok, err

    def _group_chat_id(self, cfg: dict) -> str:
        if cfg.get("telegram_group_enabled", "true") != "true":
            return ""
        return (cfg.get("telegram_group_chat_id") or self.settings.telegram_chat_id).strip()

    def _notify_user(self, level: str, user: dict, subject: str, body: str, cfg: dict) -> None:
        if cfg.get("notify_oncall_email", "true") == "true" and user.get("notify_email", 1) and user.get("email"):
            self._send_and_log(level, "email", user["email"], subject, body)
        if cfg.get("notify_oncall_telegram", "true") == "true" and user.get("notify_telegram", 1) and user.get("telegram_chat_id"):
            self._send_and_log(level, "telegram", user["telegram_chat_id"], subject, body)
        if cfg.get("notify_oncall_ntfy", "true") == "true" and user.get("notify_ntfy", 1) and user.get("ntfy_topic"):
            is_urgent = level in {"critical", "escalation", "test"}
            self._send_and_log(
                level,
                "ntfy",
                user["ntfy_topic"],
                subject,
                body,
                priority=5 if is_urgent else 3,
                tags=["rotating_light"] if is_urgent else ["white_check_mark"],
            )

    def dispatch_general_report(self, body: str) -> bool:
        cfg = self.app_settings.get_all()
        if self.is_night_window() and cfg.get("mute_general_at_night", "true") == "true":
            return False
        group_id = self._group_chat_id(cfg)
        if not group_id:
            return False
        ok, _ = self._send_and_log("info", "telegram", group_id, "", body)
        return ok

    def dispatch_critical_alert(self, node_name: str, summary: str, detail: str) -> dict:
        cfg = self.app_settings.get_all()
        if cfg.get("node_alerts_enabled", "true") != "true":
            return {"id": 0, "summary": summary, "status": "muted"}
        oncall = self.get_active_oncall()
        primary = oncall["primary"]
        incident = self.incidents.create(node_name, summary, primary["id"] if primary else None)
        group_id = self._group_chat_id(cfg)

        if oncall["is_night"] and primary:
            subject = f"[NOBET #{incident['id']}] {summary}"
            oncall_note = f"\n\nNöbetçi: {primary['name']}\nÜzerine almak için Telegram'da /ack veya Web UI kullanın."
            self._notify_user("critical", primary, subject, detail + oncall_note, cfg)
            if cfg.get("notify_group_on_critical", "true") == "true" and group_id:
                self._send_and_log(
                    "critical",
                    "telegram",
                    group_id,
                    "",
                    f"{detail}\n\nGece nöbetçisi bilgilendirildi: {primary['name']}",
                )
        else:
            if group_id:
                self._send_and_log("critical", "telegram", group_id, "", detail)
        return incident

    def resolve_node_incident(self, node_name: str, detail: str) -> None:
        cfg = self.app_settings.get_all()
        resolved_count = self.incidents.resolve_by_node(node_name)
        group_id = self._group_chat_id(cfg)
        if cfg.get("node_alerts_enabled", "true") == "true" and group_id:
            self._send_and_log("resolved", "telegram", group_id, "", detail)
        if resolved_count and self.is_night_window():
            oncall = self.get_active_oncall()
            if oncall["primary"]:
                self._notify_user(
                    "resolved",
                    oncall["primary"],
                    f"[COZULDU] {node_name} Ready",
                    detail,
                    cfg,
                )

    def check_escalations(self) -> None:
        cfg = self.app_settings.get_all()
        try:
            esc_mins = int(cfg.get("escalation_minutes", "15"))
        except ValueError:
            esc_mins = 0
        if esc_mins <= 0 or not self.is_night_window():
            return

        now_utc = datetime.now(timezone.utc)
        oncall = self.get_active_oncall()
        backup = oncall["backup"]
        for inc in self.incidents.list_open_unescalated():
            created = datetime.fromisoformat(inc["created_at"])
            if (now_utc - created).total_seconds() < esc_mins * 60:
                continue
            self.incidents.mark_escalated(inc["id"])
            subject = f"🆘 [ESKALASYON #{inc['id']}] {inc['summary']}"
            body = f"Birincil nöbetçi {esc_mins} dk içinde /ack vermedi!\nOlay: {inc['summary']}"
            if backup:
                self._notify_user("escalation", backup, subject, body, cfg)
            if self.settings.telegram_chat_id:
                target = backup["name"] if backup else "Tüm Ekip"
                self._send_and_log("escalation", "telegram", self.settings.telegram_chat_id, subject, f"{body}\nYönlendirilen: {target}")

    def send_test_notification(self, user_id: int | None, channel: str, email_override: str = "") -> tuple[bool, str]:
        subject = "GameGaraj Nobet Sistemi Test Bildirimi"
        body = "Bu mesaj bildirim kanalının çalıştığını doğrulamak için gönderildi."
        cfg = self.app_settings.get_all()
        if channel == "telegram_group":
            group_id = (cfg.get("telegram_group_chat_id") or self.settings.telegram_chat_id).strip()
            return self._send_and_log("test", "telegram", group_id, subject, body)
        if channel == "email" and email_override.strip():
            return self._send_and_log("test", "email", email_override.strip(), subject, body)
        if not user_id:
            return False, "Kullanıcı seçilmedi"
        user = self.users.get(user_id)
        if not user:
            return False, "Kullanıcı bulunamadı"
        if channel == "email":
            return self._send_and_log("test", "email", user["email"], subject, body)
        if channel == "telegram_dm":
            return self._send_and_log("test", "telegram", user["telegram_chat_id"], subject, body)
        if channel == "ntfy":
            return self._send_and_log("test", "ntfy", user["ntfy_topic"], subject, body)
        return False, "Geçersiz kanal"
