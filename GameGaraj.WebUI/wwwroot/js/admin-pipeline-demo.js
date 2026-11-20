let currentStep = 0;
let isRunning = false;
let fullPipelineStartTime = 0;
let baselineCatalog = null;
let demoEventCount = 0;
let liveEventCount = 0;

const testOrderData = {
    productId: '',
    productName: '',
    pictureUrl: '/default.jpg',
    quantity: 1,
    totalPrice: 15500,
    orderId: 0,
    cardNumber: '5528790000000008',
    cardName: 'Kadir Yilmaz'
};

const steps = [
    { key: 'stock_validate', name: 'Catalog Stok Doğrulama', node: 'node-catalog', pipe: 'pipe-1' },
    { key: 'order_create', name: 'Order DB & Outbox', node: 'node-order', pipe: 'pipe-2' },
    { key: 'payment_process', name: 'İyzico Ödeme İşlemi', node: 'node-payment', pipe: 'pipe-3' },
    { key: 'rabbitmq_events', name: 'RabbitMQ Dağıtımı & Downstream', node: 'node-rabbitmq', pipe: 'pipe-4' }
];

document.addEventListener('DOMContentLoaded', () => {
    const scenarioKey = document.getElementById('scenario-root')?.dataset.scenario || 'normal';
    applyPreset(scenarioKey);
});

function onCatalogProductChanged() {
    const select = document.getElementById('select-catalog-product');
    const customPanel = document.getElementById('custom-product-panel');
    if (!select) return;

    if (select.value === 'custom') {
        customPanel?.classList.remove('d-none');
        syncCustomProduct();
    } else {
        customPanel?.classList.add('d-none');
        const opt = select.options[select.selectedIndex];
        testOrderData.productId = select.value;
        testOrderData.productName = opt.getAttribute('data-name');
        testOrderData.totalPrice = parseFloat(opt.getAttribute('data-price')) || 15500;
        testOrderData.pictureUrl = opt.getAttribute('data-img') || '/default.jpg';
        resetPipelineUI();
    }
}

function syncCustomProduct() {
    testOrderData.productId = document.getElementById('custom-prod-id')?.value.trim() || '00000000-0000-0000-0000-000000000000';
    testOrderData.productName = document.getElementById('custom-prod-name')?.value.trim() || 'Silinmiş Test Ürünü';
    testOrderData.totalPrice = parseFloat(document.getElementById('custom-prod-price')?.value) || 5000;
    testOrderData.pictureUrl = '/default.jpg';

    resetPipelineUI(false);
    const catVal = document.getElementById('db-catalog-values');
    const catDelta = document.getElementById('db-catalog-delta');
    if (catVal) catVal.innerText = 'Özel GUID seçildi (DB kaydı yok)';
    if (catDelta) {
        catDelta.className = 'badge bg-secondary extra-small';
        catDelta.innerText = 'Özel';
    }
}

function updateDemoCard() {
    const cardSelect = document.getElementById('select-card-quick');
    if (!cardSelect) return;
    testOrderData.cardNumber = cardSelect.value;
    const label = testOrderData.cardNumber === '5528790000000001'
        ? 'Yetersiz Bakiye Kartı (Compensate / Rollback Testi)'
        : 'Geçerli Kart';
    logMessage('Kart', `Kart seçildi: ${label}`, 'info');
}

function stripEmojis(str) {
    return (str || '').replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\u{FE0F}]/gu, '').trim();
}

function colorFor(type, fallback) {
    if (type === 'success') return 'text-success';
    if (type === 'error') return 'text-danger';
    if (type === 'warning') return 'text-warning';
    if (type === 'info') return 'text-info';
    return fallback;
}

function appendLog(consoleId, sender, text, type, fallbackColor) {
    const consoleEl = document.getElementById(consoleId);
    if (!consoleEl) return false;
    const timestamp = new Date().toLocaleTimeString();
    const line = document.createElement('div');
    line.innerHTML = `<span class="text-secondary">[${timestamp}]</span> <span class="fw-bold ${colorFor(type, fallbackColor)}">[${stripEmojis(sender)}]</span> ${stripEmojis(text)}`;
    consoleEl.appendChild(line);
    consoleEl.scrollTop = consoleEl.scrollHeight;
    return true;
}

function logDemoMessage(sender, text, type = 'info') {
    if (!appendLog('log-console-demo', sender, text, type, 'text-info')) return;
    demoEventCount++;
    const el = document.getElementById('badge-demo-count');
    if (el) el.innerText = demoEventCount;
}

function logLiveMessage(sender, text, type = 'info') {
    if (!appendLog('log-console-live', sender, text, type, 'text-success')) return;
    liveEventCount++;
    const el = document.getElementById('badge-live-count');
    if (el) el.innerText = liveEventCount;
}

function logMessage(sender, text, type = 'info') {
    logDemoMessage(sender, text, type);
}

function clearActiveConsole() {
    if (document.getElementById('tab-sim')?.classList.contains('active')) {
        document.getElementById('log-console-demo').innerHTML = '';
        demoEventCount = 0;
        document.getElementById('badge-demo-count').innerText = '0';
    } else {
        document.getElementById('log-console-live').innerHTML = '';
        liveEventCount = 0;
        document.getElementById('badge-live-count').innerText = '0';
    }
}

async function refreshDbSnapshot(logChange = false, setAsBaseline = false) {
    if (!testOrderData.productId) return;
    try {
        const url = `/Admin/OrderPipelineDemo/GetDbSnapshot?productId=${encodeURIComponent(testOrderData.productId)}&orderId=${testOrderData.orderId || 0}`;
        const res = await fetch(url);
        if (!res.ok || res.redirected) return;
        const data = await res.json();

        const catValEl = document.getElementById('db-catalog-values');
        const catDeltaEl = document.getElementById('db-catalog-delta');

        if (data.catalog && catValEl && catDeltaEl) {
            if (data.catalog.available) {
                const c = data.catalog;
                if (setAsBaseline || !baselineCatalog || baselineCatalog.productId !== c.productId) {
                    baselineCatalog = { productId: c.productId, stock: c.stock, reservedStock: c.reservedStock };
                }

                catValEl.innerHTML = `Stok: <b>${c.stock}</b> &bull; Rezerve: <b>${c.reservedStock}</b> &bull; Kullanılabilir: <b>${c.availableStock}</b>`;

                const dStock = c.stock - baselineCatalog.stock;
                const dReserve = c.reservedStock - baselineCatalog.reservedStock;

                if (dReserve > 0) {
                    catDeltaEl.className = 'badge bg-warning text-dark extra-small';
                    catDeltaEl.innerText = `Rezerve +${dReserve}`;
                } else if (dStock < 0) {
                    catDeltaEl.className = 'badge bg-success extra-small';
                    catDeltaEl.innerText = `Stok Düştü (${dStock})`;
                } else if (currentStep > 0 || testOrderData.orderId > 0) {
                    catDeltaEl.className = 'badge bg-info text-dark extra-small';
                    catDeltaEl.innerText = 'Değişmedi / İade Edildi';
                } else {
                    catDeltaEl.className = 'badge bg-secondary extra-small';
                    catDeltaEl.innerText = 'Başlangıç';
                }

                if (logChange) {
                    logMessage('DB Catalog', `Stok=${c.stock}, Rezerve=${c.reservedStock}, Kullanılabilir=${c.availableStock}`, 'info');
                }
            } else {
                catValEl.innerText = data.catalog.reason || 'Okunamadı';
                catDeltaEl.className = 'badge bg-danger extra-small';
                catDeltaEl.innerText = 'Erişilemiyor';
            }
        }

        const ordValEl = document.getElementById('db-order-values');
        const ordBadgeEl = document.getElementById('db-order-badge');

        if (ordValEl && ordBadgeEl) {
            if (testOrderData.orderId > 0 && data.order) {
                if (data.order.available) {
                    const o = data.order;
                    ordValEl.innerHTML = `Sipariş <b>#${o.orderId}</b> &bull; Tutar: <b>₺${Number(o.totalPaidAmount).toLocaleString('tr-TR')}</b>`;
                    ordBadgeEl.innerText = o.statusText;
                    if (o.status === 1) ordBadgeEl.className = 'badge bg-success extra-small';
                    else if (o.status === 2) ordBadgeEl.className = 'badge bg-danger extra-small';
                    else if (o.status === 0) ordBadgeEl.className = 'badge bg-warning text-dark extra-small';
                    else ordBadgeEl.className = 'badge bg-info extra-small';

                    if (logChange) {
                        logMessage('DB Order', `Sipariş #${o.orderId} statü: ${o.statusText}`, o.status === 2 ? 'warning' : 'info');
                    }
                } else {
                    ordValEl.innerText = data.order.reason;
                    ordBadgeEl.className = 'badge bg-secondary extra-small';
                    ordBadgeEl.innerText = 'Bulunamadı';
                }
            } else if (!testOrderData.orderId) {
                ordValEl.innerText = 'Henüz sipariş oluşturulmadı';
                ordBadgeEl.className = 'badge bg-secondary extra-small';
                ordBadgeEl.innerText = 'Bekliyor';
            }
        }
    } catch { }
}

function setServiceRadio(service, val) {
    const radio = document.getElementById(`radio-${service}-${val}`);
    if (radio) radio.checked = true;
}

async function updateServiceChaosRadio(service, val) {
    const rule = {
        serviceName: service,
        enabled: val !== 'normal',
        alwaysFail: val === 'blocked',
        failUntilAttempt: val === 'retry' ? 3 : 0,
        latencyMs: val === 'latency' ? 3000 : 0,
        blockPipeline: val === 'blocked'
    };

    try {
        const res = await fetch('/Admin/OrderPipelineDemo/SetRule', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(rule)
        });

        if (!res.ok || res.redirected) {
            if (res.status === 401 || res.status === 403 || res.redirected) {
                logMessage('ChaosManager', 'Oturum süresi dolmuş olabilir. Sayfayı yenileyin.', 'warning');
                return;
            }
            throw new Error(`HTTP ${res.status}`);
        }

        const modeLabel = {
            normal: 'Aktif',
            retry: 'Polly Retry (ilk 2 deneme 503, 3. başarılı)',
            latency: '3sn Gecikme',
            blocked: 'Uykuda (503)'
        }[val];
        const logType = val === 'normal' ? 'success' : (val === 'blocked' ? 'error' : 'warning');
        logMessage('ChaosManager', `${service.toUpperCase()} -> ${modeLabel}`, logType);
    } catch (err) {
        logMessage('ChaosManager', `Kural güncellenemedi: ${err.message || err}`, 'error');
    }
}

function selectFirstInStockProduct() {
    const select = document.getElementById('select-catalog-product');
    if (!select) return;
    for (let i = 0; i < select.options.length; i++) {
        const stock = parseInt(select.options[i].getAttribute('data-stock') || '0', 10);
        if (select.options[i].value !== 'custom' && stock > 0) {
            select.selectedIndex = i;
            break;
        }
    }
    onCatalogProductChanged();
}

async function applyPreset(preset) {
    await resetAllChaos(false);
    const cardSelect = document.getElementById('select-card-quick');
    if (cardSelect) cardSelect.value = '5528790000000008';
    testOrderData.cardNumber = '5528790000000008';

    if (preset === 'normal' || preset === 'custom') {
        selectFirstInStockProduct();
        logMessage('Senaryo', preset === 'custom'
            ? 'Serbest Chaos Lab hazır. Vanaları manuel ayarlayıp çalıştırabilirsiniz.'
            : 'Normal Akış hazır: tüm servisler sağlıklı.', 'success');
    } else if (preset === 'polly_retry') {
        selectFirstInStockProduct();
        setServiceRadio('catalog', 'retry');
        await updateServiceChaosRadio('catalog', 'retry');
        logMessage('Senaryo', 'Polly Retry hazır: Catalog.API ilk 2 istekte 503 dönecek, Polly 3. denemede kurtaracak.', 'warning');
    } else if (preset === 'latency') {
        selectFirstInStockProduct();
        setServiceRadio('catalog', 'latency');
        await updateServiceChaosRadio('catalog', 'latency');
        logMessage('Senaryo', '3sn Gecikme hazır: Catalog.API her isteğe 3 sn gecikme ekleyecek.', 'info');
    } else if (preset === 'nostock') {
        const select = document.getElementById('select-catalog-product');
        let found = false;
        if (select) {
            for (let i = 0; i < select.options.length; i++) {
                if (select.options[i].value !== 'custom' && select.options[i].getAttribute('data-stock') === '0') {
                    select.selectedIndex = i;
                    found = true;
                    break;
                }
            }
            if (!found) select.value = 'custom';
        }
        onCatalogProductChanged();
        logMessage('Senaryo', 'Stok Yok Koruması hazır: 0 stoklu ürün seçildi.', 'warning');
    } else if (preset === 'compensate') {
        selectFirstInStockProduct();
        if (cardSelect) cardSelect.value = '5528790000000001';
        testOrderData.cardNumber = '5528790000000001';
        logMessage('Senaryo', 'Compensate hazır: Yetersiz bakiye kartı seçildi. Ödeme reddedilince stok iade edilip sipariş Failed olacak.', 'warning');
    }
}

async function resetAllChaos(log = true) {
    ['catalog', 'order', 'payment', 'invoice', 'notification', 'campaign'].forEach(s => setServiceRadio(s, 'normal'));
    try {
        await fetch('/Admin/OrderPipelineDemo/ResetAllRules', { method: 'POST' });
        if (log) logMessage('ChaosManager', 'Tüm vanalar normale döndürüldü.', 'success');
    } catch { }
}

function resetPipelineUI(refreshDb = true) {
    currentStep = 0;
    isRunning = false;
    fullPipelineStartTime = 0;
    testOrderData.orderId = 0;
    baselineCatalog = null;

    setStatus('bg-secondary', 'Beklemede');

    for (let i = 1; i <= 5; i++) {
        const tube = document.querySelector(`#pipe-${i} .tube-fluid`);
        if (tube) tube.className = 'tube-fluid';
    }

    ['node-catalog', 'node-order', 'node-payment', 'node-rabbitmq', 'node-downstream'].forEach(n => {
        const nodeEl = document.getElementById(n);
        if (!nodeEl) return;
        nodeEl.querySelector('.node-icon').className = 'node-icon bg-dark border border-secondary text-secondary shadow';
        const badge = nodeEl.querySelector('.node-badge span');
        badge.className = 'badge bg-secondary';
        badge.innerText = 'Bekliyor';
    });

    const webuiIcon = document.querySelector('#node-webui .node-icon');
    if (webuiIcon) webuiIcon.className = 'node-icon bg-primary text-white shadow';
    const webuiBadge = document.querySelector('#node-webui .node-badge span');
    if (webuiBadge) {
        webuiBadge.className = 'badge bg-success';
        webuiBadge.innerText = 'Hazır';
    }

    if (refreshDb) refreshDbSnapshot(false, true);
}

function setStatus(cls, text) {
    const el = document.getElementById('pipeline-status-text');
    if (!el) return;
    el.className = `badge ${cls} px-3 py-1`;
    el.innerText = text;
}

async function stepPipeline() {
    if (currentStep >= steps.length) {
        logMessage('Pipeline', 'Akış zaten tamamlandı. Yeniden başlatmak için sıfırlayın.', 'success');
        return false;
    }

    if (currentStep === 0 && fullPipelineStartTime === 0) {
        fullPipelineStartTime = performance.now();
        await refreshDbSnapshot(false, true);
    }

    const step = steps[currentStep];
    const pipeEl = document.querySelector(`#${step.pipe} .tube-fluid`);
    const nodeEl = document.getElementById(step.node);
    const iconEl = nodeEl.querySelector('.node-icon');
    const badgeEl = nodeEl.querySelector('.node-badge span');

    pipeEl?.classList.add('flowing');
    iconEl?.classList.add('active');
    badgeEl.className = 'badge bg-info';
    badgeEl.innerText = 'İşleniyor...';
    setStatus('bg-primary', `Adım ${currentStep + 1}/4`);

    logMessage('Pipeline', `[Adım ${currentStep + 1}/4] ${step.name} çağrılıyor...`, 'info');

    try {
        const res = await fetch('/Admin/OrderPipelineDemo/ExecuteStep', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                stepKey: step.key,
                stepName: step.name,
                productId: testOrderData.productId,
                productName: testOrderData.productName,
                pictureUrl: testOrderData.pictureUrl,
                quantity: testOrderData.quantity,
                totalPrice: testOrderData.totalPrice,
                orderId: testOrderData.orderId,
                cardNumber: testOrderData.cardNumber,
                cardName: testOrderData.cardName
            })
        });

        if (!res.ok || res.redirected) {
            if (res.status === 401 || res.status === 403 || res.redirected) {
                logMessage('Pipeline', 'Oturum süresi dolmuş olabilir. Sayfayı yenileyin.', 'warning');
                return false;
            }
            throw new Error(`HTTP ${res.status}`);
        }

        const result = await res.json();

        if (result.success) {
            iconEl.className = 'node-icon success text-white';
            badgeEl.className = 'badge bg-success';
            badgeEl.innerText = `OK (${result.durationMs}ms)`;
            logMessage('Pipeline', `${step.name}: ${result.message} (${result.durationMs}ms)`, 'success');

            if (step.key === 'order_create' && result.payload && result.payload.orderId) {
                testOrderData.orderId = result.payload.orderId;
                setTimeout(() => refreshDbSnapshot(true, false), 900);
            }

            currentStep++;
            await refreshDbSnapshot(true, false);

            if (currentStep === steps.length) {
                const finalNode = document.getElementById('node-downstream');
                finalNode.querySelector('.node-icon').className = 'node-icon success text-white';
                const finalBadge = finalNode.querySelector('.node-badge span');
                finalBadge.className = 'badge bg-success';
                finalBadge.innerText = 'Tamamlandı';
                document.querySelector('#pipe-5 .tube-fluid')?.classList.add('flowing');

                setStatus('bg-success', 'Başarılı');
                const totalTimeSec = ((performance.now() - fullPipelineStartTime) / 1000).toFixed(2);
                logMessage('Saga', `Sipariş akışı tamamlandı. Toplam süre: ${totalTimeSec} sn. Downstream kontrol ediliyor...`, 'success');
                fullPipelineStartTime = 0;

                setTimeout(() => refreshDbSnapshot(true, false), 1500);
            }
            return true;
        }

        pipeEl.className = 'tube-fluid blocked';
        iconEl.className = 'node-icon failed text-white';
        badgeEl.className = 'badge bg-danger';
        badgeEl.innerText = `Hata (${result.statusCode})`;
        logMessage('Pipeline', `${step.name} durdu: ${result.message}`, 'error');
        setStatus('bg-danger', 'Hata / Durduruldu');

        if (step.key === 'stock_validate') {
            logMessage('Saga Koruması', 'Stok doğrulanamadığı için akış durduruldu. Ödeme adımına geçilmedi.', 'warning');
        } else if (step.key === 'payment_process') {
            logMessage('Compensate', 'PaymentFailed eventi yayınlandı. Rezerve stok iadesi ve sipariş statüsü kontrol ediliyor...', 'warning');
            setTimeout(() => refreshDbSnapshot(true, false), 1500);
            setTimeout(() => refreshDbSnapshot(true, false), 4000);
        }
        await refreshDbSnapshot(false, false);
        fullPipelineStartTime = 0;
        return false;
    } catch (err) {
        logMessage('Pipeline', `İstek hatası: ${err}`, 'error');
        setStatus('bg-danger', 'İstek Hatası');
        fullPipelineStartTime = 0;
        return false;
    }
}

async function startFullPipeline() {
    if (isRunning) return;
    resetPipelineUI(false);
    isRunning = true;
    logMessage('Pipeline', `Otomatik akış başlatıldı: ${testOrderData.productName}`, 'info');

    while (currentStep < steps.length) {
        const executedKey = steps[currentStep].key;
        const success = await stepPipeline();
        if (!success) break;
        const delayMs = executedKey === 'order_create' ? 1300 : 700;
        await new Promise(r => setTimeout(r, delayMs));
    }

    isRunning = false;
}

if (typeof signalR !== 'undefined') {
    const signalrConnection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/pipeline")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    signalrConnection.on("ReceivePipelineEvent", (data) => {
        const sender = data.sender || 'Gerçek Site';
        const msg = data.message || '';
        const type = data.type || 'info';

        if (sender.toLowerCase().includes('polly') || msg.includes('Polly Retry')) {
            logDemoMessage('Polly', msg, type);
        } else {
            logLiveMessage(sender, msg, type);
        }
    });

    signalrConnection.start()
        .then(() => {
            document.getElementById('signalr-dot')?.classList.add('connected');
            logLiveMessage('SignalR', 'Bağlantı aktif. Gerçek site siparişleri dinleniyor.', 'success');
        })
        .catch(err => {
            const dot = document.getElementById('signalr-dot');
            if (dot) dot.className = 'pulse-dot bg-danger';
            logLiveMessage('SignalR', 'Bağlantı kurulamadı: ' + err, 'error');
        });
}
