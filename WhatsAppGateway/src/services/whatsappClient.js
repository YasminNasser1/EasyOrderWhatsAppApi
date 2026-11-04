const fs = require('fs');
const pino = require('pino');
const QRCode = require('qrcode');
const {
    default: makeWASocket,
    useMultiFileAuthState,
    DisconnectReason,
    generateWAMessageFromContent,
    proto
} = require('@whiskeysockets/baileys');
const { getAuthDir, RECONNECT_DELAY_MS, DEFAULT_BRAND_NAME } = require('../config');
const { toWhatsAppJid } = require('../utils/phone');

// الأزرار التفاعلية اللي بتظهر تحت رسالة التأكيد
const CONFIRMATION_BUTTONS = [
    { text: 'تأكيد الاوردر', idPrefix: 'confirm' },
    { text: 'إلغاء الاوردر', idPrefix: 'cancel' },
    { text: 'طلب الدعم', idPrefix: 'support' }
];

const INTERACTIVE_ADDITIONAL_NODES = [
    {
        tag: 'biz',
        attrs: {},
        content: [{
            tag: 'interactive',
            attrs: { type: 'native_flow', v: '1' },
            content: [{ tag: 'native_flow', attrs: { v: '9', name: 'mixed' } }]
        }]
    },
    { tag: 'bot', attrs: { biz_bot: '1' } }
];

// كلاس يمثل جلسة واتساب مستقلة لتاجر واحد
class WhatsAppSession {
    #sock = null;
    #sentMessages = new Map();
    #messageHandler = null;

    constructor(tenantId, onMessagesCallback) {
        this.tenantId = tenantId;
        this.authDir = getAuthDir(tenantId);
        this.#messageHandler = onMessagesCallback;

        this.currentQR = null;
        this.isConnected = false;
        this.connectedUser = null;
    }

    isBotMessage(messageId) {
        return this.#sentMessages.has(messageId);
    }

    resolveBrandName(savedBrandName) {
        const profileName = this.#sock?.user?.name;
        const validProfileName = profileName && profileName !== '~' ? profileName : '';
        return savedBrandName || validProfileName;
    }

    displayBrandName(savedBrandName) {
        return this.resolveBrandName(savedBrandName) || DEFAULT_BRAND_NAME;
    }

    async connect() {
        if (this.#sock) return; // متصل بالفعل

        if (!fs.existsSync(this.authDir)) {
            fs.mkdirSync(this.authDir, { recursive: true });
        }

        const { state, saveCreds } = await useMultiFileAuthState(this.authDir);

        const sock = makeWASocket({
            auth: state,
            logger: pino({ level: 'silent' }),
            printQRInTerminal: false,
            browser: [`EasyOrders WhatsApp (${this.tenantId})`, 'Chrome', '1.0.0'],
            getMessage: async (key) => this.#sentMessages.get(key.id) || { conversation: 'EasyOrders' }
        });
        this.#sock = sock;

        sock.ev.on('creds.update', saveCreds);
        sock.ev.on('connection.update', (update) => this.#handleConnectionUpdate(sock, update));
        sock.ev.on('messages.upsert', ({ messages }) => this.#messageHandler?.(messages, this.tenantId));
    }

    async logout() {
        this.#resetState();

        const sock = this.#sock;
        this.#sock = null;

        if (!sock) {
            this.#clearAuthFolder();
            setTimeout(() => this.connect(), RECONNECT_DELAY_MS);
            return;
        }

        try { await sock.logout(); } catch {}
        try { sock.end(); } catch {}
    }

    async sendText(jid, text) {
        await this.#ensureFreshSession(jid);
        const sent = await this.#sock.sendMessage(jid, { text });
        if (sent?.key?.id) this.#rememberSentMessage(sent.key.id, sent.message);
        return sent;
    }

    async sendOrderConfirmation(phone, bodyText, brandName, orderId) {
        const jid = toWhatsAppJid(phone);
        const message = this.#buildConfirmationMessage(jid, bodyText, brandName, orderId);

        this.#rememberSentMessage(message.key.id, message.message);
        await this.#ensureFreshSession(jid);

        await this.#sock.relayMessage(jid, message.message, {
            messageId: message.key.id,
            additionalNodes: INTERACTIVE_ADDITIONAL_NODES
        });
    }

    async #handleConnectionUpdate(sock, { connection, lastDisconnect, qr }) {
        if (qr) {
            this.currentQR = await QRCode.toDataURL(qr);
            this.isConnected = false;
            console.log(`📷 [التاجر: ${this.tenantId}] تم توليد QR Code جديد! افتحي http://localhost:3001/whatsapp/${this.tenantId} لمسحه.`);
        }

        if (connection === 'open') {
            this.isConnected = true;
            this.currentQR = null;
            this.connectedUser = sock.user?.id?.split(':')[0] || 'متصل';
            console.log(`✅ [التاجر: ${this.tenantId}] تم ربط الواتساب بنجاح! الرقم: ${this.connectedUser}`);
        }

        if (connection === 'close') {
            this.isConnected = false;
            this.currentQR = null;

            const isLoggedOut = lastDisconnect?.error?.output?.statusCode === DisconnectReason.loggedOut;
            if (isLoggedOut) this.#clearAuthFolder();

            setTimeout(() => this.connect(), RECONNECT_DELAY_MS);
        }
    }

    #buildConfirmationMessage(jid, bodyText, brandName, orderId) {
        const buttons = CONFIRMATION_BUTTONS.map(button => ({
            name: 'quick_reply',
            buttonParamsJson: JSON.stringify({ display_text: button.text, id: `${button.idPrefix}_${orderId}` })
        }));

        const InteractiveMessage = proto.Message.InteractiveMessage;
        return generateWAMessageFromContent(
            jid,
            {
                viewOnceMessage: {
                    message: {
                        messageContextInfo: { deviceListMetadata: {}, deviceListMetadataVersion: 2 },
                        interactiveMessage: InteractiveMessage.create({
                            body: InteractiveMessage.Body.create({ text: bodyText }),
                            footer: InteractiveMessage.Footer.create({ text: `شكراً لثقتك في ${this.displayBrandName(brandName)} 🙏` }),
                            header: InteractiveMessage.Header.create({ title: '', hasMediaAttachment: false }),
                            nativeFlowMessage: InteractiveMessage.NativeFlowMessage.create({ buttons, messageParamsJson: '' })
                        })
                    }
                }
            },
            { userJid: this.#sock?.user?.id }
        );
    }

    async #ensureFreshSession(jid) {
        try {
            const myId = this.#sock?.user?.id;
            const myJid = myId ? toWhatsAppJid(myId.split(':')[0].split('@')[0]) : null;
            await this.#sock.assertSessions(myJid ? [jid, myJid] : [jid], true);
        } catch {}
    }

    #rememberSentMessage(messageId, message) {
        this.#sentMessages.set(messageId, message);
    }

    #resetState() {
        this.isConnected = false;
        this.currentQR = null;
        this.connectedUser = null;
    }

    #clearAuthFolder() {
        try {
            if (fs.existsSync(this.authDir)) {
                fs.rmSync(this.authDir, { recursive: true, force: true });
            }
        } catch {}
    }
}

// مدير الجلسات (Session Manager) لكل التجار على المنصة
class WhatsAppSessionManager {
    #sessions = new Map();
    #globalMessageHandler = null;

    onMessages(handler) {
        this.#globalMessageHandler = handler;
    }

    // إرجاع أو إنشاء جلسة لتاجر معين
    getSession(tenantId = 'default') {
        const key = String(tenantId || 'default').trim().toLowerCase() || 'default';
        if (!this.#sessions.has(key)) {
            const session = new WhatsAppSession(key, (messages, tid) => {
                this.#globalMessageHandler?.(messages, tid);
            });
            this.#sessions.set(key, session);
            session.connect();
        }
        return this.#sessions.get(key);
    }

    getAllSessions() {
        return Array.from(this.#sessions.values());
    }

    isBotMessage(messageId) {
        for (const session of this.#sessions.values()) {
            if (session.isBotMessage(messageId)) return true;
        }
        return false;
    }

    // دوال للتوافق مع الكود القديم (تشتغل على الجلسة الافتراضية default)
    get isConnected() { return this.getSession('default').isConnected; }
    get connectedUser() { return this.getSession('default').connectedUser; }
    get currentQR() { return this.getSession('default').currentQR; }
    resolveBrandName(savedBrand) { return this.getSession('default').resolveBrandName(savedBrand); }
    displayBrandName(savedBrand) { return this.getSession('default').displayBrandName(savedBrand); }
    async connect(tenantId = 'default') { return this.getSession(tenantId).connect(); }
    async logout(tenantId = 'default') { return this.getSession(tenantId).logout(); }
    async sendText(jid, text, tenantId = 'default') { return this.getSession(tenantId).sendText(jid, text); }
    async sendOrderConfirmation(phone, text, brand, orderId, tenantId = 'default') {
        return this.getSession(tenantId).sendOrderConfirmation(phone, text, brand, orderId);
    }
}

module.exports = new WhatsAppSessionManager();
