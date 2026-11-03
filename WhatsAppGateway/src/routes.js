const express = require('express');
const whatsappClient = require('./services/whatsappClient');
const csharpApi = require('./services/csharpApi');
const publicTunnel = require('./services/publicTunnel');
const { sendOrderConfirmation } = require('./services/orderConfirmation');
const { renderDashboard } = require('./views/dashboardView');

const router = express.Router();

// صفحة الواتساب (QR Code أو "متصل الآن")
router.get('/', async (req, res) => {
    const settings = await csharpApi.getStoreSettings();
    settings.brandName = whatsappClient.resolveBrandName(settings.brandName);

    res.send(renderDashboard({
        isConnected: whatsappClient.isConnected,
        connectedUser: whatsappClient.connectedUser,
        currentQR: whatsappClient.currentQR,
        settings,
        webhookUrl: publicTunnel.webhookUrl
    }));
});

// حفظ اسم البراند ومفتاح الـ API من نافذة الإعدادات
router.post('/save-apikey', async (req, res) => {
    const apiKey = (req.body.apiKey || '').trim();
    const brandName = (req.body.brandName || '').trim();

    if (apiKey || brandName) {
        await csharpApi.saveStoreSettings({ apiKey, brandName });
    }
    res.redirect('/');
});

// تسجيل الخروج من الواتساب (بيتشال كمان من الأجهزة المرتبطة على الموبايل)
router.all(['/logout', '/whatsapp/logout'], async (req, res) => {
    await whatsappClient.logout();
    res.redirect('/');
});

// سيرفر الـ C# بينادي هنا عشان يبعت رسالة التأكيد للعميل
router.post('/send-message', async (req, res) => {
    try {
        const { statusCode, body } = await sendOrderConfirmation(req.body);
        res.status(statusCode).json(body);
    } catch (err) {
        console.error('❌ خطأ أثناء إرسال الرسالة:', err);
        res.status(500).json({ success: false, error: err.message });
    }
});

module.exports = router;
