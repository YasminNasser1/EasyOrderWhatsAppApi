const { CSHARP_API_BASE } = require('../config');

// كل الاتصالات بسيرفر الـ C# (بورت 5055) في مكان واحد
async function postJson(route, body) {
    return fetch(`${CSHARP_API_BASE}${route}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
}

// إبلاغ السيرفر إن العميل ضغط زرار عشان يحدّث الأوردر على موقع EasyOrders
async function notifyOrderAction(payload) {
    try {
        const response = await postJson('/api/webhook/order-action', payload);
        const data = await response.json().catch(() => ({}));
        console.log('🔄 نتيجة تحديث موقع EasyOrders:', data);
    } catch (err) {
        console.error('❌ خطأ في إرسال التحديث للسيرفر:', err.message);
    }
}

// تحويل أوردر جديد اكتشفناه بالمزامنة التلقائية لسيرفر الـ C#
async function forwardNewOrder(orderData) {
    const response = await postJson('/api/webhook/easyorder', orderData);
    return response.ok;
}

async function getStoreSettings(tenantId = 'default') {
    try {
        const query = tenantId && tenantId !== 'default' ? `?tenantId=${encodeURIComponent(tenantId)}` : '';
        const response = await fetch(`${CSHARP_API_BASE}/api/webhook/apikey-status${query}`);
        if (response.ok) return await response.json();
    } catch {}
    return { hasKey: false, maskedKey: '', brandName: '' };
}

async function saveStoreSettings({ apiKey, brandName, tenantId = 'default' }) {
    try {
        await postJson('/api/webhook/save-apikey', { apiKey, brandName, tenantId });
    } catch {}
}

module.exports = { notifyOrderAction, forwardNewOrder, getStoreSettings, saveStoreSettings };
