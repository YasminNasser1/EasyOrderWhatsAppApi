using EasyOrderWhatsAppBLL.Helpers;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient httpClient;
        private readonly IConfiguration config;
        private readonly ILogger<WhatsAppService> logger;

        public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger)
        {
            this.httpClient = httpClient;
            this.config = config;
            this.logger = logger;
        }

        // 1. إرسال رسالة التأكيد لبوابة الواتساب المحلية (Node.js Gateway)
        public async Task<bool> SendOrderConfirmationAsync(
            string phone,
            string message,
            string? orderUuid,
            string? shortId,
            string? storeId,
            string? customerName,
            string brandName)
        {
            string formattedPhone = OrderHelperMethod.FormatEgyptianPhone(phone);
            string gatewayUrl = config["WhatsApp:GatewayUrl"] ?? "http://localhost:3001/send-message";

            logger.LogInformation("📱 جاري إرسال رسالة تأكيد للرقم: {Phone}", formattedPhone);

            var payload = new
            {
                phone = formattedPhone,
                message = message,
                orderUuid = orderUuid ?? "",
                orderId = shortId ?? orderUuid ?? "جديد",
                storeId = storeId ?? "",
                customerName = customerName ?? "عميل",
                brandName = brandName
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await httpClient.PostAsync(gatewayUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.LogError("❌ تعذر الاتصال ببوابة الواتساب: {Error}", ex.Message);
                return false;
            }
        }

        // 2. تحديث حالة الطلب على موقع EasyOrders (مؤكد confirmed / ملغي canceled)
        public async Task<bool> UpdateOrderStatusOnEasyOrdersAsync(string? orderUuid, string? shortId, string status, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogWarning("⚠️ لم يتم إدخال مفتاح EasyOrders Public API Key بعد!");
                return false;
            }

            var (resolvedUuid, _) = await ResolveOrderUuidAndStoreIdAsync(orderUuid, shortId, null, apiKey);
            if (string.IsNullOrWhiteSpace(resolvedUuid))
                return false;

            string url = $"https://api.easy-orders.net/api/v1/external-apps/orders/{resolvedUuid}/status";
            using var request = new HttpRequestMessage(HttpMethod.Patch, url);
            request.Headers.Add("Api-Key", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { status = status }),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // 3. كتابة سبب الإلغاء أو ملاحظة التأكيد جوه الطلب نفسه على موقع EasyOrders
        public async Task<bool> AddOrderNoteOnEasyOrdersAsync(string? orderUuid, string? shortId, string? storeId, string noteText, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            var (resolvedUuid, resolvedStoreId) = await ResolveOrderUuidAndStoreIdAsync(orderUuid, shortId, storeId, apiKey);
            if (string.IsNullOrWhiteSpace(resolvedUuid))
                return false;

            string url = "https://api.easy-orders.net/api/v1/external-apps/order-notes";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Api-Key", apiKey);

            var payload = new
            {
                order_id = resolvedUuid,
                note = noteText,
                store_id = resolvedStoreId ?? "",
                type = "public"
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<(string? orderUuid, string? storeId)> ResolveOrderUuidAndStoreIdAsync(
            string? orderUuid,
            string? shortId,
            string? storeId,
            string apiKey)
        {
            bool hasValidUuid = !string.IsNullOrWhiteSpace(orderUuid) && orderUuid.Contains('-');
            if (hasValidUuid && !string.IsNullOrWhiteSpace(storeId))
                return (orderUuid, storeId);

            try
            {
                string lookupUrl = hasValidUuid
                    ? $"https://api.easy-orders.net/api/v1/external-apps/orders/{orderUuid}"
                    : $"https://api.easy-orders.net/api/v1/external-apps/orders/short/{shortId ?? orderUuid}";

                using var req = new HttpRequestMessage(HttpMethod.Get, lookupUrl);
                req.Headers.Add("Api-Key", apiKey);

                var res = await httpClient.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!hasValidUuid && root.TryGetProperty("id", out var idProp))
                        orderUuid = idProp.GetString();

                    if (string.IsNullOrWhiteSpace(storeId) && root.TryGetProperty("store_id", out var storeProp))
                        storeId = storeProp.GetString();
                }
            }
            catch { }

            return (orderUuid, storeId);
        }
    }
}
