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
    const dynamicBrand = brandName || (sock?.user?.name && sock.user.name !== '~' ? sock.user.name : 'متجرنا');
    const footerText = `شكراً لثقتك في ${dynamicBrand} 🙏`;

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

                const activeBrand = customerOrder.brandName || (sock?.user?.name && sock.user.name !== '~' ? sock.user.name : 'متجرنا');
                await sendBotTextMessage(
                    replyJid,
                    `📞 أهلاً بك في خدمة عملاء ${activeBrand}، من فضلك اكتب استفسارك هنا وهيتواصل معاك فريق الدعم فوراً 🙏`
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

                const activeBrand = customerOrder.brandName || (sock?.user?.name && sock.user.name !== '~' ? sock.user.name : 'متجرنا');
                await sendBotTextMessage(
                    replyJid,
                    `شكراً لمشاركتك رأيك معانا 🙏 تم تسجيل سبب الإلغاء، ونتمنى نخدمك بشكل أفضل المرة الجاية في ${activeBrand}.`
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

// تصميم مطابق لـ WhatsApp Web العادي بدون تفاصيل كثيرة ظاهرة
app.get('/', async (req, res) => {
    let hasApiKey = false;
    let maskedKey = '';
    let currentBrand = '';
    try {
        const resp = await fetch(`${CSHARP_API_BASE}/api/webhook/apikey-status`);
        if (resp.ok) {
            const data = await resp.json();
            hasApiKey = data.hasKey;
            maskedKey = data.maskedKey || '';
            currentBrand = data.brandName || (sock?.user?.name && sock.user.name !== '~' ? sock.user.name : '');
        }
    } catch {}

    res.send(`
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>WhatsApp Web</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Segoe UI', 'Helvetica Neue', Helvetica, Arial, sans-serif;
            background-color: #eae6df;
            color: #41525d;
            min-height: 100vh;
            position: relative;
        }
        /* الشريط الأخضر العلوي الخاص بـ WhatsApp Web */
        .wa-header {
            background-color: #00a884;
            height: 222px;
            width: 100%;
            position: absolute;
            top: 0;
            left: 0;
            z-index: 1;
        }
        .wa-header-inner {
            position: relative;
            z-index: 10;
            max-width: 1000px;
            margin: 0 auto;
            padding: 28px 20px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            direction: ltr;
        }
        .wa-brand {
            display: flex;
            align-items: center;
            gap: 14px;
            color: #ffffff;
            font-size: 14px;
            font-weight: 600;
            letter-spacing: 0.5px;
        }
        .wa-settings-btn {
            position: relative;
            z-index: 20;
            background: rgba(255,255,255,0.18);
            color: #fff;
            border: none;
            padding: 7px 14px;
            border-radius: 20px;
            font-size: 13px;
            cursor: pointer;
            font-family: inherit;
        }
        .wa-settings-btn:hover { background: rgba(255,255,255,0.28); }
        /* الكارت الأبيض الرئيسي مثل WhatsApp Web */
        .wa-main {
            position: relative;
            z-index: 2;
            max-width: 960px;
            margin: 0 auto;
            padding: 95px 20px 40px;
            pointer-events: none;
        }
        .wa-card {
            pointer-events: auto;
            background: #ffffff;
            border-radius: 3px;
            box-shadow: 0 17px 50px 0 rgba(11,20,26,.19), 0 12px 15px 0 rgba(11,20,26,.24);
            padding: 58px 60px;
            min-height: 440px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 48px;
        }
        .wa-instructions {
            flex: 1;
        }
        .wa-title {
            font-size: 28px;
            font-weight: 300;
            color: #41525d;
            margin-bottom: 36px;
        }
        .wa-steps {
            list-style: none;
            font-size: 18px;
            line-height: 28px;
            color: #3b4a54;
        }
        .wa-steps li {
            margin-bottom: 18px;
        }
        .wa-qr-box {
            width: 264px;
            height: 264px;
            display: flex;
            align-items: center;
            justify-content: center;
            position: relative;
            flex-shrink: 0;
        }
        .wa-qr-box img {
            width: 264px;
            height: 264px;
            display: block;
        }
        .wa-spinner {
            width: 48px;
            height: 48px;
            border: 4px solid #e9edef;
            border-top-color: #00a884;
            border-radius: 50%;
            animation: spin 0.9s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }
        /* شاشة الاتصال النشط */
        .wa-connected {
            width: 100%;
            text-align: center;
            padding: 30px 0;
        }
        .wa-connected-icon {
            width: 72px;
            height: 72px;
            background: #00a884;
            color: #fff;
            border-radius: 50%;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            font-size: 34px;
            margin-bottom: 20px;
        }
        .wa-connected h2 {
            font-size: 26px;
            font-weight: 400;
            color: #41525d;
            margin-bottom: 10px;
        }
        .wa-connected p {
            font-size: 16px;
            color: #667781;
            margin-bottom: 28px;
        }
        .wa-logout-btn {
            background: transparent;
            color: #d93025;
            border: 1px solid #d93025;
            padding: 10px 28px;
            border-radius: 24px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s;
        }
        .wa-logout-btn:hover {
            background: #d93025;
            color: #fff;
        }
        /* نافذة الإعدادات المخفية (تظهر عند الضغط على زر الإعدادات فقط) */
        .modal-backdrop {
            display: none;
            position: fixed;
            inset: 0;
            background: rgba(11,20,26,0.5);
            z-index: 100;
            align-items: center;
            justify-content: center;
        }
        .modal-backdrop.active { display: flex; }
        .modal-box {
            background: #fff;
            width: 90%;
            max-width: 460px;
            border-radius: 8px;
            padding: 24px;
            box-shadow: 0 12px 30px rgba(0,0,0,0.2);
        }
        .modal-box h3 { color: #00a884; margin-bottom: 14px; font-size: 18px; }
        .modal-box label { display: block; font-size: 13px; color: #3b4a54; margin-top: 10px; font-weight: 600; }
        .modal-box input {
            width: 100%;
            padding: 9px 12px;
            border: 1px solid #d1d7db;
            border-radius: 6px;
            margin-top: 5px;
            font-size: 13px;
        }
        .modal-actions {
            display: flex;
            gap: 10px;
            margin-top: 18px;
        }
        .btn-primary {
            flex: 1;
            background: #00a884;
            color: #fff;
            border: none;
            padding: 10px;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 600;
        }
        .btn-close {
            background: #e9edef;
            color: #41525d;
            border: none;
            padding: 10px 16px;
            border-radius: 6px;
            cursor: pointer;
        }
        @media (max-width: 768px) {
            .wa-card { flex-direction: column-reverse; padding: 32px 24px; text-align: center; }
        }
    </style>
</head>
<body>
    <div class="wa-header">
        <div class="wa-header-inner">
            <div class="wa-brand">
                <svg viewBox="0 0 39 39" width="39" height="39">
                    <path fill="#00E676" d="M10.7 32.8l.6.3c2.5 1.5 5.3 2.2 8.1 2.2 8.8 0 16-7.2 16-16 0-4.2-1.7-8.3-4.7-11.3s-7-4.7-11.3-4.7c-8.8 0-16 7.2-15.9 16.1 0 3 .9 5.9 2.4 8.4l.4.6-1.6 5.9 6-1.5z"></path>
                    <path fill="#FFF" d="M32.4 6.4C29 2.9 24.3 1 19.5 1 9.3 1 1.1 9.3 1.2 19.4c0 3.2.9 6.3 2.4 9.1L1 38l9.7-2.5c2.7 1.5 5.7 2.2 8.7 2.2 10.1 0 18.3-8.3 18.3-18.4 0-4.9-1.9-9.5-5.3-12.9zM19.5 34.6c-2.7 0-5.4-.7-7.7-2.1l-.6-.3-5.8 1.5L6.9 28l-.4-.6c-4.4-7.1-2.3-16.5 4.9-20.9s16.5-2.3 20.9 4.9 2.3 16.5-4.9 20.9c-2.3 1.5-5.1 2.3-7.9 2.3zm8.8-11.1l-1.1-.5s-1.6-.7-2.6-1.2c-.1 0-.2-.1-.3-.1-.3 0-.5.1-.7.2 0 0-.1.1-1.5 1.7-.1.2-.3.3-.5.3h-.1c-.1 0-.3-.1-.4-.2l-.5-.2c-1.1-.5-2.1-1.1-2.9-1.9-.2-.2-.5-.4-.7-.6-.7-.7-1.4-1.5-1.9-2.4l-.1-.2c-.1-.1-.1-.2-.2-.4 0-.2 0-.4.1-.5 0 0 .4-.5.7-.8.2-.2.3-.5.5-.7.2-.3.3-.7.2-1-.1-.5-1.3-3.2-1.6-3.8-.2-.3-.4-.4-.7-.5h-1.1c-.2 0-.4.1-.6.1l-.1.1c-.2.1-.4.3-.6.4-.2.2-.3.4-.5.6-.7.9-1.1 2-1.1 3.1 0 .8.2 1.6.5 2.3l.1.3c.9 1.9 2.1 3.6 3.7 5.1l.4.4c.3.3.6.5.8.8 2.1 1.8 4.5 3.1 7.2 3.8.3.1.7.1 1 .2h1c.5 0 1.1-.2 1.5-.4.3-.2.5-.2.7-.4l.2-.2c.2-.2.4-.3.6-.5s.4-.4.5-.6c.2-.4.3-.9.4-1.4v-.7s-.1-.1-.3-.2z"></path>
                </svg>
                <span>WHATSAPP WEB</span>
            </div>
            <button class="wa-settings-btn" onclick="document.getElementById('settingsModal').classList.add('active')">⚙️ الإعدادات</button>
        </div>
    </div>

    <div class="wa-main">
        <div class="wa-card">
            ${isConnected ? `
                <div class="wa-connected">
                    <div class="wa-connected-icon">✓</div>
                    <h2>واتساب متصل الآن</h2>
                    <p dir="ltr">+${connectedUser}</p>
                    <form method="POST" action="/logout">
                        <button type="submit" class="wa-logout-btn">تسجيل الخروج</button>
                    </form>
                </div>
            ` : `
                <div class="wa-instructions">
                    <div class="wa-title">استخدام واتساب على الكمبيوتر</div>
                    <ol class="wa-steps">
                        <li>١. افتح واتساب على هاتفك</li>
                        <li>٢. اضغط على <strong>القائمة</strong> أو <strong>الإعدادات</strong> وحدد <strong>الأجهزة المرتبطة</strong></li>
                        <li>٣. اضغط على <strong>ربط جهاز</strong> ووجّه هاتفك إلى هذه الشاشة لمسح الرمز</li>
                    </ol>
                </div>
                <div class="wa-qr-box">
                    ${currentQR
                        ? `<img src="${currentQR}" alt="QR Code" />`
                        : `<div class="wa-spinner"></div>`
                    }
                </div>
            `}
        </div>
    </div>

    <div id="settingsModal" class="modal-backdrop">
        <div class="modal-box">
            <h3>إعدادات المتجر</h3>
            <form method="POST" action="/save-apikey">
                <label>اسم البراند:</label>
                <input type="text" name="brandName" value="${currentBrand}" />
                <label>مفتاح EasyOrders Public API Key ${hasApiKey ? `(${maskedKey})` : ''}:</label>
                <input type="text" name="apiKey" dir="ltr" placeholder="API Key..." />
                <label>لينك الـ Webhook:</label>
                <input type="text" dir="ltr" readonly value="${publicWebhookUrl}" onclick="this.select()" />
                <div class="modal-actions">
                    <button type="submit" class="btn-primary">حفظ</button>
                    <button type="button" class="btn-close" onclick="document.getElementById('settingsModal').classList.remove('active')">إغلاق</button>
                </div>
            </form>
        </div>
    </div>
    ${!isConnected ? `
    <script>
        setInterval(function () {
            var modal = document.getElementById('settingsModal');
            if (!modal || !modal.classList.contains('active')) {
                window.location.reload();
            }
        }, 4000);
    </script>
    ` : ''}
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

app.all(['/logout', '/whatsapp/logout'], async (req, res) => {
    try {
        isConnected = false;
        currentQR = null;
        connectedUser = null;
        if (sock) {
            const activeSock = sock;
            sock = null;
            try { await activeSock.logout(); } catch {}
            try { activeSock.end(); } catch {}
        } else {
            clearAuthFolder();
            setTimeout(connectToWhatsApp, 1500);
        }
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

        const resolvedBrand = brandName || (sock?.user?.name && sock.user.name !== '~' ? sock.user.name : '');
        let finalMessage = message;
        if (!brandName && resolvedBrand && finalMessage.includes('حضرتك طلبت من متجرنا :')) {
            finalMessage = finalMessage.replace('حضرتك طلبت من متجرنا :', `حضرتك طلبت من براند ${resolvedBrand} :`);
        }

        orders.unshift({
            orderUuid: orderUuid || '',
            orderId: displayOrderId,
            storeId: storeId || '',
            customerName: customerName || 'عميل',
            phone: cleanPhone,
            lid: '',
            brandName: resolvedBrand,
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
        await sendConfirmItInteractiveMessage(jid, finalMessage, resolvedBrand, displayOrderId);
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
