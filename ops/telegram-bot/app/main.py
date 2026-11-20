from __future__ import annotations

import base64
import logging
import secrets
from contextlib import asynccontextmanager
from dataclasses import dataclass

from fastapi import FastAPI, Request
from fastapi.responses import Response

from app.api.routes import create_router
from app.config import Settings
from app.db.database import Database
from app.db.repositories import (
    AlertLogRepository,
    IncidentRepository,
    ScheduleRepository,
    SettingsRepository,
    UserRepository,
)
from app.k8s.client import K8sClient
from app.k8s.reports import ClusterReporter
from app.notifiers.email import EmailNotifier
from app.notifiers.telegram import TelegramNotifier
from app.services.oncall import OnCallService
from app.services.watcher import BotRunner

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] %(levelname)s %(name)s: %(message)s")


@dataclass
class AppContainer:
    settings: Settings
    db: Database
    users: UserRepository
    schedule: ScheduleRepository
    app_settings: SettingsRepository
    incidents: IncidentRepository
    logs: AlertLogRepository
    k8s: K8sClient
    reporter: ClusterReporter
    telegram: TelegramNotifier
    email: EmailNotifier
    oncall: OnCallService
    runner: BotRunner


def build_container(settings: Settings | None = None, start_runner: bool = True) -> AppContainer:
    cfg = settings or Settings.from_env()
    db = Database(cfg.db_path, cfg)
    users = UserRepository(db)
    schedule = ScheduleRepository(db)
    app_settings = SettingsRepository(db)
    incidents = IncidentRepository(db)
    logs = AlertLogRepository(db)
    k8s = K8sClient()
    reporter = ClusterReporter(k8s, cfg.gamegaraj_namespace, cfg.argocd_namespace, cfg.tz)
    telegram = TelegramNotifier(cfg.read_bot_token())
    email = EmailNotifier(cfg, app_settings)
    oncall = OnCallService(cfg, users, schedule, app_settings, incidents, logs, telegram, email)
    runner = BotRunner(cfg, k8s, reporter, app_settings, incidents, oncall, telegram)
    if start_runner:
        runner.start()
    return AppContainer(
        settings=cfg,
        db=db,
        users=users,
        schedule=schedule,
        app_settings=app_settings,
        incidents=incidents,
        logs=logs,
        k8s=k8s,
        reporter=reporter,
        telegram=telegram,
        email=email,
        oncall=oncall,
        runner=runner,
    )


def create_app(settings: Settings | None = None, start_runner: bool = True) -> FastAPI:
    @asynccontextmanager
    async def lifespan(application: FastAPI):
        container = build_container(settings=settings, start_runner=start_runner)
        application.state.container = container
        try:
            yield
        finally:
            container.runner.stop()
            container.db.close()

    application = FastAPI(title="GameGaraj Cluster Bot & On-Call", lifespan=lifespan)

    @application.middleware("http")
    async def optional_basic_auth(request: Request, call_next):
        pwd = request.app.state.container.settings.web_auth_password if hasattr(request.app.state, "container") else ""
        if pwd and request.url.path != "/healthz":
            auth = request.headers.get("authorization", "")
            authorized = False
            if auth.startswith("Basic "):
                try:
                    decoded = base64.b64decode(auth[6:]).decode("utf-8")
                    _, given = decoded.split(":", 1)
                    authorized = secrets.compare_digest(given, pwd)
                except Exception:
                    authorized = False
            if not authorized:
                return Response(
                    status_code=401,
                    headers={"WWW-Authenticate": 'Basic realm="GameGaraj On-Call"'},
                    content="Yetkisiz erisim",
                )
        return await call_next(request)

    application.include_router(create_router())
    return application


app = create_app()
