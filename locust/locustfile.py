import itertools
import json
import random
import threading
import time
from collections import defaultdict
from datetime import datetime
from pathlib import Path

from locust import FastHttpUser, LoadTestShape, events, tag, task
from locust.runners import MasterRunner, WorkerRunner

DEFAULT_HOST = "http://100.67.37.25:30000"
DEFAULT_KEYWORDS = ["rtx", "asus", "msi", "gaming", "laptop", "ssd", "ram", "monitör"]
RESULTS_DIR = Path(__file__).resolve().parent / "results"

TARGET_ENV_HOSTS = {
    "wk1-tailscale": ["http://100.67.37.25:30000"],
    "wk2-tailscale": ["http://100.120.194.97:30000"],
    "wk1+wk2-tailscale": ["http://100.67.37.25:30000", "http://100.120.194.97:30000"],
    "all3-tailscale": [
        "http://100.67.37.25:30000",
        "http://100.120.194.97:30000",
        "http://100.100.32.3:30000",
    ],
    "wk1+wk2-lan": ["http://192.168.1.5:30000", "http://192.168.1.200:30000"],
    "all3-lan": [
        "http://192.168.1.5:30000",
        "http://192.168.1.200:30000",
        "http://192.168.1.56:30000",
    ],
    "cloudflare": ["https://gateway.kadiryilmaz.online"],
}

HOST_LABELS = {
    "http://100.67.37.25:30000": "wk1 (tailscale)",
    "http://100.120.194.97:30000": "wk2 (tailscale)",
    "http://100.100.32.3:30000": "kadirserver (tailscale)",
    "http://192.168.1.5:30000": "wk1 (lan)",
    "http://192.168.1.200:30000": "wk2 (lan)",
    "http://192.168.1.56:30000": "kadirserver (lan)",
    "https://gateway.kadiryilmaz.online": "cloudflare",
}

# Preset profilleri: hedef_rps -> (otomatik_kullanici_sayisi, saniyede_artis_hizi)
RPS_PROFILES = {
    "500": (250, 50),
    "1000": (500, 100),
    "2000": (1000, 200),
    "5000": (2500, 500),
    "10000": (5000, 1000),
}

_PRODUCTS_LOCK = threading.Lock()
_PRODUCTS: list[dict] = []
_USER_SEQ = itertools.count(1)

_NODE_STATS_LOCK = threading.Lock()
_WORKER_NODE_STATS: dict[str, dict] = defaultdict(lambda: {"count": 0, "fail": 0, "total_ms": 0.0, "max_ms": 0.0})
_MASTER_NODE_STATS: dict[str, dict] = defaultdict(lambda: {"count": 0, "fail": 0, "total_ms": 0.0, "max_ms": 0.0})
_TEST_STARTED_AT: float = 0.0


def _record_node_stat(node_label: str, ok: bool, elapsed_ms: float):
    with _NODE_STATS_LOCK:
        bucket = _WORKER_NODE_STATS[node_label]
        bucket["count"] += 1
        if not ok:
            bucket["fail"] += 1
        bucket["total_ms"] += elapsed_ms
        if elapsed_ms > bucket["max_ms"]:
            bucket["max_ms"] = elapsed_ms


@events.test_start.add_listener
def _(environment, **kwargs):
    global _TEST_STARTED_AT
    _TEST_STARTED_AT = time.perf_counter()
    with _NODE_STATS_LOCK:
        _WORKER_NODE_STATS.clear()
        _MASTER_NODE_STATS.clear()


@events.report_to_master.add_listener
def _(client_id, data):
    with _NODE_STATS_LOCK:
        if not _WORKER_NODE_STATS:
            return
        data["node_stats"] = {k: dict(v) for k, v in _WORKER_NODE_STATS.items()}
        _WORKER_NODE_STATS.clear()


@events.worker_report.add_listener
def _(client_id, data):
    node_stats = data.get("node_stats")
    if not node_stats:
        return
    with _NODE_STATS_LOCK:
        for node_label, stats in node_stats.items():
            bucket = _MASTER_NODE_STATS[node_label]
            bucket["count"] += stats["count"]
            bucket["fail"] += stats["fail"]
            bucket["total_ms"] += stats["total_ms"]
            if stats["max_ms"] > bucket["max_ms"]:
                bucket["max_ms"] = stats["max_ms"]


@events.request.add_listener
def _(request_type, name, response_time, response_length, exception, context, **kwargs):
    node_label = (context or {}).get("node") or "unknown"
    _record_node_stat(node_label, exception is None, float(response_time or 0.0))


@events.test_stop.add_listener
def _(environment, **kwargs):
    if isinstance(environment.runner, WorkerRunner):
        return

    elapsed = max(time.perf_counter() - _TEST_STARTED_AT, 0.001) if _TEST_STARTED_AT else 1.0
    opts = getattr(environment, "parsed_options", None)
    target_env = getattr(opts, "target_env", "custom")
    target_rps = getattr(opts, "target_rps", "manual")
    intensity = getattr(opts, "intensity", "fast")

    # Local tek-process modda worker_report calismadigi icin _WORKER_NODE_STATS'i birlestir
    with _NODE_STATS_LOCK:
        merged_nodes = {k: dict(v) for k, v in _MASTER_NODE_STATS.items()}
        for k, v in _WORKER_NODE_STATS.items():
            bucket = merged_nodes.setdefault(k, {"count": 0, "fail": 0, "total_ms": 0.0, "max_ms": 0.0})
            bucket["count"] += v["count"]
            bucket["fail"] += v["fail"]
            bucket["total_ms"] += v["total_ms"]
            bucket["max_ms"] = max(bucket["max_ms"], v["max_ms"])

    total_stats = environment.stats.total
    total_reqs = total_stats.num_requests
    total_fails = total_stats.num_failures
    fail_rate = (total_fails / total_reqs * 100.0) if total_reqs else 0.0
    avg_rps = total_reqs / elapsed

    lines = [
        "=== GameGaraj Locust Test Sonucu ===",
        f"Tarih       : {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"Target Env  : {target_env}",
        f"Target RPS  : {target_rps} | Intensity: {intensity}",
        f"Sure (sn)   : {elapsed:.1f}",
        f"Toplam Istek: {total_reqs} | Hata: {total_fails} ({fail_rate:.2f}%)",
        f"Ortalama RPS: {avg_rps:.1f} | Current RPS: {total_stats.current_rps:.1f}",
        (
            f"Latency (ms): avg={total_stats.avg_response_time:.1f} "
            f"p50={total_stats.get_response_time_percentile(0.50):.0f} "
            f"p95={total_stats.get_response_time_percentile(0.95):.0f} "
            f"p99={total_stats.get_response_time_percentile(0.99):.0f} "
            f"max={total_stats.max_response_time:.0f}"
        ),
        "",
        "--- Node Bazli Performans (Giris Noktasi) ---",
    ]

    node_json = {}
    for node_label, data in sorted(merged_nodes.items()):
        cnt = data["count"]
        if cnt == 0:
            continue
        n_rps = cnt / elapsed
        n_avg = data["total_ms"] / cnt
        n_fail_pct = (data["fail"] / cnt * 100.0)
        lines.append(
            f"  {node_label:<24} rps={n_rps:>7.1f}  count={cnt:<7} "
            f"fail={data['fail']:<5} ({n_fail_pct:.2f}%)  avg={n_avg:>7.1f}ms  max={data['max_ms']:>7.0f}ms"
        )
        node_json[node_label] = {
            "rps": round(n_rps, 2),
            "count": cnt,
            "fail": data["fail"],
            "fail_pct": round(n_fail_pct, 2),
            "avg_ms": round(n_avg, 2),
            "max_ms": round(data["max_ms"], 2),
        }

    lines.extend(["", "--- Endpoint Bazli Performans ---"])
    endpoints_json = {}
    for (name, method), entry in sorted(environment.stats.entries.items()):
        if entry.num_requests == 0:
            continue
        e_rps = entry.num_requests / elapsed
        e_p50 = entry.get_response_time_percentile(0.50)
        e_p95 = entry.get_response_time_percentile(0.95)
        lines.append(
            f"  {method:<6} {name:<40} rps={e_rps:>6.1f}  count={entry.num_requests:<7} "
            f"fail={entry.num_failures:<5} avg={entry.avg_response_time:>6.1f}ms  p50={e_p50:>5.0f}ms  p95={e_p95:>5.0f}ms"
        )
        endpoints_json[f"{method} {name}"] = {
            "rps": round(e_rps, 2),
            "count": entry.num_requests,
            "fail": entry.num_failures,
            "avg_ms": round(entry.avg_response_time, 2),
            "p50_ms": round(e_p50, 2),
            "p95_ms": round(e_p95, 2),
        }

    summary_text = "\n".join(lines) + "\n"
    run_dir = RESULTS_DIR / datetime.now().strftime("%Y%m%d-%H%M%S")
    run_dir.mkdir(parents=True, exist_ok=True)
    (run_dir / "summary.txt").write_text(summary_text, encoding="utf-8")
    (RESULTS_DIR / "latest_summary.txt").write_text(summary_text, encoding="utf-8")
    (run_dir / "results.json").write_text(
        json.dumps(
            {
                "timestamp": datetime.now().isoformat(),
                "target_env": target_env,
                "target_rps": target_rps,
                "intensity": intensity,
                "elapsed_s": round(elapsed, 2),
                "total_requests": total_reqs,
                "total_failures": total_fails,
                "avg_rps": round(avg_rps, 2),
                "p50_ms": round(total_stats.get_response_time_percentile(0.50), 2),
                "p95_ms": round(total_stats.get_response_time_percentile(0.95), 2),
                "p99_ms": round(total_stats.get_response_time_percentile(0.99), 2),
                "nodes": node_json,
                "endpoints": endpoints_json,
            },
            indent=2,
        ),
        encoding="utf-8",
    )


@events.init_command_line_parser.add_listener
def _(parser):
    parser.add_argument(
        "--target-env",
        type=str,
        default="wk1+wk2-tailscale",
        choices=[
            "custom",
            "wk1-tailscale",
            "wk2-tailscale",
            "wk1+wk2-tailscale",
            "all3-tailscale",
            "wk1+wk2-lan",
            "all3-lan",
            "cloudflare",
        ],
        help="Hedef giris noktasi (custom = ustteki Host kutusunu kullanir)",
    )
    parser.add_argument(
        "--target-rps",
        type=str,
        default="manual",
        choices=["manual", "500", "1000", "2000", "5000", "10000"],
        help="Hedef RPS profili (secilirse Users ve Ramp-up otomatik ayarlanir; manual = elle giris)",
    )
    parser.add_argument(
        "--intensity",
        type=str,
        default="fast",
        choices=["realistic", "fast", "max"],
        help="Manual modda istek siddeti: realistic (0.2-1.0s), fast (0.02-0.1s), max (0s bekleme)",
    )


class TargetRpsShape(LoadTestShape):
    """
    Target RPS secildiginde Users ve Ramp-up degerlerini otomatik yonetir.
    'manual' secilirse Web UI'dan girilen Users ve Ramp-up kullanilir.
    """

    use_common_options = True

    def tick(self):
        opts = getattr(self.runner.environment, "parsed_options", None) if self.runner else None
        target_rps = getattr(opts, "target_rps", "manual")

        if target_rps in RPS_PROFILES:
            users, spawn_rate = RPS_PROFILES[target_rps]
            return (users, spawn_rate)

        users = getattr(opts, "num_users", None) or 100
        spawn_rate = getattr(opts, "spawn_rate", None) or 10
        return (users, spawn_rate)


def _normalize_product(raw: dict) -> dict | None:
    if not isinstance(raw, dict):
        return None
    product_id = raw.get("id")
    name = raw.get("name")
    if not product_id or not name:
        return None

    picture_url = raw.get("firstImageUrl")
    image_urls = raw.get("imageUrls") or []
    if not picture_url and image_urls:
        picture_url = image_urls[0]

    return {
        "id": str(product_id),
        "name": str(name),
        "category_id": str(raw.get("categoryId") or ""),
        "price": float(raw.get("price") or 0),
        "picture_url": str(picture_url or ""),
        "product_slug": str(raw.get("slug") or ""),
        "brand": str(raw.get("brand") or ""),
    }


def _search_term(product: dict | None) -> str:
    if not product:
        return random.choice(DEFAULT_KEYWORDS)
    if product.get("brand"):
        return product["brand"]
    words = product.get("name", "").split()
    return words[0] if words else random.choice(DEFAULT_KEYWORDS)


def _to_basket_payload(product: dict, quantity: int = 1) -> dict:
    return {
        "Id": product["id"],
        "Name": product["name"],
        "CategoryId": product["category_id"],
        "Price": product["price"],
        "PictureUrl": product["picture_url"],
        "ProductSlug": product["product_slug"],
        "Quantity": quantity,
        "Brand": product["brand"],
    }


class GameGarajUser(FastHttpUser):
    """
    GameGaraj Locust load test user.

    Web UI'da:
      - 'Target Env': wk1-tailscale, wk2-tailscale, wk1+wk2-tailscale, all3-tailscale, wk1+wk2-lan, all3-lan, cloudflare, custom
      - 'Target RPS' (manual, 500, 1000, 2000, 5000, 10000): Secilirse Users/Ramp-up otomatik profil olarak uygulanir.
      - 'Intensity' (realistic, fast, max): Manual modda kullanici basina bekleme suresini belirler.
      - 'Tags': search, catalog, basket, e2e
    """

    host = DEFAULT_HOST
    connection_timeout = 10.0
    network_timeout = 30.0

    def context(self):
        return {"node": getattr(self, "node_label", "unknown")}

    def wait_time(self):
        opts = getattr(self.environment, "parsed_options", None)
        target_rps = getattr(opts, "target_rps", "manual")
        intensity = getattr(opts, "intensity", "fast")

        if target_rps in RPS_PROFILES:
            profile_users, _ = RPS_PROFILES[target_rps]
            per_user_rps = int(target_rps) / profile_users
            now = time.perf_counter()
            last = getattr(self, "_last_pace_ts", None)
            self._last_pace_ts = now
            if last is None:
                return 0
            delay = (1.0 / per_user_rps) - (now - last)
            return delay if delay > 0 else 0

        if intensity == "max":
            return 0
        if intensity == "fast":
            return random.uniform(0.02, 0.1)
        return random.uniform(0.2, 1.0)

    def on_start(self):
        seq = next(_USER_SEQ)
        self.user_id = f"guest-locust-{seq}"
        self.cart_items: list[dict] = []

        opts = getattr(self.environment, "parsed_options", None)
        target_env = getattr(opts, "target_env", "custom")
        hosts = TARGET_ENV_HOSTS.get(target_env)
        if hosts:
            chosen = hosts[seq % len(hosts)]
            self.client.base_url = chosen
            self.node_label = HOST_LABELS.get(chosen, chosen)
        else:
            self.node_label = HOST_LABELS.get(self.client.base_url or "", self.client.base_url or "custom")

        self._ensure_products_loaded()

    def _ensure_products_loaded(self):
        if _PRODUCTS:
            return
        with _PRODUCTS_LOCK:
            if _PRODUCTS:
                return
            for path in ("/api/catalog/products/featured", "/api/catalog/products"):
                with self.client.get(path, name="[setup] load_products", catch_response=True) as resp:
                    if resp.status_code != 200:
                        resp.failure(f"HTTP {resp.status_code}")
                        continue
                    try:
                        data = resp.json()
                        items = data.get("value", data) if isinstance(data, dict) else data
                        if isinstance(items, list):
                            parsed = [p for p in (_normalize_product(i) for i in items) if p]
                            if parsed:
                                random.shuffle(parsed)
                                _PRODUCTS.extend(parsed[:200])
                                resp.success()
                                return
                    except Exception as exc:
                        resp.failure(str(exc))

    def _pick_product(self) -> dict | None:
        return random.choice(_PRODUCTS) if _PRODUCTS else None

    def _basket_headers(self) -> dict[str, str]:
        return {
            "X-User-Id": self.user_id,
            "Content-Type": "application/json",
        }

    # -------------------------------------------------------------------------
    # Search API (~%35 E2E agirligi)
    # -------------------------------------------------------------------------
    @tag("search", "e2e")
    @task(20)
    def search_api_query(self):
        term = _search_term(self._pick_product())
        self.client.get(
            "/api/search",
            params={"q": term},
            name="search: /api/search?q=...",
        )

    @tag("search", "e2e")
    @task(10)
    def search_api_suggestions(self):
        term = _search_term(self._pick_product())[:3]
        self.client.get(
            "/api/search/suggestions",
            params={"q": term},
            name="search: /api/search/suggestions",
        )

    @tag("search", "e2e")
    @task(5)
    def search_api_featured(self):
        self.client.get(
            "/api/search/products/featured",
            name="search: /api/search/products/featured",
        )

    # -------------------------------------------------------------------------
    # Catalog API (~%35 E2E agirligi)
    # -------------------------------------------------------------------------
    @tag("catalog", "e2e")
    @task(18)
    def catalog_product_detail(self):
        product = self._pick_product()
        if not product:
            return
        self.client.get(
            f"/api/catalog/products/{product['id']}",
            name="catalog: /api/catalog/products/{id}",
        )

    @tag("catalog", "e2e")
    @task(10)
    def catalog_featured(self):
        self.client.get(
            "/api/catalog/products/featured",
            name="catalog: /api/catalog/products/featured",
        )

    @tag("catalog", "e2e")
    @task(7)
    def catalog_search(self):
        term = _search_term(self._pick_product())
        self.client.get(
            "/api/catalog/products/search",
            params={"q": term},
            name="catalog: /api/catalog/products/search",
        )

    # -------------------------------------------------------------------------
    # Basket API (~%30 E2E agirligi)
    # -------------------------------------------------------------------------
    @tag("basket", "e2e")
    @task(12)
    def basket_get(self):
        self.client.get(
            "/api/basket/baskets",
            headers=self._basket_headers(),
            name="basket: GET /api/basket/baskets",
        )

    @tag("basket", "e2e")
    @task(10)
    def basket_add(self):
        product = self._pick_product()
        if not product:
            return
        payload = _to_basket_payload(product, quantity=1)
        with self.client.post(
            "/api/basket/baskets/items",
            headers=self._basket_headers(),
            json=payload,
            name="basket: POST /api/basket/baskets/items",
            catch_response=True,
        ) as resp:
            if resp.status_code in (200, 201, 204):
                resp.success()
                if len(self.cart_items) < 10:
                    self.cart_items.append(payload)
            else:
                resp.failure(f"HTTP {resp.status_code}")

    @tag("basket", "e2e")
    @task(5)
    def basket_update_quantities(self):
        if not self.cart_items:
            return
        item = random.choice(self.cart_items)
        item["Quantity"] = min(item["Quantity"] + 1, 5)
        self.client.post(
            "/api/basket/baskets",
            headers=self._basket_headers(),
            json={"UserId": self.user_id, "Items": self.cart_items},
            name="basket: POST /api/basket/baskets (update)",
        )

    @tag("basket", "e2e")
    @task(3)
    def basket_remove(self):
        if not self.cart_items:
            return
        removed = self.cart_items.pop(0)
        with self.client.delete(
            f"/api/basket/baskets/items/{removed['Id']}",
            headers=self._basket_headers(),
            name="basket: DELETE /api/basket/baskets/items/{id}",
            catch_response=True,
        ) as resp:
            if resp.status_code in (200, 204, 400, 404):
                resp.success()
            else:
                resp.failure(f"HTTP {resp.status_code}")
