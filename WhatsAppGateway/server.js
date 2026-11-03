// ============================================================
//  بوابة الواتساب المجانية (WhatsApp Gateway)
//  الملف ده بس بيشغّل الأجزاء - كل جزء في ملف لوحده جوه فولدر src:
//    src/config.js                      -> الإعدادات الثابتة
//    src/constants.js                   -> حالات الأوردر والأكشنز
//    src/routes.js                      -> الـ API والصفحة
//    src/services/whatsappClient.js     -> الاتصال بالواتساب والإرسال
//    src/services/customerReplyHandler.js -> التعامل مع ضغطات أزرار العميل
//    src/services/orderConfirmation.js  -> إرسال رسالة تأكيد الأوردر
//    src/services/easyOrdersPoller.js   -> المزامنة التلقائية مع EasyOrders
//    src/services/publicTunnel.js       -> لينك الـ Webhook الثابت
//    src/services/csharpApi.js          -> الاتصال بسيرفر الـ C#
//    src/stores/ordersStore.js          -> حفظ الأوردرات (orders_log.json)
//    src/views/                         -> تصميم صفحة WhatsApp Web
// ============================================================
const express = require('express');
const { PORT } = require('./src/config');
const routes = require('./src/routes');
const whatsappClient = require('./src/services/whatsappClient');
const easyOrdersPoller = require('./src/services/easyOrdersPoller');
const publicTunnel = require('./src/services/publicTunnel');
const { handleIncomingMessages } = require('./src/services/customerReplyHandler');

// حماية السيرفر من التوقف لو النت فصل لحظة
process.on('uncaughtException', (err) => console.error('⚠️ تنبيه غير حرج (Uncaught Exception):', err.message));
process.on('unhandledRejection', (err) => console.error('⚠️ تنبيه غير حرج (Unhandled Rejection):', err?.message || err));

const app = express();
app.use(express.json());
app.use(express.urlencoded({ extended: true }));
app.use(routes);

app.listen(PORT, () => {
    console.log(`🚀 بوابة الواتساب شغالة على: http://localhost:${PORT}`);

    whatsappClient.onMessages(handleIncomingMessages);
    whatsappClient.connect();
    publicTunnel.start();
    easyOrdersPoller.start();
});
