const fs = require('fs');
const path = require('path');

// الـ CSS في ملف لوحده عشان الكود يبقى أنضف (وبنحطه جوه الصفحة عشان تشتغل كمان من بورت 5055)
const DASHBOARD_CSS = fs.readFileSync(path.join(__dirname, 'dashboard.css'), 'utf8');
const AUTO_REFRESH_MS = 4000;

const WHATSAPP_LOGO_SVG = `
<svg viewBox="0 0 39 39" width="39" height="39">
    <path fill="#00E676" d="M10.7 32.8l.6.3c2.5 1.5 5.3 2.2 8.1 2.2 8.8 0 16-7.2 16-16 0-4.2-1.7-8.3-4.7-11.3s-7-4.7-11.3-4.7c-8.8 0-16 7.2-15.9 16.1 0 3 .9 5.9 2.4 8.4l.4.6-1.6 5.9 6-1.5z"></path>
    <path fill="#FFF" d="M32.4 6.4C29 2.9 24.3 1 19.5 1 9.3 1 1.1 9.3 1.2 19.4c0 3.2.9 6.3 2.4 9.1L1 38l9.7-2.5c2.7 1.5 5.7 2.2 8.7 2.2 10.1 0 18.3-8.3 18.3-18.4 0-4.9-1.9-9.5-5.3-12.9zM19.5 34.6c-2.7 0-5.4-.7-7.7-2.1l-.6-.3-5.8 1.5L6.9 28l-.4-.6c-4.4-7.1-2.3-16.5 4.9-20.9s16.5-2.3 20.9 4.9 2.3 16.5-4.9 20.9c-2.3 1.5-5.1 2.3-7.9 2.3zm8.8-11.1l-1.1-.5s-1.6-.7-2.6-1.2c-.1 0-.2-.1-.3-.1-.3 0-.5.1-.7.2 0 0-.1.1-1.5 1.7-.1.2-.3.3-.5.3h-.1c-.1 0-.3-.1-.4-.2l-.5-.2c-1.1-.5-2.1-1.1-2.9-1.9-.2-.2-.5-.4-.7-.6-.7-.7-1.4-1.5-1.9-2.4l-.1-.2c-.1-.1-.1-.2-.2-.4 0-.2 0-.4.1-.5 0 0 .4-.5.7-.8.2-.2.3-.5.5-.7.2-.3.3-.7.2-1-.1-.5-1.3-3.2-1.6-3.8-.2-.3-.4-.4-.7-.5h-1.1c-.2 0-.4.1-.6.1l-.1.1c-.2.1-.4.3-.6.4-.2.2-.3.4-.5.6-.7.9-1.1 2-1.1 3.1 0 .8.2 1.6.5 2.3l.1.3c.9 1.9 2.1 3.6 3.7 5.1l.4.4c.3.3.6.5.8.8 2.1 1.8 4.5 3.1 7.2 3.8.3.1.7.1 1 .2h1c.5 0 1.1-.2 1.5-.4.3-.2.5-.2.7-.4l.2-.2c.2-.2.4-.3.6-.5s.4-.4.5-.6c.2-.4.3-.9.4-1.4v-.7s-.1-.1-.3-.2z"></path>
</svg>`;

// الصفحة الرئيسية بشكل WhatsApp Web
function renderDashboard({ isConnected, connectedUser, currentQR, settings, webhookUrl, tenantId = 'default' }) {
    return `
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>WhatsApp Web${tenantId !== 'default' ? ` - ${escapeHtml(tenantId)}` : ''}</title>
    <style>
        ${DASHBOARD_CSS}
        .wa-tenant-tag {
            background: rgba(255,255,255,0.25);
            color: #fff;
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 12px;
            margin-right: 10px;
            font-weight: 500;
        }
    </style>
</head>
<body>
    ${renderHeader(tenantId)}
    <div class="wa-main">
        <div class="wa-card">
            ${isConnected ? renderConnectedView(connectedUser, tenantId) : renderQrView(currentQR)}
        </div>
    </div>
    ${renderSettingsModal(settings, webhookUrl, tenantId)}
    ${isConnected ? '' : renderAutoRefreshScript()}
</body>
</html>`;
}

function renderHeader(tenantId) {
    const tenantBadge = tenantId && tenantId !== 'default'
        ? `<span class="wa-tenant-tag">متجر: ${escapeHtml(tenantId)}</span>`
        : '';

    return `
    <div class="wa-header">
        <div class="wa-header-inner">
            <div class="wa-brand">${WHATSAPP_LOGO_SVG}<span>WHATSAPP WEB</span> ${tenantBadge}</div>
            <button class="wa-settings-btn" onclick="document.getElementById('settingsModal').classList.add('active')">⚙️ الإعدادات</button>
        </div>
    </div>`;
}

function renderConnectedView(connectedUser, tenantId) {
    const logoutAction = tenantId && tenantId !== 'default' ? `/logout/${encodeURIComponent(tenantId)}` : '/logout';
    return `
            <div class="wa-connected">
                <div class="wa-connected-icon">✓</div>
                <h2>واتساب متصل الآن</h2>
                <p dir="ltr">+${connectedUser}</p>
                <form method="POST" action="${logoutAction}">
                    <button type="submit" class="wa-logout-btn">تسجيل الخروج</button>
                </form>
            </div>`;
}

function renderQrView(currentQR) {
    const qrContent = currentQR
        ? `<img src="${currentQR}" alt="QR Code" />`
        : `<div class="wa-spinner"></div>`;

    return `
            <div class="wa-instructions">
                <div class="wa-title">استخدام واتساب على الكمبيوتر</div>
                <ol class="wa-steps">
                    <li>١. افتح واتساب على هاتفك</li>
                    <li>٢. اضغط على <strong>القائمة</strong> أو <strong>الإعدادات</strong> وحدد <strong>الأجهزة المرتبطة</strong></li>
                    <li>٣. اضغط على <strong>ربط جهاز</strong> ووجّه هاتفك إلى هذه الشاشة لمسح الرمز</li>
                </ol>
            </div>
            <div class="wa-qr-box">${qrContent}</div>`;
}

function renderSettingsModal({ brandName, hasKey, maskedKey }, webhookUrl, tenantId) {
    return `
    <div id="settingsModal" class="modal-backdrop">
        <div class="modal-box">
            <h3>إعدادات المتجر ${tenantId && tenantId !== 'default' ? `(${escapeHtml(tenantId)})` : ''}</h3>
            <form method="POST" action="/save-apikey">
                <input type="hidden" name="tenantId" value="${escapeHtml(tenantId || 'default')}" />
                <label>اسم البراند:</label>
                <input type="text" name="brandName" value="${escapeHtml(brandName)}" />
                <label>مفتاح EasyOrders Public API Key ${hasKey ? `(${maskedKey})` : ''}:</label>
                <input type="text" name="apiKey" dir="ltr" placeholder="API Key..." />
                <label>لينك الـ Webhook الخاص بك:</label>
                <input type="text" dir="ltr" readonly value="${webhookUrl}" onclick="this.select()" />
                <div class="modal-actions">
                    <button type="submit" class="btn-primary">حفظ</button>
                    <button type="button" class="btn-close" onclick="document.getElementById('settingsModal').classList.remove('active')">إغلاق</button>
                </div>
            </form>
        </div>
    </div>`;
}

// تحديث الصفحة كل 4 ثواني لحد ما الواتساب يتربط (ومش بيحدث وانتي فاتحة الإعدادات)
function renderAutoRefreshScript() {
    return `
    <script>
        setInterval(function () {
            var modal = document.getElementById('settingsModal');
            if (!modal.classList.contains('active')) window.location.reload();
        }, ${AUTO_REFRESH_MS});
    </script>`;
}

function escapeHtml(value) {
    return String(value || '').replace(/[&<>"']/g, char => (
        { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]
    ));
}

module.exports = { renderDashboard };
