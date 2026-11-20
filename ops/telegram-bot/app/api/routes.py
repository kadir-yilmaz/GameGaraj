from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import HTMLResponse
from pydantic import BaseModel, Field

WEB_HTML = Path(__file__).resolve().parent.parent / "web" / "index.html"


class UserCreate(BaseModel):
    name: str = Field(min_length=1, max_length=80)
    email: str = Field(min_length=3, max_length=120)
    telegram_chat_id: str = ""
    color: str = "#3b82f6"
    notify_email: bool = True
    notify_telegram: bool = True


class UserUpdate(UserCreate):
    active: bool = True


class UserTogglesUpdate(BaseModel):
    active: bool | None = None
    notify_email: bool | None = None
    notify_telegram: bool | None = None


class DayScheduleUpdate(BaseModel):
    primary_user_id: int | None = None
    backup_user_id: int | None = None


class SwapDaysRequest(BaseModel):
    source_day: int = Field(ge=0, le=6)
    target_day: int = Field(ge=0, le=6)
    role: str = Field(default="primary", pattern="^(primary|backup)$")


class OverrideRequest(BaseModel):
    date: str = Field(pattern=r"^\d{4}-\d{2}-\d{2}$")
    primary_user_id: int | None = None
    backup_user_id: int | None = None
    note: str = ""


class SettingsUpdate(BaseModel):
    report_interval_hours: str | None = None
    night_start: str | None = Field(default=None, pattern=r"^\d{2}:\d{2}$")
    night_end: str | None = Field(default=None, pattern=r"^\d{2}:\d{2}$")
    mute_general_at_night: str | None = None
    notify_group_on_critical: str | None = None
    notify_oncall_email: str | None = None
    notify_oncall_telegram: str | None = None
    escalation_minutes: str | None = None
    telegram_group_enabled: str | None = None
    telegram_group_chat_id: str | None = None
    node_alerts_enabled: str | None = None
    smtp_host: str | None = None
    smtp_port: str | None = None
    smtp_user: str | None = None
    smtp_pass: str | None = None
    smtp_from: str | None = None
    smtp_tls: str | None = None


class TestNotifyRequest(BaseModel):
    user_id: int | None = None
    channel: str = Field(pattern="^(email|telegram_dm|telegram_group)$")
    email_override: str = ""


def create_router() -> APIRouter:
    router = APIRouter()

    @router.get("/", response_class=HTMLResponse)
    def index() -> HTMLResponse:
        return HTMLResponse(WEB_HTML.read_text(encoding="utf-8"))

    @router.get("/healthz")
    def healthz() -> dict:
        return {"status": "ok"}

    @router.get("/api/status")
    def get_status(request: Request) -> dict:
        c = request.app.state.container
        return {
            "cluster": c.reporter.cluster_snapshot(),
            "oncall": c.oncall.get_active_oncall(),
            "next_report_seconds": c.runner.next_report_seconds(),
            "smtp_configured": c.email.is_configured(),
            "telegram_configured": c.telegram.is_configured(),
        }

    @router.get("/api/users")
    def list_users(request: Request) -> list[dict]:
        return request.app.state.container.users.list_all()

    @router.post("/api/users")
    def create_user(payload: UserCreate, request: Request) -> dict:
        try:
            return request.app.state.container.users.create(
                name=payload.name,
                email=payload.email,
                telegram_chat_id=payload.telegram_chat_id,
                color=payload.color,
                notify_email=payload.notify_email,
                notify_telegram=payload.notify_telegram,
            )
        except Exception as exc:
            raise HTTPException(status_code=400, detail=f"Kullanıcı eklenemedi: {exc}") from exc

    @router.put("/api/users/{user_id}")
    def update_user(user_id: int, payload: UserUpdate, request: Request) -> dict:
        updated = request.app.state.container.users.update(
            user_id=user_id,
            name=payload.name,
            email=payload.email,
            telegram_chat_id=payload.telegram_chat_id,
            color=payload.color,
            active=payload.active,
            notify_email=payload.notify_email,
            notify_telegram=payload.notify_telegram,
        )
        if not updated:
            raise HTTPException(status_code=404, detail="Kullanıcı bulunamadı")
        return updated

    @router.patch("/api/users/{user_id}/toggles")
    def toggle_user(user_id: int, payload: UserTogglesUpdate, request: Request) -> dict:
        updated = request.app.state.container.users.set_toggles(
            user_id=user_id,
            active=payload.active,
            notify_email=payload.notify_email,
            notify_telegram=payload.notify_telegram,
        )
        if not updated:
            raise HTTPException(status_code=404, detail="Kullanıcı bulunamadı")
        return updated

    @router.delete("/api/users/{user_id}")
    def delete_user(user_id: int, request: Request) -> dict:
        if not request.app.state.container.users.delete(user_id):
            raise HTTPException(status_code=404, detail="Kullanıcı bulunamadı")
        return {"deleted": True}

    @router.get("/api/schedule")
    def get_schedule(request: Request) -> dict:
        repo = request.app.state.container.schedule
        return {"weekly": repo.get_weekly(), "overrides": repo.list_overrides()}

    @router.put("/api/schedule/day/{day_of_week}")
    def update_day(day_of_week: int, payload: DayScheduleUpdate, request: Request) -> dict:
        if day_of_week < 0 or day_of_week > 6:
            raise HTTPException(status_code=400, detail="Gün 0-6 arasında olmalı")
        return request.app.state.container.schedule.set_day(
            day_of_week, payload.primary_user_id, payload.backup_user_id
        )

    @router.post("/api/schedule/swap")
    def swap_days(payload: SwapDaysRequest, request: Request) -> dict:
        request.app.state.container.schedule.swap_days(payload.source_day, payload.target_day, payload.role)
        return {"ok": True}

    @router.post("/api/schedule/overrides")
    def set_override(payload: OverrideRequest, request: Request) -> dict:
        return request.app.state.container.schedule.set_override(
            payload.date, payload.primary_user_id, payload.backup_user_id, payload.note
        )

    @router.delete("/api/schedule/overrides/{date_str}")
    def delete_override(date_str: str, request: Request) -> dict:
        if not request.app.state.container.schedule.delete_override(date_str):
            raise HTTPException(status_code=404, detail="İstisna bulunamadı")
        return {"deleted": True}

    @router.get("/api/settings")
    def get_settings(request: Request) -> dict:
        cfg = dict(request.app.state.container.app_settings.get_all())
        has_pass = bool(cfg.get("smtp_pass") or request.app.state.container.settings.smtp_pass)
        cfg["smtp_pass"] = ""
        cfg["smtp_pass_set"] = "true" if has_pass else "false"
        return cfg

    @router.put("/api/settings")
    def update_settings(payload: SettingsUpdate, request: Request) -> dict:
        updates = {k: v for k, v in payload.model_dump().items() if v is not None}
        if updates.get("smtp_pass") in {"", "__UNCHANGED__"}:
            updates.pop("smtp_pass", None)
        request.app.state.container.app_settings.update_many(updates)
        return get_settings(request)

    @router.get("/api/incidents")
    def list_incidents(request: Request) -> list[dict]:
        return request.app.state.container.incidents.list_recent()

    @router.post("/api/incidents/{incident_id}/ack")
    def ack_incident(incident_id: int, request: Request) -> dict:
        inc = request.app.state.container.incidents.ack(incident_id)
        if not inc:
            raise HTTPException(status_code=404, detail="Olay bulunamadı")
        return inc

    @router.get("/api/logs")
    def list_logs(request: Request) -> list[dict]:
        return request.app.state.container.logs.list_recent()

    @router.post("/api/test-notify")
    def test_notify(payload: TestNotifyRequest, request: Request) -> dict:
        ok, err = request.app.state.container.oncall.send_test_notification(
            payload.user_id, payload.channel, payload.email_override
        )
        if not ok:
            raise HTTPException(status_code=400, detail=err)
        return {"sent": True}

    return router
