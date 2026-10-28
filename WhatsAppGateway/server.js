const express = require('express');
const fs = require('fs');
const path = require('path');
const localtunnel = require('localtunnel');
const { default: makeWASocket, useMultiFileAuthState, DisconnectReason } = require('@whiskeysockets/baileys');
const QRCode = require('qrcode');
const pino = require('pino');

const app = express();
app.use(express.json());

const AUTH_DIR = path.join(__dirname, 'auth_info');
const STATIC_SUBDOMAIN = 'outlix-easyorder-yasmin';

let sock = null;
let currentQR = null;
let isConnected = false;
let connectedUser = null;
let publicWebhookUrl = `https://${STATIC_SUBDOMAIN}.loca.lt/api/webhook/easyorder`;

// تشغيل اللينك الثابت أوتوماتيك وربطه ببورت الـ C# (5055)
async function startPermanentTunnel() {
    try {
        const tunnel = await localtunnel({ port: 5055, subdomain: STATIC_SUBDOMAIN });
        publicWebhookUrl = `${tunnel.url}/api/webhook/easyorder`;
        console.log('====================================================');
        console.log(`🌐 اللينك الثابت لموقع EasyOrders جاهز وشغال:`);
        console.log(`👉 ${publicWebhookUrl}`);
        console.log('====================================================');

        tunnel.on('close', () => {
            console.log('⚠️ انقطع اللينك الثابت، جاري إعادة فتحه تلقائياً...');
            setTimeout(startPermanentTunnel, 3000);
        });

        tunnel.on('error', (err) => {
            console.log('⚠️ جاري تجديد اتصال اللينك الثابت...', err.message);
            setTimeout(startPermanentTunnel, 3000);
        });
    } catch (err) {
        console.error('⚠️ فشل فتح اللينك الثابت، جاري المحاولة مرة أخرى خلال 5 ثواني:', err.message);
        setTimeout(startPermanentTunnel, 5000);
    }
}

async function connectToWhatsApp() {
    const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);

    sock = makeWASocket({
        auth: state,
        logger: pino({ level: 'silent' }),
        printQRInTerminal: false,
        browser: ['EasyOrders WhatsApp System', 'Chrome', '1.0.0']
    });

    sock.ev.on('creds.update', saveCreds);

    sock.ev.on('connection.update', async (update) => {
        const { connection, lastDisconnect, qr } = update;

        if (qr) {
            currentQR = await QRCode.toDataURL(qr);
            isConnected = false;
            console.log('📷 تم توليد QR Code جديد! افتحي http://localhost:3001 لمسحه بموبايلك.');
        }

        if (connection === 'close') {
            isConnected = false;
            currentQR = null;
            const statusCode = lastDisconnect?.error?.output?.statusCode;
            const shouldReconnect = statusCode !== DisconnectReason.loggedOut;
            console.log('⚠️ انقطع الاتصال بالواتساب، جاري إعادة المحاولة...', shouldReconnect);
            if (shouldReconnect) {
                setTimeout(connectToWhatsApp, 2000);
            } else {
                clearAuthFolder();
                setTimeout(connectToWhatsApp, 2000);
            }
        } else if (connection === 'open') {
            isConnected = true;
            currentQR = null;
            connectedUser = sock.user?.id?.split(':')[0] || 'متصل';
            console.log(`✅ تم ربط الواتساب بنجاح! الرقم المتصل: ${connectedUser}`);
        }
    });
}

function clearAuthFolder() {
    try {
        if (fs.existsSync(AUTH_DIR)) {
            fs.rmSync(AUTH_DIR, { recursive: true, force: true });
        }
    } catch (e) {
        console.error('Error clearing auth folder:', e.message);
    }
}

// صفحة الويب لعرض الـ QR Code وحالة الاتصال واللينك الثابت
app.get('/', (req, res) => {
    res.send(`
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="refresh" content="5">
    <title>بوابة واتساب المجانية - EasyOrders C#</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #f0f2f5; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; }
        .card { background: white; padding: 32px; border-radius: 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.1); text-align: center; max-width: 540px; width: 92%; }
        h2 { color: #128C7E; margin-top: 0; }
        .status-ok { background: #d4edda; color: #155724; padding: 15px; border-radius: 10px; font-weight: bold; font-size: 18px; margin: 20px 0; }
        .status-wait { background: #fff3cd; color: #856404; padding: 12px; border-radius: 10px; margin: 15px 0; }
        .url-box { background: #eef6ff; border: 1px dashed #0d6efd; padding: 12px; border-radius: 8px; margin: 15px 0; direction: ltr; font-family: Consolas, monospace; font-size: 14px; color: #0a58ca; word-break: break-all; }
        img.qr { width: 260px; height: 260px; border: 4px solid #25D366; border-radius: 12px; padding: 8px; margin: 10px 0; }
        ol { text-align: right; line-height: 1.8; color: #444; }
        .btn-logout { background: #dc3545; color: white; border: none; padding: 10px 20px; border-radius: 8px; font-size: 15px; cursor: pointer; font-weight: bold; margin-top: 12px; }
        .btn-logout:hover { background: #c82333; }
        .footer { font-size: 13px; color: #777; margin-top: 16px; }
    </style>
</head>
<body>
    <div class="card">
        <h2>💬 بوابة واتساب المجانية (C# Webhook)</h2>
        <div><b>🔗 اللينك الثابت لموقع EasyOrders:</b></div>
        <div class="url-box">${publicWebhookUrl}</div>
        ${isConnected ? `
            <div class="status-ok">
                ✅ الواتساب متصل وشغال بنجاح!<br>
                <small>الرقم المربوط حالياً: ${connectedUser}</small>
            </div>
            <p>أي أوردر جديد هييجي من موقع EasyOrders هيتبعت للعميل تلقائياً من الرقم ده.</p>
            <form method="POST" action="/whatsapp/logout">
                <button type="submit" class="btn-logout">🔄 فصل الرقم الحالي وربط رقم تاني</button>
            </form>
        ` : currentQR ? `
            <div class="status-wait">📱 امسحي الـ QR Code ده من الرقم اللي عايزة تربطيه:</div>
            <img class="qr" src="${currentQR}" alt="WhatsApp QR Code" />
            <ol>
                <li>افتحي <b>واتساب</b> على التليفون.</li>
                <li>اضغطي على النقط التلاتة (أو الإعدادات) ⬅ <b>الأجهزة المرتبطة (Linked Devices)</b>.</li>
                <li>اضغطي <b>ربط جهاز (Link a Device)</b> ووجهي الكاميرا للكود فوق.</li>
            </ol>
        ` : `
            <div class="status-wait">⏳ جاري تجهيز الاتصال... (الصفحة بتحدث نفسها تلقائياً كل 5 ثواني)</div>
        `}
        <div class="footer">تحديث تلقائي كل 5 ثواني • يعمل محلياً مع مشروع EasyOrderWhatsAppApi</div>
    </div>
</body>
</html>
    `);
});

app.post(['/logout', '/whatsapp/logout'], async (req, res) => {
    try {
        console.log('🔄 جاري فصل الرقم الحالي لتجهيز QR Code لرقم جديد...');
        isConnected = false;
        currentQR = null;
        connectedUser = null;
        if (sock) {
            try { await sock.logout(); } catch {}
            try { sock.end(); } catch {}
            sock = null;
        }
        clearAuthFolder();
        setTimeout(connectToWhatsApp, 1500);
        res.redirect('/');
    } catch (err) {
        res.redirect('/');
    }
});

app.post('/send-message', async (req, res) => {
    try {
        const { phone, message } = req.body;
        if (!phone || !message) {
            return res.status(400).json({ success: false, error: 'رقم الهاتف والرسالة مطلوبين' });
        }

        if (!isConnected || !sock) {
            return res.status(503).json({
                success: false,
                error: 'الواتساب غير متصل بعد! افتحي http://localhost:3001 وامسحي الـ QR Code أولاً.'
            });
        }

        let cleanPhone = phone.toString().replace(/\D/g, '');
        if (cleanPhone.startsWith('01') && cleanPhone.length === 11) {
            cleanPhone = '2' + cleanPhone;
        }
        const jid = `${cleanPhone}@s.whatsapp.net`;

        await sock.sendMessage(jid, { text: message });
        console.log(`📤 تم إرسال رسالة واتساب بنجاح للرقم: ${cleanPhone}`);

        return res.json({ success: true, to: cleanPhone });
    } catch (err) {
        console.error('❌ خطأ أثناء إرسال الرسالة:', err);
        return res.status(500).json({ success: false, error: err.message });
    }
});

const PORT = 3001;
app.listen(PORT, () => {
    console.log(`🚀 بوابة الواتساب المجانية شغالة على: http://localhost:${PORT}`);
    connectToWhatsApp();
    startPermanentTunnel();
});
