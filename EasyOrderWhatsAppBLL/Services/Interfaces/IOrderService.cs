using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    public interface IOrderService
    {
        Task<bool> ProcessIncomingWebhookOrder(string rawJson);
        Task<bool> HandleCustomerOrderAction(OrderActionViewModel actionModel);
        IEnumerable<OrderViewModel> GetAllOrders();
        Task<OrderViewModel?> GetOrder(int id);
        Task<(bool hasKey, string maskedKey, string brandName)> GetStoreSettingStatus();
        Task<bool> SaveStoreSetting(SaveSettingViewModel model);
    }
}
