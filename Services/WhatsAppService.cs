using System.Text;
using System.Text.Json;

namespace EasyOrderWhatsAppApi.Services
{
    public class WhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendOrderConfirmationAsync(string phone, string message)
        {
            string formattedPhone = FormatEgyptianPhone(phone);

            // رابط البوابة المجانية اللي شغالة على جهازك
            string gatewayUrl = _config["WhatsApp:GatewayUrl"] ?? "http://localhost:3001/send-message";

            _logger.LogInformation("==========================================");
            _logger.LogInformation("📱 جاري إرسال رسالة واتساب للرقم: {Phone}", formattedPhone);
            _logger.LogInformation("✉️ نص الرسالة:\n{Message}", message);
            _logger.LogInformation("==========================================");

            var payload = new
            {
                phone = formattedPhone,
                message = message
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await _httpClient.PostAsync(gatewayUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ تم إرسال رسالة الواتساب للعميل بنجاح! {Response}", responseBody);
                    return true;
                }
                else
                {
                    _logger.LogWarning("⚠️ تنبيه من بوابة الواتساب: {Response}", responseBody);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ تعذر الاتصال ببوابة الواتساب المحلية (تأكدي من تشغيل WhatsAppGateway على بورت 3001): {Error}", ex.Message);
                return false;
            }
        }

        private string FormatEgyptianPhone(string phone)
        {
            phone = phone.Trim().Replace(" ", "").Replace("+", "").Replace("-", "");

            if (phone.StartsWith("01") && phone.Length == 11)
            {
                return "2" + phone; // مثال: 201012345678
            }
            return phone;
        }
    }
}
