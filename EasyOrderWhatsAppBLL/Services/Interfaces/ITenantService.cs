using EasyOrderWhatsAppBLL.ViewModels.TenantViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    public interface ITenantService
    {
        Task<TenantViewModel?> GetTenantByKeyAsync(string storeKey);
        Task<IEnumerable<TenantViewModel>> GetAllTenantsAsync();
        Task<TenantViewModel> RegisterTenantAsync(RegisterTenantViewModel model);
        Task<TenantViewModel?> UpdateTenantAsync(string storeKey, UpdateTenantViewModel model);
        Task<TenantViewModel> EnsureTenantExistsAsync(string storeKey, string? brandName = null);
    }
}
