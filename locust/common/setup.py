import requests
import json

def fetch_products(host):
    products = []
    add_basket_payloads = {}
    
    print(f"[Setup] Urunler cekiliyor: {host}/api/catalog/products")
    try:
        response = requests.get(f"{host}/api/catalog/products")
        if response.status_code == 200:
            data = response.json()
            items = data.get("value", data) if isinstance(data, dict) else data
            
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
                products.append(prod_dict)
                
                # CPU ISLEMINI SIFIRA INDIRMEK ICIN PRE-DUMP JSON (STRING)
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
            print(f"[Setup] Basarili! Toplam {len(products)} urun hafizaya alindi.")
        else:
            print(f"[Setup] Hata: Urunler cekilemedi. Status: {response.status_code}")
    except Exception as e:
        print(f"[Setup] Istek atilirken bir hata olustu: {e}")
        
    return products, add_basket_payloads
