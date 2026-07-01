import requests
import json
import random


PRODUCT_CACHE_LIMIT = 200


def _normalize_products(items):
    products = []
    add_basket_payloads = {}

    for p in items:
        picture_url = p.get("firstImageUrl")
        if not picture_url and p.get("imageUrls"):
            picture_url = p["imageUrls"][0]

        prod_dict = {
            "id": p.get("id"),
            "name": p.get("name"),
            "categoryId": p.get("categoryId", ""),
            "price": p.get("price", 0),
            "pictureUrl": picture_url or "",
            "productSlug": p.get("slug", ""),
            "brand": p.get("brand", "")
        }

        if not prod_dict["id"] or not prod_dict["name"]:
            continue

        products.append(prod_dict)

        add_basket_payloads[prod_dict["id"]] = json.dumps({
            "Id": prod_dict["id"],
            "Name": prod_dict["name"],
            "CategoryId": prod_dict["categoryId"],
            "Price": prod_dict["price"],
            "PictureUrl": prod_dict["pictureUrl"],
            "ProductSlug": prod_dict["productSlug"],
            "Quantity": 1,
            "Brand": prod_dict["brand"]
        })

    random.shuffle(products)
    products = products[:PRODUCT_CACHE_LIMIT]
    add_basket_payloads = {p["id"]: add_basket_payloads[p["id"]] for p in products}
    return products, add_basket_payloads


def _fetch_json(url):
    response = requests.get(url, timeout=20)
    if response.status_code != 200:
        print(f"[Setup] Hata: {url} status={response.status_code}")
        return []

    data = response.json()
    return data.get("value", data) if isinstance(data, dict) else data

def fetch_products(host):
    print(f"[Setup] Urun havuzu hazirlaniyor: {host}")
    try:
        items = _fetch_json(f"{host}/api/catalog/products/featured")

        if len(items) < 3:
            items = _fetch_json(f"{host}/api/catalog/products")

        products, add_basket_payloads = _normalize_products(items)
        print(f"[Setup] Basarili! Toplam {len(products)} urun hafizaya alindi.")
        return products, add_basket_payloads
    except Exception as e:
        print(f"[Setup] Istek atilirken bir hata olustu: {e}")

    return [], {}
