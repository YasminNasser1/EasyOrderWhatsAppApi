const express = require('express');
const fs = require('fs');
const path = require('path');
const localtunnel = require('localtunnel');
const {
    default: makeWASocket,
    useMultiFileAuthState,
    DisconnectReason,
    generateWAMessageFromContent,
    proto
} = require('@whiskeysockets/baileys');
const QRCode = require('qrcode');
const pino = require('pino');

// حماية السيرفر من التوقف لو النت فصل لحظة
process.on('uncaughtException', (err) => {
    console.error('⚠️ تنبيه غير حرج (Uncaught Exception):', err.message);
});
process.on('unhandledRejection', (err) => {
    console.error('⚠️ تنبيه غير حرج (Unhandled Rejection):', err?.message || err);
});

const app = express();
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

const AUTH_DIR = path.join(__dirname, 'auth_info');
const ORDERS_FILE = path.join(__dirname, 'orders_log.json');
const STATIC_SUBDOMAIN = 'outlix-easyorder-yasmin';
const CSHARP_API_BASE = 'http://localhost:5055';

let sock = null;
let currentQR = null;
let isConnected = false;
let connectedUser = null;
let publicWebhookUrl = `https://${STATIC_SUBDOMAIN}.loca.lt/api/webhook/easyorder`;

// حفظ الرسائل المرسلة من البوت لفك تشفير الردود ومنع البوت من الرد على رسائله التلقائية
const sentMessagesStore = new Map();
const botSentIds = new Set();

// حفظ بيانات الأوردرات عشان لما العميل يضغط على الزرار في الواتساب نحدث الأوردر على موقع EasyOrders
let orders = loadOrders();

function loadOrders() {
    try {
        if (fs.existsSync(ORDERS_FILE)) {
            return JSON.parse(fs.readFileSync(ORDERS_FILE, 'utf8'));
        }
    } catch {}
    return [];
}

function saveOrders() {
    try {
        fs.writeFileSync(ORDERS_FILE, JSON.stringify(orders, null, 2), 'utf8');
    } catch {}
}

async function notifyCSharpOrderAction(payload) {
    try {
        const res = await fetch(`${CSHARP_API_BASE}/api/webhook/order-action`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const data = await res.json().catch(() => ({}));
        console.log('🔄 نتيجة تحديث موقع EasyOrders:', data);
    } catch (err) {
        console.error('❌ خطأ في إرسال التحديث للسيرفر:', err.message);
    }
}

async function sendBotTextMessage(jid, text) {
    try {
        const meJid = sock?.user?.id ? `${sock.user.id.split(':')[0].split('@')[0]}@s.whatsapp.net` : null;
        await sock.assertSessions(meJid ? [jid, meJid] : [jid], true);
    } catch {}
    const sent = await sock.sendMessage(jid, { text });
    if (sent?.key?.id) {
        botSentIds.add(sent.key.id);
        sentMessagesStore.set(sent.key.id, sent.message);
    }
    return sent;
}

// استخراج بيانات ضغطة الزرار أو النص من أي مستوى داخل كائن رسالة واتساب
function extractInteractionFromMessage(messageObj) {
    let result = { buttonId: '', text: '' };
    if (!messageObj || typeof messageObj !== 'object') return result;

    const findRecursive = (obj) => {
        if (!obj || typeof obj !== 'object') return;

        if (obj.nativeFlowResponseMessage?.paramsJson) {
            try {
                const parsed = JSON.parse(obj.nativeFlowResponseMessage.paramsJson);
                if (parsed.id) result.buttonId = parsed.id;
                if (parsed.display_text) result.text = parsed.display_text;
            } catch {}
        }

        if (obj.interactiveResponseMessage?.body?.text && !result.text) {
            result.text = obj.interactiveResponseMessage.body.text;
        }
        if (obj.buttonsResponseMessage?.selectedButtonId && !result.buttonId) {
            result.buttonId = obj.buttonsResponseMessage.selectedButtonId;
        }
        if (obj.buttonsResponseMessage?.selectedDisplayText && !result.text) {
            result.text = obj.buttonsResponseMessage.selectedDisplayText;
        }
        if (obj.templateButtonReplyMessage?.selectedId && !result.buttonId) {
            result.buttonId = obj.templateButtonReplyMessage.selectedId;
        }
        if (obj.templateButtonReplyMessage?.selectedDisplayText && !result.text) {
            result.text = obj.templateButtonReplyMessage.selectedDisplayText;
        }
        if (typeof obj.conversation === 'string' && obj.conversation.trim() && !result.text) {
            result.text = obj.conversation.trim();
        }
        if (typeof obj.extendedTextMessage?.text === 'string' && obj.extendedTextMessage.text.trim() && !result.text) {
            result.text = obj.extendedTextMessage.text.trim();
        }

        for (const key of Object.keys(obj)) {
            if (obj[key] && typeof obj[key] === 'object') {
                findRecursive(obj[key]);
            }
        }
    };

    findRecursive(messageObj);
    return result;
}

// دالة إرسال رسالة واتساب بأزرار تفاعلية قابلة للضغط (Clickable Buttons) مثل موقع Confirm-It بالظبط
async function sendConfirmItInteractiveMessage(jid, bodyText, brandName, orderId) {
    const footerText = `شكراً لثقتك في ${brandName || 'Outlix'} 🙏`;

    const buttons = [
        {
            name: 'quick_reply',
            buttonParamsJson: JSON.stringify({
                display_text: 'تأكيد الاوردر',
                id: `confirm_${orderId}`
            })
        },
        {
            name: 'quick_reply',
            buttonParamsJson: JSON.stringify({
                display_text: 'إلغاء الاوردر',
                id: `cancel_${orderId}`
            })
        },
        {
            name: 'quick_reply',
            buttonParamsJson: JSON.stringify({
                display_text: 'طلب الدعم',
                id: `support_${orderId}`
            })
        }
    ];

    const msgContent = generateWAMessageFromContent(
        jid,
        {
            viewOnceMessage: {
                message: {
                    messageContextInfo: {
                        deviceListMetadata: {},
                        deviceListMetadataVersion: 2
                    },
                    interactiveMessage: proto.Message.InteractiveMessage.create({
                        body: proto.Message.InteractiveMessage.Body.create({
                            text: bodyText
                        }),
                        footer: proto.Message.InteractiveMessage.Footer.create({
                            text: footerText
                        }),
                        header: proto.Message.InteractiveMessage.Header.create({
                            title: '',
                            hasMediaAttachment: false
                        }),
                        nativeFlowMessage: proto.Message.InteractiveMessage.NativeFlowMessage.create({
                            buttons: buttons,
                            messageParamsJson: ''
                        })
                    })
                }
            }
        },
        { userJid: sock?.user?.id }
    );

    botSentIds.add(msgContent.key.id);
    sentMessagesStore.set(msgContent.key.id, msgContent.message);

    try {
        const meJid = sock?.user?.id ? `${sock.user.id.split(':')[0].split('@')[0]}@s.whatsapp.net` : null;
        await sock.assertSessions(meJid ? [jid, meJid] : [jid], true);
    } catch {}

    await sock.relayMessage(jid, msgContent.message, {
        messageId: msgContent.key.id,
        additionalNodes: [
            {
                tag: 'biz',
                attrs: {},
                content: [
                    {
                        tag: 'interactive',
                        attrs: { type: 'native_flow', v: '1' },
                        content: [
                            {
                                tag: 'native_flow',
                                attrs: { v: '9', name: 'mixed' }
                            }
                        ]
                    }
                ]
            },
            {
                tag: 'bot',
                attrs: { biz_bot: '1' }
            }
        ]
    });
}

async function startPermanentTunnel() {
    try {
        const tunnel = await localtunnel({ port: 5055, local_host: '127.0.0.1', subdomain: STATIC_SUBDOMAIN });
        if (!tunnel.url.includes(STATIC_SUBDOMAIN)) {
            try { tunnel.close(); } catch {}
            setTimeout(startPermanentTunnel, 4000);
            return;
        }
        publicWebhookUrl = `${tunnel.url}/api/webhook/easyorder`;
        console.log('====================================================');
        console.log(`🌐 اللينك الثابت لموقع EasyOrders جاهز وشغال:`);
        console.log(`👉 ${publicWebhookUrl}`);
        console.log('====================================================');

        tunnel.on('close', () => {
            setTimeout(startPermanentTunnel, 3000);
        });

        tunnel.on('error', () => {
            setTimeout(startPermanentTunnel, 3000);
        });
    } catch (err) {
        setTimeout(startPermanentTunnel, 4000);
    }
}

// مزامنة تلقائية ذكية مع موقع EasyOrders كل 5 ثواني (عشان حتى لو الـ Tunnel علق لحظة، الأوردر يوصل فوراً 100%)
const lastCheckedShortIdByKey = new Map();
let isSyncingEasyOrders = false;

async function pollNewEasyOrders() {
    if (isSyncingEasyOrders || !isConnected) return;
    isSyncingEasyOrders = true;
    try {
        const apiKeyFile = path.join(__dirname, '..', 'easyorders_apikey.txt');
        if (!fs.existsSync(apiKeyFile)) return;
        const apiKey = fs.readFileSync(apiKeyFile, 'utf8').trim();
        if (!apiKey) return;

        // لو أول مرة نفحص المفتاح ده، نحدد آخر رقم طلب (short_id) اتسجل للمتجر ده
        if (!lastCheckedShortIdByKey.has(apiKey)) {
            const firstRes = await fetch('https://api.easy-orders.net/api/v1/external-apps/orders/short/1', {
                headers: { 'Api-Key': apiKey }
            });
            if (firstRes.ok) {
                const firstOrder = await firstRes.json();
                const storeId = firstOrder.store_id;
                const storeOrders = orders.filter(o => o.storeId === storeId);
                let maxShort = 0;
                for (const o of storeOrders) {
                    const num = parseInt(o.orderId, 10);
                    if (!isNaN(num) && num < 100000 && num > maxShort) {
                        maxShort = num;
                    }
                }
                lastCheckedShortIdByKey.set(apiKey, maxShort);
            } else {
                return;
            }
        }

        const nextShortId = (lastCheckedShortIdByKey.get(apiKey) || 0) + 1;
        const res = await fetch(`https://api.easy-orders.net/api/v1/external-apps/orders/short/${nextShortId}`, {
            headers: { 'Api-Key': apiKey }
        });

        if (res.ok) {
            const orderData = await res.json();
            if (orderData && orderData.id) {
                const alreadyProcessed = orders.some(o => o.orderUuid === orderData.id);
                if (alreadyProcessed || orderData.status !== 'pending') {
                    lastCheckedShortIdByKey.set(apiKey, nextShortId);
                } else {
                    console.log(`⚡ تم اكتشاف أوردر جديد (#${nextShortId}) تلقائياً من موقع EasyOrders! جاري إرسال رسالة التأكيد...`);
                    const csharpRes = await fetch(`${CSHARP_API_BASE}/api/webhook/easyorder`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(orderData)
                    });
                    if (csharpRes.ok) {
                        lastCheckedShortIdByKey.set(apiKey, nextShortId);
                    }
                }
            }
        }
    } catch {} finally {
        isSyncingEasyOrders = false;
    }
}

async function connectToWhatsApp() {
    const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);

    sock = makeWASocket({
        auth: state,
        logger: pino({ level: 'silent' }),
        printQRInTerminal: false,
        browser: ['EasyOrders WhatsApp System', 'Chrome', '1.0.0'],
        getMessage: async (key) => {
            return sentMessagesStore.get(key.id) || { conversation: 'EasyOrders' };
        }
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

    // استقبال ضغطات الأزرار التفاعلية وردود العميل على الواتساب وتحديث الطلب في موقع EasyOrders
    sock.ev.on('messages.upsert', async ({ messages }) => {
        for (const msg of messages) {
            if (!msg.key) continue;

            // نتجاهل فقط الرسائل اللي البوت نفسه لسه باعتها عشان ما يردش على نفسه
            if (botSentIds.has(msg.key.id)) continue;

            const remoteJid = msg.key.remoteJid || '';
            if (!remoteJid || remoteJid.endsWith('@g.us') || remoteJid === 'status@broadcast') continue;

            console.log('⚡ [حدث واتساب وارد]:', JSON.stringify({
                from: remoteJid,
                fromMe: msg.key.fromMe,
                stubType: msg.messageStubType,
                hasMessage: !!msg.message,
                messageKeys: msg.message ? Object.keys(msg.message) : []
            }));

            if (!msg.message) continue;

            const { buttonId, text } = extractInteractionFromMessage(msg.message);

            if (msg.key.fromMe && !buttonId && !['تأكيد', 'تاكيد', 'إلغاء', 'الغاء', 'تأكيد الاوردر', 'إلغاء الاوردر', 'طلب الدعم'].includes(text)) {
                continue;
            }

            if (!text && !buttonId) continue;

            const senderPhone = (
                msg.key.senderPn ||
                msg.key.remoteJidAlt ||
                remoteJid
            ).split('@')[0].replace(/\D/g, '');

            console.log(`📩 تفاعل وارد من العميل (${remoteJid}) -> النص: "${text}" | الزر: "${buttonId}"`);

            let customerOrder = null;
            if (buttonId && buttonId.includes('_')) {
                const clickedOrderId = buttonId.split('_')[1];
                customerOrder = orders.find(o => String(o.orderId) === String(clickedOrderId));
            }
            if (!customerOrder) {
                customerOrder = orders.find(o => o.lid === remoteJid || o.phone === senderPhone);
            }
            if (!customerOrder && orders.length > 0) {
                customerOrder = orders.find(o => o.status === 'awaiting_cancel_reason' || o.status === 'pending') || orders[0];
            }
            if (!customerOrder) continue;

            customerOrder.lid = remoteJid;
            saveOrders();

            const replyJid = customerOrder.phone ? `${customerOrder.phone}@s.whatsapp.net` : remoteJid;
            const normalized = text.replace(/\s+/g, ' ');

            // 1. لو العميل ضغط على زر "تأكيد الاوردر"
            if (
                buttonId.startsWith('confirm_') ||
                normalized === '1' ||
                normalized === '١' ||
                normalized.includes('تأكيد') ||
                normalized.includes('تاكيد')
            ) {
                customerOrder.status = 'confirmed';
                saveOrders();

                await notifyCSharpOrderAction({
                    action: 'confirm',
                    orderUuid: customerOrder.orderUuid,
                    orderId: customerOrder.orderId,
                    storeId: customerOrder.storeId
                });

                await sendBotTextMessage(replyJid, '✅ تم تأكيد طلبك بنجاح وسيتم ترتيب الشحن');
                console.log(`✅ العميل ضغط تأكيد للطلب رقم #${customerOrder.orderId} وتم تحديثه على موقع EasyOrders!`);
            }
            // 2. لو العميل ضغط على زر "إلغاء الاوردر"
            else if (
                buttonId.startsWith('cancel_') ||
                normalized === '2' ||
                normalized === '٢' ||
                normalized.includes('إلغاء') ||
                normalized.includes('الغاء')
            ) {
                customerOrder.status = 'awaiting_cancel_reason';
                saveOrders();

                await notifyCSharpOrderAction({
                    action: 'cancel',
                    orderUuid: customerOrder.orderUuid,
                    orderId: customerOrder.orderId,
                    storeId: customerOrder.storeId
                });

                await sendBotTextMessage(replyJid, '❌ تم الغاء طلبك...\nمن فضلك شاركنا سبب الإلغاء لمساعدتنا على التحسين');
                console.log(`❌ العميل ضغط إلغاء للطلب رقم #${customerOrder.orderId} وتم إلغاؤه على موقع EasyOrders.`);
            }
            // 3. لو العميل ضغط على زر "طلب الدعم"
            else if (
                buttonId.startsWith('support_') ||
                normalized === '3' ||
                normalized === '٣' ||
                normalized.includes('دعم')
            ) {
                customerOrder.status = 'support';
                saveOrders();

                await notifyCSharpOrderAction({
                    action: 'support',
                    orderUuid: customerOrder.orderUuid,
                    orderId: customerOrder.orderId,
                    storeId: customerOrder.storeId
                });

                await sendBotTextMessage(
                    replyJid,
                    `📞 أهلاً بك في خدمة عملاء ${customerOrder.brandName || 'Outlix'}، من فضلك اكتب استفسارك هنا وهيتواصل معاك فريق الدعم فوراً 🙏`
                );
            }
            // 4. لو العميل بيكتب سبب الإلغاء بعد ما ضغط زر "إلغاء الاوردر"
            else if (customerOrder.status === 'awaiting_cancel_reason' && !msg.key.fromMe) {
                customerOrder.status = 'cancelled';
                customerOrder.cancelReason = text;
                saveOrders();

                await notifyCSharpOrderAction({
                    action: 'cancel_reason',
                    orderUuid: customerOrder.orderUuid,
                    orderId: customerOrder.orderId,
                    storeId: customerOrder.storeId,
                    reason: text
                });

                await sendBotTextMessage(
                    replyJid,
                    `شكراً لمشاركتك رأيك معانا 🙏 تم تسجيل سبب الإلغاء، ونتمنى نخدمك بشكل أفضل المرة الجاية في ${customerOrder.brandName || 'Outlix'}.`
                );
                console.log(`📝 تم تسجيل سبب إلغاء الطلب #${customerOrder.orderId} داخل موقع EasyOrders: ${text}`);
            }
        }
    });
}

function clearAuthFolder() {
    try {
        if (fs.existsSync(AUTH_DIR)) {
            fs.rmSync(AUTH_DIR, { recursive: true, force: true });
        }
    } catch {}
}

// الصفحة البسيطة والأنيقة زي ما كانت في الأول
app.get('/', async (req, res) => {
    let hasApiKey = false;
    let maskedKey = '';
    let currentBrand = 'Outlix';
    try {
        const resp = await fetch(`${CSHARP_API_BASE}/api/webhook/apikey-status`);
        if (resp.ok) {
            const data = await resp.json();
            hasApiKey = data.hasKey;
            maskedKey = data.maskedKey || '';
            currentBrand = data.brandName || 'Outlix';
        }
    } catch {}

    res.send(`
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
    <meta charset="UTF-8">
    ${currentQR && !isConnected ? '<meta http-equiv="refresh" content="5">' : ''}
    <title>ربط واتساب متجر EasyOrders</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #f4f6f9; text-align: center; padding: 30px; }
        .card { background: white; max-width: 580px; margin: 0 auto; padding: 30px; border-radius: 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.08); }
        h1 { color: #128C7E; margin-bottom: 10px; font-size: 24px; }
        .status-ok { background: #d4edda; color: #155724; padding: 15px; border-radius: 10px; font-size: 17px; font-weight: bold; margin-top: 16px; }
        .status-wait { background: #fff3cd; color: #856404; padding: 15px; border-radius: 10px; font-size: 16px; margin-top: 16px; }
        .url-box { background: #eef6ff; border: 2px dashed #0d6efd; padding: 14px; border-radius: 10px; margin: 18px 0; direction: ltr; font-family: Consolas, monospace; font-size: 14px; color: #0a58ca; word-break: break-all; font-weight: bold; }
        .api-box { background: #f8f9fa; border: 1px solid #dee2e6; padding: 16px; border-radius: 10px; margin: 18px 0; text-align: right; }
        .api-input { width: 100%; padding: 10px; border: 1px solid #ccc; border-radius: 8px; font-size: 13px; box-sizing: border-box; margin-top: 6px; }
        .btn-save { background: #128C7E; color: white; border: none; padding: 10px 18px; border-radius: 8px; font-size: 14px; cursor: pointer; font-weight: bold; margin-top: 10px; width: 100%; }
        .btn-save:hover { background: #0e6f64; }
        img.qr { width: 260px; height: 260px; border: 4px solid #25D366; border-radius: 12px; padding: 8px; margin-top: 15px; }
        .btn-logout { background: #dc3545; color: white; border: none; padding: 12px 24px; border-radius: 8px; font-size: 15px; cursor: pointer; margin-top: 16px; font-weight: bold; }
        .btn-logout:hover { background: #bb2d3b; }
    </style>
</head>
<body>
    <div class="card">
        <h1>💬 بوابة ربط الواتساب (EasyOrders)</h1>
        
        <div style="background:#f8f9fa; padding:12px; border-radius:10px; margin-bottom:15px;">
            <div style="color:#333; font-weight:bold; margin-bottom:5px;">🌐 اللينك الثابت لموقع EasyOrders:</div>
            <div class="url-box">${publicWebhookUrl}</div>
            <small style="color:#666;">انسخي اللينك ده وحطيه في أي متجر على EasyOrders جوه خانة Webhook URL (مع الكود السري <code>123456</code>)</small>
        </div>

        <div class="api-box">
            <div style="font-weight:bold; color:#128C7E; font-size:15px;">
                ⚙️ إعدادات المتجر المربوط حالياً (الاسم + مفتاح الـ API):
            </div>
            <div style="font-size:13px; color:#555; margin-top:4px;">
                🏷️ اسم البراند الحالي في الرسالة: <b>${currentBrand}</b><br>
                ${hasApiKey
                    ? `✅ <b>مفتاح EasyOrders API مربوط وشغال!</b> (<span dir="ltr">${maskedKey}</span>)`
                    : `⚠️ لتفعيل تأكيد وإلغاء الطلب على الموقع، الصقي مفتاح <b>Public API</b> من إعدادات EasyOrders:`}
            </div>
            <form method="POST" action="/save-apikey" style="margin-top:10px;">
                <label style="font-size:12px;font-weight:bold;color:#333;">اسم المتجر / البراند (اللي بيظهر للعميل في رسالة الواتساب):</label>
                <input type="text" name="brandName" class="api-input" value="${currentBrand}" placeholder="اكتبي اسم البراند هنا..." />
                <label style="font-size:12px;font-weight:bold;color:#333;display:block;margin-top:8px;">مفتاح EasyOrders Public API Key للمتجر:</label>
                <input type="text" name="apiKey" class="api-input" dir="ltr" placeholder="${hasApiKey ? 'لو ربطتي متجر جديد الصقي مفتاحه الجديد هنا...' : 'الصقي كود EasyOrders Public API Key هنا...'}" />
                <button type="submit" class="btn-save">💾 حفظ إعدادات المتجر</button>
            </form>
        </div>

        ${isConnected ? `
            <div class="status-ok">
                ✅ الواتساب متصل وشغال بنجاح!<br>
                الرقم المربوط حالياً: <b>${connectedUser}</b>
            </div>
            <p style="color:#555; margin-top:14px; font-size:14px;">أي أوردر جديد هيتعمل على متجرك هيتبعتله رسالة واتساب بأزرار قابلة للضغط (<b>تأكيد الاوردر</b> / <b>إلغاء الاوردر</b> / <b>طلب الدعم</b>) 🎉</p>
            <form method="POST" action="/logout">
                <button type="submit" class="btn-logout">🔄 فصل الرقم الحالي وربط رقم جديد</button>
            </form>
        ` : currentQR ? `
            <div class="status-wait">
                📱 افتحي واتساب من الموبايل ⬅️ الأجهزة المرتبطة (Linked Devices) ⬅️ ربط جهاز، وامسحي الكود ده:
            </div>
            <img class="qr" src="${currentQR}" alt="WhatsApp QR Code" />
        ` : `
            <div class="status-wait">
                ⏳ جاري تجهيز كود الـ QR... الصفحة هتحدث نفسها تلقائياً خلال ثواني
            </div>
        `}
    </div>
</body>
</html>
    `);
});

app.post('/save-apikey', async (req, res) => {
    try {
        const apiKey = (req.body.apiKey || '').trim();
        const brandName = (req.body.brandName || '').trim();
        if (apiKey || brandName) {
            await fetch(`${CSHARP_API_BASE}/api/webhook/save-apikey`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ apiKey, brandName })
            });
        }
    } catch {}
    res.redirect('/');
});

app.post(['/logout', '/whatsapp/logout'], async (req, res) => {
    try {
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
    } catch {
        res.redirect('/');
    }
});

app.post('/send-message', async (req, res) => {
    try {
        const { phone, message, orderUuid, orderId, storeId, customerName, brandName } = req.body;
        if (!phone || !message) {
            return res.status(400).json({ success: false, error: 'رقم الهاتف والرسالة مطلوبين' });
        }

        // منع إرسال الرسالة مرتين لنفس الأوردر لو وصل من الـ Webhook والمزامنة التلقائية في نفس اللحظة
        if (orderUuid && orders.some(o => o.orderUuid === orderUuid)) {
            return res.json({ success: true, duplicate: true });
        }

        let cleanPhone = phone.toString().replace(/\D/g, '');
        if (cleanPhone.startsWith('01') && cleanPhone.length === 11) {
            cleanPhone = '2' + cleanPhone;
        }

        const displayOrderId = orderId || Math.floor(1000 + Math.random() * 9000).toString();

        orders.unshift({
            orderUuid: orderUuid || '',
            orderId: displayOrderId,
            storeId: storeId || '',
            customerName: customerName || 'عميل',
            phone: cleanPhone,
            lid: '',
            brandName: brandName || 'Outlix',
            status: 'pending',
            cancelReason: '',
            createdAt: new Date().toISOString()
        });
        saveOrders();

        if (!isConnected || !sock) {
            return res.status(503).json({
                success: false,
                error: 'الواتساب غير متصل بعد! افتحي http://localhost:3001 وامسحي الـ QR Code أولاً.'
            });
        }

        const jid = `${cleanPhone}@s.whatsapp.net`;
        await sendConfirmItInteractiveMessage(jid, message, brandName || 'Outlix', displayOrderId);
        console.log(`📤 تم إرسال رسالة تأكيد الطلب بأزرار تفاعلية (#${displayOrderId}) بنجاح للرقم: ${cleanPhone}`);

        return res.json({ success: true, to: cleanPhone });
    } catch (err) {
        console.error('❌ خطأ أثناء إرسال الرسالة:', err);
        return res.status(500).json({ success: false, error: err.message });
    }
});

const PORT = 3001;
app.listen(PORT, () => {
    console.log(`🚀 بوابة الواتساب شغالة على: http://localhost:${PORT}`);
    connectToWhatsApp();
    startPermanentTunnel();
    setInterval(pollNewEasyOrders, 4000);
});
