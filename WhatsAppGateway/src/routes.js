const express = require('express');
const whatsappClient = require('./services/whatsappClient');
const csharpApi = require('./services/csharpApi');
const publicTunnel = require('./services/publicTunnel');
const { sendOrderConfirmation } = require('./services/orderConfirmation');
const { renderDashboard } = require('./views/dashboardView');

const router = express.Router();

function getMerchantWebhookUrl(tenantId) {
    if (!publicTunnel.webhookUrl) return '';
    const base = publicTunnel.webhookUrl.replace(/\/api\/webhook\/easyorder\/?$/, '');
    return tenantId && tenantId !== 'default'
        ? `${base}/api/webhook/order/${tenantId}`
        : publicTunnel.webhookUrl;
}

// دالة مشتركة لتجهيز وعرض لوحة التحكم لأي تاجر
async function handleDashboardView(req, res, tenantId = 'default') {
    const session = whatsappClient.getSession(tenantId);
    const settings = await csharpApi.getStoreSettings(tenantId);
    settings.brandName = session.resolveBrandName(settings.brandName);

    res.send(renderDashboard({
        isConnected: session.isConnected,
        connectedUser: session.connectedUser,
        currentQR: session.currentQR,
        settings,
        webhookUrl: getMerchantWebhookUrl(tenantId),
        tenantId
    }));
}

// صفحة الواتساب الرئيسية (للتاجر الافتراضي default)
router.get(['/', '/whatsapp'], async (req, res) => {
    await handleDashboardView(req, res, 'default');
});

// حفظ اسم البراند ومفتاح الـ API من نافذة الإعدادات (لكل تاجر على حدة)
router.post('/save-apikey', async (req, res) => {
    const apiKey = (req.body.apiKey || '').trim();
    const brandName = (req.body.brandName || '').trim();
    const tenantId = (req.body.tenantId || 'default').trim();

    if (apiKey || brandName) {
        await csharpApi.saveStoreSettings({ apiKey, brandName, tenantId });
    }

    res.redirect(tenantId === 'default' ? '/' : `/${tenantId}`);
});

// تسجيل الخروج للتاجر الافتراضي
router.all(['/logout', '/whatsapp/logout'], async (req, res) => {
    await whatsappClient.logout('default');
    res.redirect('/');
});

// تسجيل الخروج لتاجر معين
router.all(['/logout/:tenantId', '/whatsapp/logout/:tenantId'], async (req, res) => {
    const tenantId = req.params.tenantId || 'default';
    await whatsappClient.logout(tenantId);
    res.redirect(tenantId === 'default' ? '/' : `/${tenantId}`);
});

// سيرفر الـ C# بينادي هنا عشان يبعت رسالة التأكيد للعميل (مع الـ tenantId)
router.post('/send-message', async (req, res) => {
    try {
        const { statusCode, body } = await sendOrderConfirmation(req.body);
        res.status(statusCode).json(body);
    } catch (err) {
        console.error('❌ خطأ أثناء إرسال الرسالة:', err);
        res.status(500).json({ success: false, error: err.message });
    }
});

// لوحة تحكم مستقلة لكل تاجر بالرابط بتاعه: /whatsapp/:tenantId أو /:tenantId
router.get(['/whatsapp/:tenantId', '/:tenantId'], async (req, res, next) => {
    const tenantId = req.params.tenantId;
    const reservedRoutes = ['save-apikey', 'logout', 'send-message', 'favicon.ico', 'whatsapp'];
    if (reservedRoutes.includes(tenantId)) {
        return next();
    }
    await handleDashboardView(req, res, tenantId);
});

module.exports = router;
