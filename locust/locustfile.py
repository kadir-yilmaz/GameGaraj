import itertools
import random
import threading
import time

from locust import FastHttpUser, events, tag, task

DEFAULT_HOST = "http://100.67.37.25:30000"
DEFAULT_KEYWORDS = ["rtx", "asus", "msi", "gaming", "laptop", "ssd", "ram", "monitör"]

_PRODUCTS_LOCK = threading.Lock()
_PRODUCTS: list[dict] = []
_USER_SEQ = itertools.count(1)


@events.init_command_line_parser.add_listener
def _(parser):
    parser.add_argument(
        "--target-rps",
        type=str,
        default="manual",
        choices=["manual", "500", "1000", "2000", "5000", "10000"],
        help="Hedef RPS (manual = sadece Users + Intensity belirler; sayi secilirse o RPS'e sabitler/zorlar)",
    )
    parser.add_argument(
        "--intensity",
        type=str,
        default="fast",
        choices=["realistic", "fast", "max"],
        help="Istek siddeti: realistic (0.2-1.0s), fast (0.02-0.1s), max (0s bekleme)",
    )


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
      - 'Target RPS' (manual, 500, 1000, 2000, 5000, 10000) ve 'Intensity' (realistic, fast, max) secilebilir.
      - 'Tags' alani ile tekil servis veya karma E2E testi secilebilir:
          search, catalog, basket, e2e
    """

    host = DEFAULT_HOST
    connection_timeout = 10.0
    network_timeout = 30.0

    def wait_time(self):
        opts = getattr(self.environment, "parsed_options", None)
        target_rps = getattr(opts, "target_rps", "manual")
        intensity = getattr(opts, "intensity", "fast")

        if target_rps and target_rps != "manual":
            runner = self.environment.runner
            user_count = max(runner.user_count if runner else 1, 1)
            per_user_rps = int(target_rps) / user_count
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
        self.user_id = f"guest-locust-{next(_USER_SEQ)}"
        self.cart_items: list[dict] = []
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
