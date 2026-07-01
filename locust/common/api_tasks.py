import random

def product_search(user):
    user.client.get("/api/catalog/products", name="product_search")

def product_detail(user, products):
    if not products: return
    product = random.choice(products)
    user.client.get(f"/api/catalog/products/{product['id']}", name="product_detail")

def basket_add(user, products, payloads):
    if not products: return
    product = random.choice(products)
    payload_str = payloads[product["id"]]
    user.client.post("/api/basket/baskets/items", data=payload_str, headers=user.headers, name="basket_add")

def basket_remove(user, products):
    if not products: return
    product = random.choice(products)
    user.client.delete(f"/api/basket/baskets/items/{product['id']}", headers=user.headers, name="basket_remove")

def basket_get(user):
    user.client.get("/api/basket/baskets", headers=user.headers, name="basket_get")
