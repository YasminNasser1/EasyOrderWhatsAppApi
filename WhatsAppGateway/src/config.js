// كل الإعدادات الثابتة للبوابة في مكان واحد بدل ما تبقى متفرقة جوه الكود
const path = require('path');

const GATEWAY_DIR = path.join(__dirname, '..');
const PROJECT_DIR = path.join(GATEWAY_DIR, '..');

module.exports = {
    PORT: 3001,
    CSHARP_API_BASE: 'http://localhost:5055',
    EASYORDERS_API_BASE: 'https://api.easy-orders.net/api/v1/external-apps',
    STATIC_SUBDOMAIN: 'outlix-easyorder-yasmin',

    AUTH_DIR: path.join(GATEWAY_DIR, 'auth_info'),
    getAuthDir: (tenantId = 'default') => path.join(GATEWAY_DIR, 'auth_info', String(tenantId).trim() || 'default'),
    ORDERS_FILE: path.join(GATEWAY_DIR, 'orders_log.json'),
    API_KEY_FILE: path.join(PROJECT_DIR, 'easyorders_apikey.txt'), // نفس الملف اللي سيرفر الـ C# بيحفظه

    POLL_INTERVAL_MS: 4000,
    RECONNECT_DELAY_MS: 2000,
    TUNNEL_RETRY_DELAY_MS: 4000,

    DEFAULT_BRAND_NAME: 'متجرنا'
};
