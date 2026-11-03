const fs = require('fs');
const whatsappClient = require('./whatsappClient');
const ordersStore = require('../stores/ordersStore');
const csharpApi = require('./csharpApi');
const { API_KEY_FILE, EASYORDERS_API_BASE, POLL_INTERVAL_MS } = require('../config');
const { ORDER_STATUS } = require('../constants');

// مزامنة تلقائية مع موقع EasyOrders: بنسأل كل كام ثانية "فيه أوردر رقمه الجاي؟"
// عشان حتى لو الـ Webhook (الـ Tunnel) علق لحظة، الأوردر يوصل برضه
class EasyOrdersPoller {
    #lastShortIdByApiKey = new Map();
    #isPolling = false;

    start() {
        setInterval(() => this.#poll(), POLL_INTERVAL_MS);
    }

    async #poll() {
        if (this.#isPolling || !whatsappClient.isConnected) return;
        this.#isPolling = true;

        try {
            const apiKey = this.#readApiKey();
            if (!apiKey) return;

            const lastShortId = await this.#getLastShortId(apiKey);
            if (lastShortId === null) return;

            const nextShortId = lastShortId + 1;
            const order = await this.#fetchOrderByShortId(nextShortId, apiKey);
            if (!order?.id) return;

            const isNewPendingOrder = !ordersStore.exists(order.id) && order.status === ORDER_STATUS.PENDING;
            if (isNewPendingOrder) {
                console.log(`⚡ تم اكتشاف أوردر جديد (#${nextShortId}) تلقائياً من موقع EasyOrders! جاري إرسال رسالة التأكيد...`);
                const isForwarded = await csharpApi.forwardNewOrder(order);
                if (!isForwarded) return; // نجرب تاني في المرة الجاية
            }

            this.#lastShortIdByApiKey.set(apiKey, nextShortId);
        } catch {
        } finally {
            this.#isPolling = false;
        }
    }

    // أول مرة نشوف المفتاح ده: نبدأ من آخر أوردر احنا بعتناله رسالة للمتجر ده
    async #getLastShortId(apiKey) {
        if (this.#lastShortIdByApiKey.has(apiKey)) {
            return this.#lastShortIdByApiKey.get(apiKey);
        }

        const firstOrder = await this.#fetchOrderByShortId(1, apiKey);
        if (!firstOrder) return null;

        const lastShortId = ordersStore.getMaxShortId(firstOrder.store_id);
        this.#lastShortIdByApiKey.set(apiKey, lastShortId);
        return lastShortId;
    }

    async #fetchOrderByShortId(shortId, apiKey) {
        const response = await fetch(`${EASYORDERS_API_BASE}/orders/short/${shortId}`, {
            headers: { 'Api-Key': apiKey }
        });
        return response.ok ? response.json() : null;
    }

    #readApiKey() {
        return fs.existsSync(API_KEY_FILE) ? fs.readFileSync(API_KEY_FILE, 'utf8').trim() : '';
    }
}

module.exports = new EasyOrdersPoller();
