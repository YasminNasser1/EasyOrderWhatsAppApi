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
const { AUTH_DIR, RECONNECT_DELAY_MS, DEFAULT_BRAND_NAME } = require('../config');
const { toWhatsAppJid } = require('../utils/phone');

// الأزرار التفاعلية اللي بتظهر تحت رسالة التأكيد (زي موقع Confirm-It بالظبط)
const CONFIRMATION_BUTTONS = [
    { text: 'تأكيد الاوردر', idPrefix: 'confirm' },
    { text: 'إلغاء الاوردر', idPrefix: 'cancel' },
    { text: 'طلب الدعم', idPrefix: 'support' }
];

// لازم تتبعت مع الرسالة عشان الواتساب يعرض الأزرار وتبقى clickable
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

// كلاس مسؤول عن كل حاجة ليها علاقة باتصال الواتساب (ربط - QR - إرسال - تسجيل خروج)
class WhatsAppClient {
    #sock = null;
    #messageHandler = null;
    #sentMessages = new Map(); // بنحتفظ بالرسايل المبعوتة عشان لو الواتساب طلب إعادة إرسالها

    currentQR = null;
    isConnected = false;
    connectedUser = null;

    // تسجيل الدالة اللي هتتنفذ لما توصل رسايل جديدة من العملاء
    onMessages(handler) {
        this.#messageHandler = handler;
    }

    isBotMessage(messageId) {
        return this.#sentMessages.has(messageId);
    }

    // اسم البراند: اللي متسجل في الإعدادات، ولو مش موجود ناخد اسم بروفايل الواتساب المربوط
    resolveBrandName(savedBrandName) {
        const profileName = this.#sock?.user?.name;
        const validProfileName = profileName && profileName !== '~' ? profileName : '';
        return savedBrandName || validProfileName;
    }

    displayBrandName(savedBrandName) {
        return this.resolveBrandName(savedBrandName) || DEFAULT_BRAND_NAME;
    }

    async connect() {
        const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);

        const sock = makeWASocket({
            auth: state,
            logger: pino({ level: 'silent' }),
            printQRInTerminal: false,
            browser: ['EasyOrders WhatsApp System', 'Chrome', '1.0.0'],
            getMessage: async (key) => this.#sentMessages.get(key.id) || { conversation: 'EasyOrders' }
        });
        this.#sock = sock;

        sock.ev.on('creds.update', saveCreds);
        sock.ev.on('connection.update', (update) => this.#handleConnectionUpdate(sock, update));
        sock.ev.on('messages.upsert', ({ messages }) => this.#messageHandler?.(messages));
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

        // بعد الخروج، حدث connection.update هيمسح الجلسة ويولد QR Code جديد
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
            console.log('📷 تم توليد QR Code جديد! افتحي http://localhost:3001 لمسحه بموبايلك.');
        }

        if (connection === 'open') {
            this.isConnected = true;
            this.currentQR = null;
            this.connectedUser = sock.user?.id?.split(':')[0] || 'متصل';
            console.log(`✅ تم ربط الواتساب بنجاح! الرقم المتصل: ${this.connectedUser}`);
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

    // بنجدد جلسة التشفير قبل الإرسال عشان الرسالة ماتظهرش "قد يستغرق ذلك بعض الوقت"
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
            if (fs.existsSync(AUTH_DIR)) {
                fs.rmSync(AUTH_DIR, { recursive: true, force: true });
            }
        } catch {}
    }
}

module.exports = new WhatsAppClient();
