const whatsappClient = require('./whatsappClient');
const ordersStore = require('../stores/ordersStore');
const { ORDER_STATUS } = require('../constants');
const { formatEgyptianPhone } = require('../utils/phone');

// إرسال رسالة تأكيد الأوردر (بالأزرار) للعميل - بيتنادى من سيرفر الـ C#
// بيرجع { statusCode, body } عشان الـ route يرد بيه على طول
async function sendOrderConfirmation({ phone, message, orderUuid, orderId, storeId, customerName, brandName }) {
    if (!phone || !message) {
        return result(400, { success: false, error: 'رقم الهاتف والرسالة مطلوبين' });
    }

    // منع إرسال الرسالة مرتين لو الأوردر وصل من الـ Webhook والمزامنة التلقائية في نفس اللحظة
    if (ordersStore.exists(orderUuid)) {
        return result(200, { success: true, duplicate: true });
    }

    const cleanPhone = formatEgyptianPhone(phone);
    const displayOrderId = orderId || Math.floor(1000 + Math.random() * 9000).toString();
    const resolvedBrand = whatsappClient.resolveBrandName(brandName);

    ordersStore.add({
        orderUuid: orderUuid || '',
        orderId: displayOrderId,
        storeId: storeId || '',
        customerName: customerName || 'عميل',
        phone: cleanPhone,
        lid: '',
        brandName: resolvedBrand,
        status: ORDER_STATUS.PENDING,
        cancelReason: '',
        createdAt: new Date().toISOString()
    });

    if (!whatsappClient.isConnected) {
        return result(503, {
            success: false,
            error: 'الواتساب غير متصل بعد! افتحي http://localhost:3001 وامسحي الـ QR Code أولاً.'
        });
    }

    const finalMessage = addBrandToMessage(message, brandName, resolvedBrand);
    await whatsappClient.sendOrderConfirmation(cleanPhone, finalMessage, resolvedBrand, displayOrderId);
    console.log(`📤 تم إرسال رسالة تأكيد الطلب بأزرار تفاعلية (#${displayOrderId}) بنجاح للرقم: ${cleanPhone}`);

    return result(200, { success: true, to: cleanPhone });
}

// لو مفيش اسم براند في الإعدادات، بنحط اسم بروفايل الواتساب بدل كلمة "متجرنا"
function addBrandToMessage(message, savedBrandName, resolvedBrand) {
    const genericLine = 'حضرتك طلبت من متجرنا :';
    if (savedBrandName || !resolvedBrand || !message.includes(genericLine)) return message;
    return message.replace(genericLine, `حضرتك طلبت من براند ${resolvedBrand} :`);
}

function result(statusCode, body) {
    return { statusCode, body };
}

module.exports = { sendOrderConfirmation };
