import argparse
import asyncio
import csv
import json
import os
import random
import sys
import time
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

import aiohttp


DEFAULT_HOST = "https://gateway.kadiryilmaz.online"

ENDPOINT_SERVICE = {
    "product_search_keyword": "catalog",
    "product_detail": "catalog",
    "basket_add": "basket",
    "basket_get": "basket",
    "basket_update_quantities": "basket",
    "basket_remove": "basket",
}


@dataclass
class Product:
    id: str
    name: str
    category_id: str
    price: float
    picture_url: str
    product_slug: str
    brand: str


@dataclass
class LogicalUser:
    user_id: str
    step: int = 0
    products: list[Product] = field(default_factory=list)
    cart_items: list[dict] = field(default_factory=list)


class Stats:
    def __init__(self):
        self.started_at = time.perf_counter()
        self.total = 0
        self.failures = 0
        self.by_name = defaultdict(lambda: {"count": 0, "fail": 0, "total_ms": 0.0, "max_ms": 0.0})

    def record(self, name, ok, elapsed_ms):
        self.total += 1
        if not ok:
            self.failures += 1

        bucket = self.by_name[name]
        bucket["count"] += 1
        if not ok:
            bucket["fail"] += 1
        bucket["total_ms"] += elapsed_ms
        bucket["max_ms"] = max(bucket["max_ms"], elapsed_ms)

    def snapshot(self):
        elapsed = max(time.perf_counter() - self.started_at, 0.001)
        rps = self.total / elapsed
        fail_rate = (self.failures / self.total * 100) if self.total else 0

        endpoints = {}
        for name, bucket in sorted(self.by_name.items()):
            count = bucket["count"]
            avg_ms = bucket["total_ms"] / count if count else 0
            endpoints[name] = {
                "count": count,
                "fail": bucket["fail"],
                "avg_ms": avg_ms,
                "max_ms": bucket["max_ms"],
            }

        return {
            "time": time.strftime("%H:%M:%S"),
            "elapsed_s": elapsed,
            "total": self.total,
            "rps": rps,
            "failures": self.failures,
            "fail_rate": fail_rate,
            "endpoints": endpoints,
        }

    def format_summary(self):
        snapshot = self.snapshot()
        lines = [
            "",
            (
                f"[{snapshot['time']}] total={snapshot['total']} "
                f"rps={snapshot['rps']:.1f} fail={snapshot['failures']} "
                f"({snapshot['fail_rate']:.2f}%)"
            ),
        ]

        for name, endpoint in snapshot["endpoints"].items():
            lines.append(
                f"  {name:<28} count={endpoint['count']:<6} fail={endpoint['fail']:<4} "
                f"avg={endpoint['avg_ms']:>7.1f}ms max={endpoint['max_ms']:>7.1f}ms"
            )

        return "\n".join(lines)


class RunLogger:
    def __init__(self, output_dir):
        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        self.run_dir = Path(output_dir) / timestamp
        self.run_dir.mkdir(parents=True, exist_ok=True)
        self.summary_path = self.run_dir / "summary.log"
        self.csv_path = self.run_dir / "stats.csv"
        self.final_json_path = self.run_dir / "final_stats.json"
        self.previous_snapshot = None

        with self.csv_path.open("w", newline="", encoding="utf-8") as file:
            writer = csv.writer(file)
            writer.writerow([
                "time",
                "elapsed_s",
                "endpoint",
                "count",
                "fail",
                "avg_ms",
                "max_ms",
                "total",
                "rps",
                "current_rps",
                "endpoint_current_rps",
                "failures",
                "fail_rate",
            ])

    def write_line(self, text):
        with self.summary_path.open("a", encoding="utf-8") as file:
            file.write(text + "\n")

    def write_summary(self, stats, live_console=False):
        snapshot = stats.snapshot()
        interval = self.interval_snapshot(snapshot)
        formatted = self.format_summary(snapshot, interval)

        if live_console:
            self.render_live_console(formatted)
        else:
            print(formatted)

        self.write_line(formatted)

        with self.csv_path.open("a", newline="", encoding="utf-8") as file:
            writer = csv.writer(file)
            for endpoint, data in snapshot["endpoints"].items():
                writer.writerow([
                    snapshot["time"],
                    f"{snapshot['elapsed_s']:.3f}",
                    endpoint,
                    data["count"],
                    data["fail"],
                    f"{data['avg_ms']:.3f}",
                    f"{data['max_ms']:.3f}",
                    snapshot["total"],
                    f"{snapshot['rps']:.3f}",
                    f"{interval['current_rps']:.3f}",
                    f"{interval['endpoints'].get(endpoint, {}).get('current_rps', 0):.3f}",
                    snapshot["failures"],
                    f"{snapshot['fail_rate']:.3f}",
                ])

        self.previous_snapshot = snapshot

    def interval_snapshot(self, snapshot):
        if not self.previous_snapshot:
            elapsed = max(snapshot["elapsed_s"], 0.001)
            previous_total = 0
            previous_failures = 0
            previous_endpoints = {}
        else:
            elapsed = max(snapshot["elapsed_s"] - self.previous_snapshot["elapsed_s"], 0.001)
            previous_total = self.previous_snapshot["total"]
            previous_failures = self.previous_snapshot["failures"]
            previous_endpoints = self.previous_snapshot["endpoints"]

        delta_total = snapshot["total"] - previous_total
        delta_failures = snapshot["failures"] - previous_failures
        endpoints = {}
        services = defaultdict(lambda: {"count": 0, "fail": 0})

        for endpoint, data in snapshot["endpoints"].items():
            previous = previous_endpoints.get(endpoint, {"count": 0, "fail": 0})
            delta_count = data["count"] - previous["count"]
            delta_fail = data["fail"] - previous["fail"]
            current_rps = delta_count / elapsed
            endpoints[endpoint] = {
                "delta_count": delta_count,
                "delta_fail": delta_fail,
                "current_rps": current_rps,
            }

            service = ENDPOINT_SERVICE.get(endpoint, "unknown")
            services[service]["count"] += delta_count
            services[service]["fail"] += delta_fail

        return {
            "interval_s": elapsed,
            "delta_total": delta_total,
            "delta_failures": delta_failures,
            "current_rps": delta_total / elapsed,
            "endpoints": endpoints,
            "services": {
                service: {
                    "current_rps": values["count"] / elapsed,
                    "delta_count": values["count"],
                    "delta_fail": values["fail"],
                }
                for service, values in sorted(services.items())
            },
        }

    def format_summary(self, snapshot, interval):
        lines = [
            "",
            (
                f"[{snapshot['time']}] total={snapshot['total']} "
                f"avg_rps={snapshot['rps']:.1f} current_rps={interval['current_rps']:.1f} "
                f"fail={snapshot['failures']} ({snapshot['fail_rate']:.2f}%)"
            ),
        ]

        if interval["services"]:
            services = "  ".join(
                f"{service}={data['current_rps']:.1f} rps"
                for service, data in interval["services"].items()
            )
            lines.append(f"  services: {services}")

        for name, endpoint in snapshot["endpoints"].items():
            current = interval["endpoints"].get(name, {"current_rps": 0, "delta_fail": 0})
            lines.append(
                f"  {name:<28} current={current['current_rps']:>7.1f}rps "
                f"count={endpoint['count']:<6} fail={endpoint['fail']:<4} "
                f"avg={endpoint['avg_ms']:>7.1f}ms max={endpoint['max_ms']:>7.1f}ms"
            )

        return "\n".join(lines)

    def render_live_console(self, formatted):
        if os.name == "nt":
            os.system("cls")
        else:
            sys.stdout.write("\033[2J\033[H")
            sys.stdout.flush()

        print("GameGaraj Logical Load Runner")
        print(f"Logs: {self.run_dir}")
        print(formatted)

    def write_final(self, stats):
        with self.final_json_path.open("w", encoding="utf-8") as file:
            json.dump(stats.snapshot(), file, indent=2)

        print(f"\n[logs] summary: {self.summary_path}")
        print(f"[logs] csv: {self.csv_path}")
        print(f"[logs] final json: {self.final_json_path}")


def product_to_payload(product: Product, quantity=1):
    return {
        "Id": product.id,
        "Name": product.name,
        "CategoryId": product.category_id,
        "Price": product.price,
        "PictureUrl": product.picture_url,
        "ProductSlug": product.product_slug,
        "Quantity": quantity,
        "Brand": product.brand,
    }


def search_term_for(product: Product):
    if product.brand:
        return product.brand
    words = product.name.split()
    return words[0] if words else "gaming"


def normalize_product(raw):
    picture_url = raw.get("firstImageUrl")
    image_urls = raw.get("imageUrls") or []
    if not picture_url and image_urls:
        picture_url = image_urls[0]

    product_id = raw.get("id")
    name = raw.get("name")
    if not product_id or not name:
        return None

    return Product(
        id=product_id,
        name=name,
        category_id=raw.get("categoryId") or "",
        price=float(raw.get("price") or 0),
        picture_url=picture_url or "",
        product_slug=raw.get("slug") or "",
        brand=raw.get("brand") or "",
    )


async def fetch_products(session, host):
    print(f"[setup] Fetching products from {host}")
    for path in ("/api/catalog/products/featured", "/api/catalog/products"):
        async with session.get(f"{host}{path}") as response:
            data = await response.json(content_type=None)
            items = data.get("value", data) if isinstance(data, dict) else data
            products = [p for p in (normalize_product(item) for item in items) if p]
            if len(products) >= 3:
                random.shuffle(products)
                print(f"[setup] Loaded {len(products)} products using {path}")
                return products[:200]

    raise RuntimeError("Could not load enough products for the test.")


async def request(session, stats, method, url, *, name, headers=None, json_body=None, params=None, ok_statuses=None):
    ok_statuses = ok_statuses or {200, 201, 204}
    started = time.perf_counter()
    ok = False

    try:
        async with session.request(method, url, headers=headers, json=json_body, params=params) as response:
            await response.read()
            ok = response.status in ok_statuses
    except Exception:
        ok = False
    finally:
        elapsed_ms = (time.perf_counter() - started) * 1000
        stats.record(name, ok, elapsed_ms)


def reset_user(user, products):
    if len(products) >= 4:
        user.products = random.sample(products, 4)
    else:
        user.products = random.choices(products, k=4)
    user.cart_items = []
    user.step = 0


async def run_user_step(session, stats, host, user, products):
    if not user.products:
        reset_user(user, products)

    headers = {
        "X-User-Id": user.user_id,
        "Content-Type": "application/json",
    }

    step = user.step

    if step in (0, 3, 6):
        product = user.products[step // 3]
        await request(
            session,
            stats,
            "GET",
            f"{host}/api/catalog/products/search",
            name="product_search_keyword",
            params={"q": search_term_for(product)},
        )

    elif step in (1, 4, 7):
        product = user.products[step // 3]
        await request(session, stats, "GET", f"{host}/api/catalog/products/{product.id}", name="product_detail")

    elif step in (2, 5, 8):
        product = user.products[step // 3]
        await request(
            session,
            stats,
            "POST",
            f"{host}/api/basket/baskets/items",
            name="basket_add",
            headers=headers,
            json_body=product_to_payload(product),
        )
        user.cart_items.append(product_to_payload(product))

    elif step == 9:
        await request(session, stats, "GET", f"{host}/api/basket/baskets", name="basket_get", headers=headers)

    elif step == 10:
        if len(user.cart_items) >= 2:
            for item in random.sample(user.cart_items, k=2):
                item["Quantity"] += 1

        await request(
            session,
            stats,
            "POST",
            f"{host}/api/basket/baskets",
            name="basket_update_quantities",
            headers=headers,
            json_body={"UserId": user.user_id, "Items": user.cart_items},
        )

    elif step == 11:
        await request(session, stats, "GET", f"{host}/api/basket/baskets", name="basket_get", headers=headers)

    elif step == 12:
        product = user.products[2]
        await request(
            session,
            stats,
            "DELETE",
            f"{host}/api/basket/baskets/items/{product.id}",
            name="basket_remove",
            headers=headers,
            ok_statuses={200, 204, 400, 404},
        )
        user.cart_items = [item for item in user.cart_items if item["Id"] != product.id]

    elif step == 13:
        await request(session, stats, "GET", f"{host}/api/basket/baskets", name="basket_get", headers=headers)

    elif step == 14:
        product = user.products[3]
        await request(
            session,
            stats,
            "GET",
            f"{host}/api/catalog/products/search",
            name="product_search_keyword",
            params={"q": search_term_for(product)},
        )

    elif step == 15:
        product = user.products[3]
        await request(session, stats, "GET", f"{host}/api/catalog/products/{product.id}", name="product_detail")

    else:
        reset_user(user, products)
        return

    user.step += 1


async def progress_printer(stats, logger, interval, live_console):
    while True:
        await asyncio.sleep(interval)
        logger.write_summary(stats, live_console=live_console)


async def run(args):
    timeout = aiohttp.ClientTimeout(total=args.timeout)
    connector = aiohttp.TCPConnector(limit=args.max_connections, ttl_dns_cache=300)
    logger = RunLogger(args.output_dir)

    async with aiohttp.ClientSession(timeout=timeout, connector=connector) as session:
        products = await fetch_products(session, args.host)
        users = [LogicalUser(f"guest-locust-{i}") for i in range(1, args.logical_users + 1)]
        for user in users:
            reset_user(user, products)

        stats = Stats()
        printer_task = asyncio.create_task(progress_printer(stats, logger, args.summary_interval, not args.no_live_console))

        run_message = (
            f"[run] host={args.host} logical_users={args.logical_users} "
            f"rps={args.rps} duration={args.duration}s max_connections={args.max_connections}"
        )
        print(run_message)
        logger.write_line(run_message)

        interval = 1 / args.rps
        deadline = time.perf_counter() + args.duration
        next_tick = time.perf_counter()
        in_flight = set()

        try:
            while time.perf_counter() < deadline:
                user = random.choice(users)
                task = asyncio.create_task(run_user_step(session, stats, args.host, user, products))
                in_flight.add(task)
                task.add_done_callback(in_flight.discard)

                next_tick += interval
                delay = next_tick - time.perf_counter()
                if delay > 0:
                    await asyncio.sleep(delay)
                else:
                    await asyncio.sleep(0)

            if in_flight:
                await asyncio.gather(*in_flight, return_exceptions=True)
        finally:
            printer_task.cancel()

        logger.write_summary(stats, live_console=False)
        logger.write_final(stats)


def parse_duration(value):
    value = value.strip().lower()
    if value.endswith("ms"):
        return float(value[:-2]) / 1000
    if value.endswith("s"):
        return float(value[:-1])
    if value.endswith("m"):
        return float(value[:-1]) * 60
    if value.endswith("h"):
        return float(value[:-1]) * 3600
    return float(value)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--logical-users", type=int, default=10_000)
    parser.add_argument("--rps", type=float, default=125)
    parser.add_argument("--duration", type=parse_duration, default=parse_duration("30m"))
    parser.add_argument("--max-connections", type=int, default=200)
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--summary-interval", type=float, default=1)
    parser.add_argument("--output-dir", default="results")
    parser.add_argument("--no-live-console", action="store_true")
    args = parser.parse_args()

    asyncio.run(run(args))


if __name__ == "__main__":
    main()
