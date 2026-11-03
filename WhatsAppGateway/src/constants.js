// الحالات اللي الأوردر بيمر بيها جوه البوابة
const ORDER_STATUS = Object.freeze({
    PENDING: 'pending',
    CONFIRMED: 'confirmed',
    AWAITING_CANCEL_REASON: 'awaiting_cancel_reason',
    CANCELLED: 'cancelled',
    SUPPORT: 'support'
});

// الأكشنز اللي بنبعتها لسيرفر الـ C# (لازم تكون نفس الأسماء اللي في CustomerActions.cs)
const CUSTOMER_ACTION = Object.freeze({
    CONFIRM: 'confirm',
    CANCEL: 'cancel',
    CANCEL_REASON: 'cancel_reason',
    SUPPORT: 'support'
});

module.exports = { ORDER_STATUS, CUSTOMER_ACTION };
