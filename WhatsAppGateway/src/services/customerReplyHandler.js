const whatsappClient = require('./whatsappClient');
const ordersStore = require('../stores/ordersStore');
const csharpApi = require('./csharpApi');
const { ORDER_STATUS, CUSTOMER_ACTION } = require('../constants');
const { toWhatsAppJid, phoneFromJid } = require('../utils/phone');
const { extractInteraction, detectAction, orderIdFromButton, isCustomerKeyword } = require('../utils/messageParser');

// لكل أكشن: الحالة الجديدة للأوردر + الرد اللي بيتبعت للعميل + رسالة اللوج
const ACTION_RULES = {
    [CUSTOMER_ACTION.CONFIRM]: {
        nextStatus: ORDER_STATUS.CONFIRMED,
        reply: () => '✅ تم تأكيد طلبك بنجاح وسيتم ترتيب الشحن',
        log: (orderId) => `✅ العميل ضغط تأكيد للطلب رقم #${orderId} وتم تحديثه على موقع EasyOrders!`
    },
    [CUSTOMER_ACTION.CANCEL]: {
        nextStatus: ORDER_STATUS.AWAITING_CANCEL_REASON,
        reply: () => '❌ تم الغاء طلبك...\nمن فضلك شاركنا سبب الإلغاء لمساعدتنا على التحسين',
        log: (orderId) => `❌ العميل ضغط إلغاء للطلب رقم #${orderId} وتم إلغاؤه على موقع EasyOrders.`
    },
    [CUSTOMER_ACTION.SUPPORT]: {
        nextStatus: ORDER_STATUS.SUPPORT,
        reply: (brand) => `📞 أهلاً بك في خدمة عملاء ${brand}، من فضلك اكتب استفسارك هنا وهيتواصل معاك فريق الدعم فوراً 🙏`,
        log: (orderId) => `📞 العميل طلب الدعم للطلب رقم #${orderId}.`
    },
    [CUSTOMER_ACTION.CANCEL_REASON]: {
        nextStatus: ORDER_STATUS.CANCELLED,
        reply: (brand) => `شكراً لمشاركتك رأيك معانا 🙏 تم تسجيل سبب الإلغاء، ونتمنى نخدمك بشكل أفضل المرة الجاية في ${brand}.`,
        log: (orderId, reason) => `📝 تم تسجيل سبب إلغاء الطلب #${orderId} داخل موقع EasyOrders: ${reason}`
    }
};

// الدالة الرئيسية: بتتنفذ مع كل رسالة جديدة توصل على الواتساب لأي تاجر
async function handleIncomingMessages(messages, tenantId = 'default') {
    for (const msg of messages) {
        try {
            await handleSingleMessage(msg, tenantId);
        } catch (err) {
            console.error('❌ خطأ أثناء معالجة رد العميل:', err.message);
        }
    }
}

async function handleSingleMessage(msg, tenantId = 'default') {
    if (!shouldHandle(msg)) return;

    const remoteJid = msg.key.remoteJid;
    const interaction = extractInteraction(msg.message);
    const { buttonId, text } = interaction;

    if (!text && !buttonId) return;
    if (msg.key.fromMe && !buttonId && !isCustomerKeyword(text)) return; // رسالة عادية من صاحب الرقم

    console.log(`📩 [التاجر: ${tenantId}] تفاعل وارد من العميل (${remoteJid}) -> النص: "${text}" | الزر: "${buttonId}"`);

    const order = ordersStore.findForCustomer({
        orderId: orderIdFromButton(buttonId),
        remoteJid,
        phone: phoneFromJid(msg.key.senderPn || msg.key.remoteJidAlt || remoteJid)
    });
    if (!order) return;

    ordersStore.update(order, { lid: remoteJid, tenantId: order.tenantId || tenantId });

    const action = detectAction(interaction) || detectCancelReason(order, msg);
    if (!action) return;

    await applyAction(order, action, remoteJid, text, tenantId);
}

// بنتجاهل: رسايل البوت نفسه + الجروبات + الاستوري + الرسايل الفاضية
function shouldHandle(msg) {
    const remoteJid = msg.key?.remoteJid || '';
    if (!remoteJid || remoteJid.endsWith('@g.us') || remoteJid === 'status@broadcast') return false;
    if (whatsappClient.isBotMessage(msg.key.id)) return false;
    return Boolean(msg.message);
}

// لو العميل لسه داس "إلغاء" والمرة دي بيكتب سبب الإلغاء
function detectCancelReason(order, msg) {
    const isWaitingForReason = order.status === ORDER_STATUS.AWAITING_CANCEL_REASON && !msg.key.fromMe;
    return isWaitingForReason ? CUSTOMER_ACTION.CANCEL_REASON : null;
}

async function applyAction(order, action, remoteJid, text, tenantId) {
    const rule = ACTION_RULES[action];
    const reason = action === CUSTOMER_ACTION.CANCEL_REASON ? text : undefined;

    const activeTenantId = order.tenantId || tenantId || 'default';
    ordersStore.update(order, { status: rule.nextStatus, ...(reason && { cancelReason: reason }), tenantId: activeTenantId });

    await csharpApi.notifyOrderAction({
        action,
        orderUuid: order.orderUuid,
        orderId: order.orderId,
        storeId: order.storeId,
        tenantId: activeTenantId,
        reason
    });

    // بنرد دايماً على رقم الموبايل نفسه (مش الـ lid) عشان التشفير مايبوظش
    const replyJid = order.phone ? toWhatsAppJid(order.phone) : remoteJid;
    const session = whatsappClient.getSession(activeTenantId);
    if (session) {
        await session.sendText(replyJid, rule.reply(session.displayBrandName(order.brandName)));
    } else {
        await whatsappClient.sendText(replyJid, rule.reply(whatsappClient.displayBrandName(order.brandName)));
    }

    console.log(rule.log(order.orderId, reason));
}

module.exports = { handleIncomingMessages };
