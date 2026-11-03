const localtunnel = require('localtunnel');
const { STATIC_SUBDOMAIN, TUNNEL_RETRY_DELAY_MS } = require('../config');

// لينك ثابت على الإنترنت بيوصل لسيرفر الـ C# (بورت 5055) عشان موقع EasyOrders يبعتله الأوردرات
class PublicTunnel {
    webhookUrl = `https://${STATIC_SUBDOMAIN}.loca.lt/api/webhook/easyorder`;

    async start() {
        try {
            const tunnel = await localtunnel({ port: 5055, local_host: '127.0.0.1', subdomain: STATIC_SUBDOMAIN });

            // لو السيرفر إدانا لينك غير الثابت، نقفله ونحاول تاني
            if (!tunnel.url.includes(STATIC_SUBDOMAIN)) {
                try { tunnel.close(); } catch {}
                return this.#retryLater();
            }

            this.webhookUrl = `${tunnel.url}/api/webhook/easyorder`;
            console.log(`🌐 لينك الـ Webhook الثابت لموقع EasyOrders جاهز:\n👉 ${this.webhookUrl}`);

            tunnel.on('close', () => this.#retryLater());
            tunnel.on('error', () => this.#retryLater());
        } catch {
            this.#retryLater();
        }
    }

    #retryLater() {
        setTimeout(() => this.start(), TUNNEL_RETRY_DELAY_MS);
    }
}

module.exports = new PublicTunnel();
