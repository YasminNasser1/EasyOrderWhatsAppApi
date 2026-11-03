using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    public interface IOrderService
    {
        Task<bool> ProcessIncomingWebhookOrderAsync(string rawJson);
        Task<bool> HandleCustomerActionAsync(OrderActionViewModel actionModel);
        IEnumerable<OrderViewModel> GetAllOrders();
        Task<OrderViewModel?> GetOrderAsync(int id);
    }
}
