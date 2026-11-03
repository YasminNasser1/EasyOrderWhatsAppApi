using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    // إعدادات المتجر (اسم البراند + مفتاح EasyOrders API)
    public interface IStoreSettingService
    {
        Task<StoreSettingStatusViewModel> GetStatusAsync();
        Task<string> GetBrandNameAsync();
        Task<string> GetApiKeyAsync();
        Task<bool> SaveAsync(SaveSettingViewModel model);
    }
}
