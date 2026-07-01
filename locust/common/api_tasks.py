import random
import json


SEARCH_FALLBACK_TERMS = [
    "laptop",
    "monitor",
    "keyboard",
    "mouse",
    "kulaklik",
    "gaming",
]


def product_to_basket_item(product, quantity=1):
    return {
        "Id": product["id"],
        "Name": product["name"],
        "CategoryId": product.get("categoryId") or "",
        "Price": product.get("price", 0),
        "PictureUrl": product.get("pictureUrl") or "",
        "ProductSlug": product.get("productSlug") or "",
        "Quantity": quantity,
        "Brand": product.get("brand") or ""
    }


def search_term_for(product):
    brand = (product.get("brand") or "").strip()
    if brand:
        return brand

    words = (product.get("name") or "").split()
    if words:
        return words[0]

    return random.choice(SEARCH_FALLBACK_TERMS)

def product_search(user):
    user.client.get("/api/catalog/products", name="product_search")

def product_keyword_search(user, keyword):
    user.client.get(
        "/api/catalog/products/search",
        params={"q": keyword},
        name="product_search_keyword"
    )

def product_detail(user, products):
    if not products: return
    product = random.choice(products)
    user.client.get(f"/api/catalog/products/{product['id']}", name="product_detail")

def product_detail_by_id(user, product):
    user.client.get(f"/api/catalog/products/{product['id']}", name="product_detail")

def basket_add(user, products, payloads):
    if not products: return
    product = random.choice(products)
    payload_str = payloads[product["id"]]
    user.client.post("/api/basket/baskets/items", data=payload_str, headers=user.headers, name="basket_add")

def basket_add_product(user, product, payloads):
    payload_str = payloads.get(product["id"]) or json.dumps(product_to_basket_item(product))
    user.client.post("/api/basket/baskets/items", data=payload_str, headers=user.headers, name="basket_add")

def basket_remove(user, products):
    if not products: return
    product = random.choice(products)
    # Eger sepette olmayan bir urunu silmeye calisirsa API 400/404 doner. 
    # Bunlari gercek sunucu hatasi sanmamak icin tabloya basarili olarak isliyoruz.
    with user.client.delete(f"/api/basket/baskets/items/{product['id']}", headers=user.headers, name="basket_remove", catch_response=True) as response:
        if response.status_code in [200, 204, 400, 404]:
            response.success()

def basket_get(user):
    user.client.get("/api/basket/baskets", headers=user.headers, name="basket_get")

def basket_update_items(user, items):
    payload = json.dumps({
        "UserId": user.user_id,
        "Items": items
    })
    user.client.post("/api/basket/baskets", data=payload, headers=user.headers, name="basket_update_quantities")

def basket_remove_product(user, product):
    with user.client.delete(f"/api/basket/baskets/items/{product['id']}", headers=user.headers, name="basket_remove", catch_response=True) as response:
        if response.status_code in [200, 204, 400, 404]:
            response.success()
