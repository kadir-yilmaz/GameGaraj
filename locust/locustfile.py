import random
from locust import FastHttpUser, task, events, between
from common.setup import fetch_products
from common.api_tasks import product_search, product_detail, basket_add, basket_remove, basket_get
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
        default="standard",
        choices=["standard", "black_friday", "heavy_basket"],
        include_in_web_ui=True,
        help="Test senaryosunu secin. Her senaryoda API isteklerinin agirligi (olasiligi) degisir."
    )

@events.test_start.add_listener
def on_test_start(environment, **kwargs):
    global products_cache, payloads_cache
    # Eger arayuzden gelmezse varsayilan host'u kullan
    host = environment.host or "https://gateway.kadiryilmaz.online"
    products_cache, payloads_cache = fetch_products(host)

class GameGarajUser(FastHttpUser):
    host = "https://gateway.kadiryilmaz.online"
    # CPU'yu rahatlatmak icin kullanicilara 0.5 ile 1.5 saniye arasi nefes alma (uyuma) payi verdik
    wait_time = between(0.5, 1.5)

    def on_start(self):
        # 1-1000 arasinda sabit kullanici havuzu
        self.user_id = f"locust-user-{random.randint(1, 1000)}"
        self.headers = {
            "X-User-Id": self.user_id,
            "Content-Type": "application/json"
        }
        
        # CPU TASARRUFU: Senaryo agirliklari her saniye degil, kullanici dogarken sadece 1 kez hesaplanir.
        scenario = self.environment.parsed_options.scenario
        weights = get_scenario_weights(scenario)
        self.scenario_ops = list(weights.keys())
        self.scenario_weights = list(weights.values())

    @task
    def execute_operation(self):
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
