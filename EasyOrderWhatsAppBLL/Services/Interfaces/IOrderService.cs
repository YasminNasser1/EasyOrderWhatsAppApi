using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    public interface IOrderService
    {
        Task<bool> ProcessIncomingWebhookOrderAsync(string rawJson, string? tenantId = "default");
        Task<bool> ProcessGenericOrderAsync(CustomOrderWebhookViewModel customOrder, string? tenantId = "default");
        Task<bool> HandleCustomerActionAsync(OrderActionViewModel actionModel);
        IEnumerable<OrderViewModel> GetAllOrders(string? tenantId = null);
        Task<OrderViewModel?> GetOrderAsync(int id);
    }
}
