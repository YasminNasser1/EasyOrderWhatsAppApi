// تنسيق رقم الموبايل المصري عشان الواتساب يقبله (مثال: 011xxxxxxxx -> 2011xxxxxxxx)
function formatEgyptianPhone(phone) {
    const cleanPhone = String(phone || '').replace(/\D/g, '');
    const isLocalEgyptianNumber = cleanPhone.startsWith('01') && cleanPhone.length === 11;
    return isLocalEgyptianNumber ? '2' + cleanPhone : cleanPhone;
}

// تحويل الرقم لعنوان واتساب (JID)
function toWhatsAppJid(phone) {
    return `${phone}@s.whatsapp.net`;
}

// استخراج الرقم من أي عنوان واتساب (مثال: 2011xxxx@s.whatsapp.net -> 2011xxxx)
function phoneFromJid(jid) {
    return String(jid || '').split('@')[0].replace(/\D/g, '');
}

module.exports = { formatEgyptianPhone, toWhatsAppJid, phoneFromJid };
