import random
from locust import FastHttpUser, task, events, between
from gevent import sleep
from common.setup import fetch_products
from common.api_tasks import (
    basket_add,
    basket_add_product,
    basket_get,
    basket_remove,
    basket_remove_product,
    basket_update_items,
    product_detail,
    product_detail_by_id,
    product_keyword_search,
    product_search,
    product_to_basket_item,
    search_term_for,
)
from scenarios.weights import get_scenario_weights

# Global RAM cache
products_cache = []
payloads_cache = {}

@events.init_command_line_parser.add_listener
def _(parser):
    parser.add_argument(
        "--scenario",
        type=str,
        env_var="LOCUST_SCENARIO",
        default="realistic_shopper",
        choices=["realistic_shopper", "standard", "black_friday", "heavy_basket"],
        include_in_web_ui=True,
        help="Test senaryosunu secin. Her senaryoda API isteklerinin agirligi (olasiligi) degisir."
    )
    parser.add_argument(
        "--start-spread",
        type=float,
        env_var="LOCUST_START_SPREAD",
        default=60.0,
        include_in_web_ui=True,
        help="Realistic shopper kullanicilarinin ilk aksiyonunu kac saniyeye yayacagini belirler."
    )

@events.test_start.add_listener
def on_test_start(environment, **kwargs):
    global products_cache, payloads_cache
    # Eger arayuzden gelmezse varsayilan host'u kullan
    host = environment.host or "https://gateway.kadiryilmaz.online"
    products_cache, payloads_cache = fetch_products(host)

class GameGarajUser(FastHttpUser):
    host = "https://gateway.kadiryilmaz.online"
    wait_time = between(2, 6)

    def on_start(self):
        self.user_id = f"locust-user-{random.randint(1, 1_000_000)}"
        self.headers = {
            "X-User-Id": self.user_id,
            "Content-Type": "application/json"
        }

        self.scenario = self.environment.parsed_options.scenario
        if self.scenario == "realistic_shopper":
            self.scenario_ops = []
            self.scenario_weights = []
        else:
            weights = get_scenario_weights(self.scenario)
            self.scenario_ops = list(weights.keys())
            self.scenario_weights = list(weights.values())
        self.first_realistic_journey = True

    def think(self, minimum=0.8, maximum=2.5):
        sleep(random.uniform(minimum, maximum))

    def realistic_shopper_journey(self):
        if self.first_realistic_journey:
            self.first_realistic_journey = False
            self.think(0, self.environment.parsed_options.start_spread)

        if len(products_cache) < 3:
            product_keyword_search(self, "gaming")
            self.think(2, 5)
            return

        selected = random.sample(products_cache, 3)
        basket_items = []

        for product in selected:
            product_keyword_search(self, search_term_for(product))
            self.think(1.2, 3.5)

            product_detail_by_id(self, product)
            self.think(2, 5)

            basket_add_product(self, product, payloads_cache)
            basket_items.append(product_to_basket_item(product))
            self.think(1, 3)

        basket_get(self)
        self.think(2, 5)

        for item in random.sample(basket_items, k=2):
            item["Quantity"] += 1

        basket_update_items(self, basket_items)
        self.think(1, 3)

        basket_get(self)
        self.think(2, 5)

        removed_product = selected[-1]
        basket_remove_product(self, removed_product)
        basket_items = [item for item in basket_items if item["Id"] != removed_product["id"]]
        self.think(1, 3)

        basket_get(self)
        self.think(2, 6)

        next_product = random.choice(products_cache)
        product_keyword_search(self, search_term_for(next_product))
        self.think(1, 3)
        product_detail_by_id(self, next_product)

    @task
    def execute_operation(self):
        if self.scenario == "realistic_shopper":
            self.realistic_shopper_journey()
            return

        # Olasiliklara/agirliklara gore rastgele islem sec
        op = random.choices(self.scenario_ops, weights=self.scenario_weights, k=1)[0]

        # Secilen islemi (common/api_tasks modulu icinden) calistir
        if op == "product_search":
            product_search(self)
        elif op == "product_detail":
            product_detail(self, products_cache)
        elif op == "basket_add":
            basket_add(self, products_cache, payloads_cache)
        elif op == "basket_remove":
            basket_remove(self, products_cache)
        elif op == "basket_get":
            basket_get(self)
