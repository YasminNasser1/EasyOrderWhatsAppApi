using EasyOrderWhatsAppBLL.Constants;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class WhatsAppGatewayService : IWhatsAppGatewayService
    {
        private readonly HttpClient httpClient; // الـ BaseAddress متظبط في Program.cs
        private readonly ILogger<WhatsAppGatewayService> logger;

        public WhatsAppGatewayService(HttpClient httpClient, ILogger<WhatsAppGatewayService> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        // 1. إرسال رسالة تأكيد الأوردر للعميل على الواتساب
        public async Task<bool> SendOrderConfirmationAsync(SendConfirmationViewModel confirmation)
        {
            try
            {
                logger.LogInformation("📱 جاري إرسال رسالة تأكيد للرقم: {Phone}", confirmation.Phone);
                using var response = await httpClient.PostAsJsonAsync(WhatsAppGatewayRoutes.SendMessage, confirmation);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.LogError("❌ تعذر الاتصال ببوابة الواتساب: {Error}", ex.Message);
                return false;
            }
        }

        // 2. جلب صفحة الواتساب (QR Code / متصل) للتاجر المحدد عشان نعرضها على بورت 5055
        public async Task<string?> GetDashboardHtmlAsync(string? tenantId = "default")
        {
            try
            {
                var route = string.IsNullOrWhiteSpace(tenantId) || tenantId.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? WhatsAppGatewayRoutes.Dashboard
                    : $"/whatsapp/{tenantId.Trim().ToLower()}";

                return await httpClient.GetStringAsync(route);
            }
            catch
            {
                return null; // البوابة لسه بتقوم
            }
        }

        // 3. تسجيل الخروج من جلسة الواتساب للتاجر المحدد
        public async Task LogoutAsync(string? tenantId = "default")
        {
            try
            {
                var route = string.IsNullOrWhiteSpace(tenantId) || tenantId.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? WhatsAppGatewayRoutes.Logout
                    : $"/logout/{tenantId.Trim().ToLower()}";

                await httpClient.PostAsync(route, null);
            }
            catch (Exception ex)
            {
                logger.LogError("❌ تعذر تسجيل الخروج من الواتساب للتاجر {Tenant}: {Error}", tenantId, ex.Message);
            }
        }
    }
}
