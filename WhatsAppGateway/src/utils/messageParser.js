const { CUSTOMER_ACTION } = require('../constants');

// الكلمات اللي لو صاحب الرقم كتبها بنفسه نعتبرها رد عميل (للتجربة من نفس الموبايل)
const CUSTOMER_KEYWORDS = ['تأكيد', 'تاكيد', 'إلغاء', 'الغاء', 'تأكيد الاوردر', 'إلغاء الاوردر', 'طلب الدعم'];

// استخراج الزرار اللي العميل ضغط عليه أو النص اللي كتبه من أي مستوى جوه رسالة الواتساب
function extractInteraction(messageObj) {
    const result = { buttonId: '', text: '' };

    const setIfEmpty = (key, value) => {
        if (!result[key] && typeof value === 'string' && value.trim()) {
            result[key] = value.trim();
        }
    };

    const search = (obj) => {
        if (!obj || typeof obj !== 'object') return;

        const nativeFlowParams = obj.nativeFlowResponseMessage?.paramsJson;
        if (nativeFlowParams) {
            try {
                const parsed = JSON.parse(nativeFlowParams);
                setIfEmpty('buttonId', parsed.id);
                setIfEmpty('text', parsed.display_text);
            } catch {}
        }

        setIfEmpty('text', obj.interactiveResponseMessage?.body?.text);
        setIfEmpty('buttonId', obj.buttonsResponseMessage?.selectedButtonId);
        setIfEmpty('text', obj.buttonsResponseMessage?.selectedDisplayText);
        setIfEmpty('buttonId', obj.templateButtonReplyMessage?.selectedId);
        setIfEmpty('text', obj.templateButtonReplyMessage?.selectedDisplayText);
        setIfEmpty('text', obj.conversation);
        setIfEmpty('text', obj.extendedTextMessage?.text);

        Object.values(obj).forEach(search);
    };

    search(messageObj);
    return result;
}

// تحديد العميل عايز إيه: تأكيد / إلغاء / دعم (أو null لو مش واحدة منهم)
function detectAction({ buttonId, text }) {
    const normalized = text.replace(/\s+/g, ' ');

    const matches = (prefix, numbers, words) =>
        buttonId.startsWith(prefix) ||
        numbers.includes(normalized) ||
        words.some(word => normalized.includes(word));

    if (matches('confirm_', ['1', '١'], ['تأكيد', 'تاكيد'])) return CUSTOMER_ACTION.CONFIRM;
    if (matches('cancel_', ['2', '٢'], ['إلغاء', 'الغاء'])) return CUSTOMER_ACTION.CANCEL;
    if (matches('support_', ['3', '٣'], ['دعم'])) return CUSTOMER_ACTION.SUPPORT;
    return null;
}

// رقم الأوردر متخزن جوه الـ id بتاع الزرار (مثال: confirm_15 -> 15)
function orderIdFromButton(buttonId) {
    return buttonId.includes('_') ? buttonId.split('_')[1] : null;
}

function isCustomerKeyword(text) {
    return CUSTOMER_KEYWORDS.includes(text);
}

module.exports = { extractInteraction, detectAction, orderIdFromButton, isCustomerKeyword };
