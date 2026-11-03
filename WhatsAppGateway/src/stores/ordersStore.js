const fs = require('fs');
const { ORDERS_FILE } = require('../config');
const { ORDER_STATUS } = require('../constants');

// كلاس مسؤول عن حفظ وقراءة الأوردرات اللي البوابة بعتت لها رسايل (orders_log.json)
class OrdersStore {
    constructor(filePath) {
        this.filePath = filePath;
        this.orders = this.#load();
    }

    exists(orderUuid) {
        return Boolean(orderUuid) && this.orders.some(order => order.orderUuid === orderUuid);
    }

    add(order) {
        this.orders.unshift(order);
        this.#save();
    }

    update(order, changes) {
        Object.assign(order, changes);
        this.#save();
    }

    // بندور على أوردر العميل: الأول برقم الأوردر اللي في الزرار، بعدين برقمه، وآخر حل أحدث أوردر مستني رد
    findForCustomer({ orderId, remoteJid, phone }) {
        return (
            (orderId && this.orders.find(order => String(order.orderId) === String(orderId))) ||
            this.orders.find(order => order.lid === remoteJid || order.phone === phone) ||
            this.orders.find(order => [ORDER_STATUS.AWAITING_CANCEL_REASON, ORDER_STATUS.PENDING].includes(order.status)) ||
            this.orders[0] ||
            null
        );
    }

    // أكبر رقم أوردر قصير (short_id) اتبعتله رسالة لمتجر معين
    getMaxShortId(storeId) {
        return this.orders
            .filter(order => order.storeId === storeId)
            .map(order => parseInt(order.orderId, 10))
            .filter(num => !isNaN(num) && num < 100000)
            .reduce((max, num) => Math.max(max, num), 0);
    }

    #load() {
        try {
            if (fs.existsSync(this.filePath)) {
                return JSON.parse(fs.readFileSync(this.filePath, 'utf8'));
            }
        } catch {}
        return [];
    }

    #save() {
        try {
            fs.writeFileSync(this.filePath, JSON.stringify(this.orders, null, 2), 'utf8');
        } catch {}
    }
}

module.exports = new OrdersStore(ORDERS_FILE);
