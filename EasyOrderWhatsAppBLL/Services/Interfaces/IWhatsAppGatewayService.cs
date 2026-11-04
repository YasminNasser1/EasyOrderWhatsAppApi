using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    // التعامل مع بوابة الواتساب المحلية (Node.js)
    public interface IWhatsAppGatewayService
    {
        Task<bool> SendOrderConfirmationAsync(SendConfirmationViewModel confirmation);
        Task<string?> GetDashboardHtmlAsync(string? tenantId = "default");
        Task LogoutAsync(string? tenantId = "default");
    }
}
