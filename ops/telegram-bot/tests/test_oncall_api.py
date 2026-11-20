from __future__ import annotations

import os
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from fastapi.testclient import TestClient

from app.config import Settings
from app.main import create_app


def make_test_settings(db_path: str) -> Settings:
    return Settings(
        telegram_token_file="/nonexistent",
        telegram_chat_id="12345",
        gamegaraj_namespace="gamegaraj",
        argocd_namespace="argocd",
        tz_offset_hours=3.0,
        node_check_interval=30,
        node_debounce_checks=2,
        db_path=db_path,
        default_report_interval_hours=6,
        default_night_start="22:00",
        default_night_end="08:00",
        smtp_host="",
        smtp_port=587,
        smtp_user="",
        smtp_pass="",
        smtp_from="",
        smtp_tls=True,
        web_auth_password="",
    )


class TestOnCallApi(unittest.TestCase):
    def setUp(self) -> None:
        self.tmpdir = tempfile.TemporaryDirectory()
        db_path = os.path.join(self.tmpdir.name, "test.db")
        self.app = create_app(settings=make_test_settings(db_path), start_runner=False)
        self.client = TestClient(self.app)
        self.client.__enter__()

    def tearDown(self) -> None:
        self.client.__exit__(None, None, None)
        self.tmpdir.cleanup()

    def test_health_and_ui(self) -> None:
        self.assertEqual(self.client.get("/healthz").json(), {"status": "ok"})
        res = self.client.get("/")
        self.assertEqual(res.status_code, 200)
        self.assertIn("Nöbet Takvimi", res.text)

    def test_user_schedule_swap_and_override(self) -> None:
        u1 = self.client.post("/api/users", json={"name": "Ali", "email": "ali@example.com"}).json()
        u2 = self.client.post("/api/users", json={"name": "Veli", "email": "veli@example.com"}).json()

        self.client.put("/api/schedule/day/0", json={"primary_user_id": u1["id"], "backup_user_id": u2["id"]})
        self.client.put("/api/schedule/day/1", json={"primary_user_id": u2["id"], "backup_user_id": u1["id"]})

        # Swap primary between Monday (0) and Tuesday (1)
        res = self.client.post("/api/schedule/swap", json={"source_day": 0, "target_day": 1, "role": "primary"})
        self.assertEqual(res.status_code, 200)

        weekly = self.client.get("/api/schedule").json()["weekly"]
        self.assertEqual(weekly[0]["primary_user_id"], u2["id"])
        self.assertEqual(weekly[1]["primary_user_id"], u1["id"])

        # Date override
        self.client.post("/api/schedule/overrides", json={"date": "2026-11-20", "primary_user_id": u1["id"], "note": "Takas"})
        overrides = self.client.get("/api/schedule").json()["overrides"]
        self.assertEqual(len(overrides), 1)
        self.assertEqual(overrides[0]["primary_user_id"], u1["id"])

    def test_night_window_and_settings(self) -> None:
        tz = timezone(timedelta(hours=3))
        oncall = self.app.state.container.oncall
        self.assertTrue(oncall.is_night_window(datetime(2026, 11, 20, 23, 30, tzinfo=tz)))
        self.assertTrue(oncall.is_night_window(datetime(2026, 11, 20, 3, 0, tzinfo=tz)))
        self.assertFalse(oncall.is_night_window(datetime(2026, 11, 20, 14, 0, tzinfo=tz)))

        res = self.client.put(
            "/api/settings",
            json={
                "report_interval_hours": "12",
                "night_start": "23:00",
                "night_end": "07:00",
                "mute_general_at_night": "true",
                "notify_group_on_critical": "true",
                "notify_oncall_email": "true",
                "notify_oncall_telegram": "true",
                "escalation_minutes": "10",
            },
        )
        self.assertEqual(res.status_code, 200)
        self.assertEqual(res.json()["report_interval_hours"], "12")
        self.assertFalse(oncall.is_night_window(datetime(2026, 11, 20, 22, 30, tzinfo=tz)))

    def test_clock_aligned_report_schedule(self) -> None:
        tz = timezone(timedelta(hours=3))
        runner = self.app.state.container.runner

        self.client.put("/api/settings", json={"report_interval_hours": "1"})
        self.assertEqual(runner.next_report_seconds(datetime(2026, 11, 20, 12, 30, 0, tzinfo=tz)), 1800)

        self.client.put("/api/settings", json={"report_interval_hours": "3"})
        self.assertEqual(runner.next_report_seconds(datetime(2026, 11, 20, 13, 20, 0, tzinfo=tz)), 6000)

        self.client.put("/api/settings", json={"report_interval_hours": "0"})
        self.assertIsNone(runner.next_report_seconds(datetime(2026, 11, 20, 13, 20, 0, tzinfo=tz)))

        # Verify slot triggering fires at the top of the hour and only once per slot
        self.client.put("/api/settings", json={"report_interval_hours": "1"})
        sent: list[str] = []
        runner.k8s.available = True
        runner.reporter.node_report = lambda title: f"{title}: ok"
        runner.oncall.dispatch_general_report = lambda body: sent.append(body) or True

        self.assertFalse(runner._maybe_send_scheduled_report(datetime(2026, 11, 20, 11, 59, 30, tzinfo=tz)))
        self.assertTrue(runner._maybe_send_scheduled_report(datetime(2026, 11, 20, 12, 0, 10, tzinfo=tz)))
        self.assertFalse(runner._maybe_send_scheduled_report(datetime(2026, 11, 20, 12, 5, 0, tzinfo=tz)))
        self.assertEqual(len(sent), 1)
        self.assertIn("1 Saatlik Node Raporu", sent[0])


if __name__ == "__main__":
    unittest.main()
