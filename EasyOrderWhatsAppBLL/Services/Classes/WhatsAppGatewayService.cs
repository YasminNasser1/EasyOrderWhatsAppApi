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

        // 2. جلب صفحة الواتساب (QR Code / متصل) عشان نعرضها على بورت 5055
        public async Task<string?> GetDashboardHtmlAsync()
        {
            try
            {
                return await httpClient.GetStringAsync(WhatsAppGatewayRoutes.Dashboard);
            }
            catch
            {
                return null; // البوابة لسه بتقوم
            }
        }

        // 3. تسجيل الخروج من الواتساب المربوط
        public async Task LogoutAsync()
        {
            try
            {
                await httpClient.PostAsync(WhatsAppGatewayRoutes.Logout, null);
            }
            catch (Exception ex)
            {
                logger.LogError("❌ تعذر تسجيل الخروج من الواتساب: {Error}", ex.Message);
            }
        }
    }
}
