using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    // إعدادات المتجر (اسم البراند + مفتاح EasyOrders API)
    public interface IStoreSettingService
    {
        Task<StoreSettingStatusViewModel> GetStatusAsync(string? tenantId = "default");
        Task<string> GetBrandNameAsync(string? tenantId = "default");
        Task<string> GetApiKeyAsync(string? tenantId = "default");
        Task<bool> SaveAsync(SaveSettingViewModel model);
    }
}
