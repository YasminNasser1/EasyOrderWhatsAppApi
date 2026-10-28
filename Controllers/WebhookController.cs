using EasyOrderWhatsAppApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace EasyOrderWhatsAppApi.Controllers
{
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly WhatsAppService _whatsAppService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            WhatsAppService whatsAppService,
            IHttpClientFactory httpClientFactory,
            ILogger<WebhookController> logger)
        {
            _whatsAppService = whatsAppService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // صفحة إدارة الواتساب والـ QR Code على نفس الدومين السحابي (/whatsapp)
        [HttpGet("/whatsapp")]
        public async Task<IActionResult> WhatsAppDashboard()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                string html = await client.GetStringAsync("http://localhost:3001/");
                return Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                return Content("<h2 style='font-family:sans-serif;text-align:center;margin-top:50px;'>⏳ جاري تشغيل بوابة الواتساب... حدث الصفحة بعد 5 ثواني</h2>", "text/html; charset=utf-8");
            }
        }

        // زرار تغيير الرقم وفصل الجلسة الحالية على نفس الدومين السحابي
        [HttpPost("/whatsapp/logout")]
        public async Task<IActionResult> WhatsAppLogout()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                await client.PostAsync("http://localhost:3001/logout", null);
            }
            catch { }
            return Redirect("/whatsapp");
        }

        [HttpGet("/api/webhook/easyorder")]
        public IActionResult HealthCheck()
        {
            return Ok("✅ السيرفر السحابي شغال 24 ساعة وجاهز لاستقبال الأوردرات من EasyOrders!");
        }

        [HttpPost("/api/webhook/easyorder")]
        [HttpPost("/")]
        public async Task<IActionResult> ReceiveEasyOrder()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            string rawBody = await reader.ReadToEndAsync();

            _logger.LogInformation("📥 وصل طلب من EasyOrders! محتوى البيانات الخام:\n{RawBody}", rawBody);

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Ok(new { success = true, message = "Webhook verified successfully" });
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(rawBody);
                JsonElement root = doc.RootElement;

                string orderId = FindStringRecursive(root, "short_id", "id", "order_id") ?? "جديد";
                string fullName = FindStringRecursive(root, "full_name", "name", "customer_name", "client_name") ?? "عميلنا العزيز";
                string? phone = FindStringRecursive(root, "phone", "mobile", "customer_phone", "phone_number");
                string gov = FindStringRecursive(root, "gov", "government", "governorate", "city") ?? "";
                string address = FindStringRecursive(root, "address", "street_address") ?? "";
                string totalCost = FindStringRecursive(root, "total_cost", "total", "cost", "total_price") ?? "";

                _logger.LogInformation("🔔 تم استخراج بيانات الأوردر -> رقم الطلب: {OrderId} | الاسم: {Name} | الموبايل: {Phone}",
                    orderId, fullName, phone);

                if (string.IsNullOrWhiteSpace(phone))
                {
                    return Ok(new { success = true, message = "Webhook test accepted" });
                }

                var productsBuilder = new StringBuilder();
                if (TryFindPropertyRecursive(root, "cart_items", out JsonElement cartItems) && cartItems.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in cartItems.EnumerateArray())
                    {
                        string qty = FindStringRecursive(item, "quantity", "qty") ?? "1";
                        string prodName = "منتج";
                        if (item.TryGetProperty("product", out JsonElement prod) && prod.ValueKind == JsonValueKind.Object)
                        {
                            prodName = FindStringRecursive(prod, "name", "title") ?? "منتج";
                        }
                        else
                        {
                            prodName = FindStringRecursive(item, "product_name", "name", "title") ?? "منتج";
                        }
                        productsBuilder.AppendLine($"• {prodName} (الكمية: {qty})");
                    }
                }

                if (productsBuilder.Length == 0)
                {
                    productsBuilder.AppendLine("• منتجات الطلب");
                }

                string locationLine = !string.IsNullOrWhiteSpace(gov) || !string.IsNullOrWhiteSpace(address)
                    ? $"📍 *العنوان:* {gov} - {address}\n"
                    : "";

                string costLine = !string.IsNullOrWhiteSpace(totalCost)
                    ? $"💰 *الإجمالي شامل الشحن:* {totalCost} ج.م\n"
                    : "";

                string message = $"أهلاً بك يا *{fullName}* 👋\n\n" +
                                 $"تم استلام وتأكيد طلبك بنجاح! ✅\n" +
                                 $"🔢 *رقم الطلب:* {orderId}\n" +
                                 $"📦 *المنتجات:*\n{productsBuilder}\n" +
                                 $"{locationLine}" +
                                 $"{costLine}\n" +
                                 $"شكراً لثقتك بنا، وسيتم التواصل معك قريباً للشحن 🚚";

                await _whatsAppService.SendOrderConfirmationAsync(phone, message);

                return Ok(new { success = true, message = "Order received and WhatsApp notification triggered." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ أثناء قراءة بيانات الأوردر");
                return Ok(new { success = true, warning = ex.Message });
            }
        }

        private static string? FindStringRecursive(JsonElement element, params string[] keys)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in keys)
                {
                    if (element.TryGetProperty(key, out JsonElement val))
                    {
                        if (val.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(val.GetString()))
                            return val.GetString();
                        if (val.ValueKind == JsonValueKind.Number)
                            return val.GetRawText();
                    }
                }

                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        var found = FindStringRecursive(prop.Value, keys);
                        if (!string.IsNullOrWhiteSpace(found))
                            return found;
                    }
                }
            }
            return null;
        }

        private static bool TryFindPropertyRecursive(JsonElement element, string propertyName, out JsonElement result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(propertyName, out result))
                    return true;

                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        TryFindPropertyRecursive(prop.Value, propertyName, out result))
                        return true;
                }
            }
            result = default;
            return false;
        }
    }
}
