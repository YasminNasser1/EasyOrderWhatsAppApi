using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;
using EasyOrderWhatsAppDAL.Data.FileStorage;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class StoreSettingService : IStoreSettingService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly ISettingsFileStore settingsFileStore;

        public StoreSettingService(IUnitOfWork unitOfWork, ISettingsFileStore settingsFileStore)
        {
            this.unitOfWork = unitOfWork;
            this.settingsFileStore = settingsFileStore;
        }

        public async Task<StoreSettingStatusViewModel> GetStatusAsync(string? tenantId = "default")
        {
            var key = NormalizeTenant(tenantId);
            if (key != "default")
            {
                var tenant = await unitOfWork.TenantRepository.GetByKeyAsync(key);
                if (tenant != null)
                {
                    return new StoreSettingStatusViewModel
                    {
                        HasKey = tenant.HasApiKey,
                        MaskedKey = MaskApiKey(tenant.ApiKey ?? ""),
                        BrandName = tenant.BrandName
                    };
                }
            }

            var setting = await GetCurrentSettingAsync();
            return new StoreSettingStatusViewModel
            {
                HasKey = setting.HasApiKey,
                MaskedKey = MaskApiKey(setting.ApiKey),
                BrandName = setting.BrandName
            };
        }

        public async Task<string> GetBrandNameAsync(string? tenantId = "default")
        {
            var key = NormalizeTenant(tenantId);
            if (key != "default")
            {
                var tenant = await unitOfWork.TenantRepository.GetByKeyAsync(key);
                if (tenant != null && !string.IsNullOrWhiteSpace(tenant.BrandName))
                    return tenant.BrandName;
            }
            return (await GetCurrentSettingAsync()).BrandName;
        }

        public async Task<string> GetApiKeyAsync(string? tenantId = "default")
        {
            var key = NormalizeTenant(tenantId);
            if (key != "default")
            {
                var tenant = await unitOfWork.TenantRepository.GetByKeyAsync(key);
                if (tenant != null && !string.IsNullOrWhiteSpace(tenant.ApiKey))
                    return tenant.ApiKey;
            }
            return (await GetCurrentSettingAsync()).ApiKey;
        }

        public async Task<bool> SaveAsync(SaveSettingViewModel model)
        {
            var key = NormalizeTenant(model.TenantId);

            // 1. تحديث التاجر في جدول Tenants
            var tenant = await unitOfWork.TenantRepository.GetByKeyAsync(key);
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    StoreKey = key,
                    BrandName = model.BrandName ?? key,
                    ApiKey = model.ApiKey,
                    IsActive = true
                };
                await unitOfWork.TenantRepository.AddAsync(tenant);
            }
            else
            {
                tenant.Update(model.BrandName, model.ApiKey, null);
                unitOfWork.TenantRepository.Update(tenant);
            }

            // 2. لو التاجر هو default نحفظ كمان في StoreSetting والملف للحفاظ على التوافق الكامل
            if (key == "default")
            {
                var setting = await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync();
                if (setting is null)
                {
                    setting = new StoreSetting();
                    await unitOfWork.StoreSettingRepository.AddAsync(setting);
                }

                setting.Update(model.ApiKey, model.BrandName);
                settingsFileStore.Save(model.ApiKey, model.BrandName);
            }

            return await unitOfWork.SaveChangesAsync() > 0;
        }

        private static string NormalizeTenant(string? tenantId) =>
            string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId.Trim().ToLower();

        private async Task<StoreSetting> GetCurrentSettingAsync()
        {
            return await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync() ?? new StoreSetting();
        }

        // مثال: 3d79xxxxxxxxc522 -> 3d79****c522
        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "";

            return apiKey.Length > 8 ? $"{apiKey[..4]}****{apiKey[^4..]}" : "****";
        }
    }
}
