import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";

const BASE_URL = (__ENV.BASE_URL || "http://192.168.1.56:30000").replace(/\/+$/, "");
const TARGET_RPS = Number(__ENV.TARGET_RPS || 3000);
const DURATION = __ENV.DURATION || "60s";
const PRE_ALLOCATED_VUS = Number(__ENV.PRE_ALLOCATED_VUS || 500);
const MAX_VUS = Number(__ENV.MAX_VUS || 5000);

const DEFAULT_KEYWORDS = ["rtx", "asus", "msi", "laptop", "ram", "ssd", "monitor", "klavye"];
const httpErrors = new Counter("http_errors");

export const options = {
  scenarios: {
    constant_rps_load: {
      executor: "constant-arrival-rate",
      rate: TARGET_RPS,
      timeUnit: "1s",
      duration: DURATION,
      preAllocatedVUs: PRE_ALLOCATED_VUS,
      maxVUs: MAX_VUS,
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1000"],
  },
  discardResponseBodies: true,
};

function normalizeProduct(raw) {
  if (!raw || !raw.id) return null;
  const images = Array.isArray(raw.imageUrls) ? raw.imageUrls : [];
  return {
    id: raw.id,
    name: raw.name || "GameGaraj Product",
    brand: raw.brand || "",
    price: Number(raw.price || 1000),
    imageUrl: raw.imageUrl || (images.length > 0 ? images[0] : ""),
  };
}

export function setup() {
  for (const path of ["/api/catalog/products/featured", "/api/catalog/products"]) {
    const res = http.get(`${BASE_URL}${path}`, {
      responseType: "text",
      timeout: "15s",
    });
    if (res.status === 200 && res.body) {
      try {
        const parsed = JSON.parse(res.body);
        const list = Array.isArray(parsed) ? parsed : (parsed.value || []);
        const products = list.map(normalizeProduct).filter(Boolean).slice(0, 200);
        if (products.length > 0) {
          return { products };
        }
      } catch (_) {}
    }
  }
  return { products: [] };
}

function pickProduct(products) {
  if (!products || products.length === 0) return null;
  return products[Math.floor(Math.random() * products.length)];
}

function searchTerm(product) {
  if (!product) {
    return DEFAULT_KEYWORDS[Math.floor(Math.random() * DEFAULT_KEYWORDS.length)];
  }
  if (product.brand) return product.brand;
  const word = (product.name || "").split(/\s+/)[0];
  return word || "rtx";
}

function basketHeaders() {
  return {
    "X-User-Id": `guest-k6-${__VU}`,
    "Content-Type": "application/json",
  };
}

function track(res, name, allowedStatuses = [200]) {
  const ok = check(res, {
    [`${name} ok`]: (r) => allowedStatuses.includes(r.status),
  });
  if (!ok) httpErrors.add(1);
}

export default function (data) {
  const products = (data && data.products) || [];
  const product = pickProduct(products);
  const roll = Math.random() * 100;

  if (roll < 20) {
    // 20%: search query
    const q = encodeURIComponent(searchTerm(product));
    const res = http.get(`${BASE_URL}/api/search?q=${q}`, {
      tags: { name: "search: /api/search?q=..." },
    });
    track(res, "search_query");
  } else if (roll < 30) {
    // 10%: search suggestions
    const q = encodeURIComponent(searchTerm(product).slice(0, 3));
    const res = http.get(`${BASE_URL}/api/search/suggestions?q=${q}`, {
      tags: { name: "search: /api/search/suggestions" },
    });
    track(res, "search_suggestions");
  } else if (roll < 35) {
    // 5%: search featured
    const res = http.get(`${BASE_URL}/api/search/products/featured`, {
      tags: { name: "search: /api/search/products/featured" },
    });
    track(res, "search_featured");
  } else if (roll < 53) {
    // 18%: catalog product detail
    if (!product) return;
    const res = http.get(`${BASE_URL}/api/catalog/products/${product.id}`, {
      tags: { name: "catalog: /api/catalog/products/{id}" },
    });
    track(res, "catalog_detail");
  } else if (roll < 63) {
    // 10%: catalog featured
    const res = http.get(`${BASE_URL}/api/catalog/products/featured`, {
      tags: { name: "catalog: /api/catalog/products/featured" },
    });
    track(res, "catalog_featured");
  } else if (roll < 70) {
    // 7%: catalog search
    const q = encodeURIComponent(searchTerm(product));
    const res = http.get(`${BASE_URL}/api/catalog/products/search?q=${q}`, {
      tags: { name: "catalog: /api/catalog/products/search" },
    });
    track(res, "catalog_search");
  } else if (roll < 82) {
    // 12%: basket get
    const res = http.get(`${BASE_URL}/api/basket/baskets`, {
      headers: basketHeaders(),
      tags: { name: "basket: GET /api/basket/baskets" },
    });
    track(res, "basket_get");
  } else if (roll < 92) {
    // 10%: basket add
    if (!product) return;
    const payload = JSON.stringify({
      Id: product.id,
      Name: product.name,
      PictureUrl: product.imageUrl,
      Price: product.price,
      Quantity: 1,
    });
    const res = http.post(`${BASE_URL}/api/basket/baskets/items`, payload, {
      headers: basketHeaders(),
      tags: { name: "basket: POST /api/basket/baskets/items" },
    });
    track(res, "basket_add", [200, 201, 204]);
  } else if (roll < 97) {
    // 5%: basket update
    if (!product) return;
    const payload = JSON.stringify({
      UserId: `guest-k6-${__VU}`,
      Items: [
        {
          Id: product.id,
          Name: product.name,
          PictureUrl: product.imageUrl,
          Price: product.price,
          Quantity: 2,
        },
      ],
    });
    const res = http.post(`${BASE_URL}/api/basket/baskets`, payload, {
      headers: basketHeaders(),
      tags: { name: "basket: POST /api/basket/baskets (update)" },
    });
    track(res, "basket_update", [200, 201, 204]);
  } else {
    // 3%: basket delete
    if (!product) return;
    const res = http.del(`${BASE_URL}/api/basket/baskets/items/${product.id}`, null, {
      headers: basketHeaders(),
      tags: { name: "basket: DELETE /api/basket/baskets/items/{id}" },
    });
    track(res, "basket_delete", [200, 204, 400, 404]);
  }
}
