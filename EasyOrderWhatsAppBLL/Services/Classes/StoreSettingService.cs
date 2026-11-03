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

        public async Task<StoreSettingStatusViewModel> GetStatusAsync()
        {
            var setting = await GetCurrentSettingAsync();
            return new StoreSettingStatusViewModel
            {
                HasKey = setting.HasApiKey,
                MaskedKey = MaskApiKey(setting.ApiKey),
                BrandName = setting.BrandName
            };
        }

        public async Task<string> GetBrandNameAsync() => (await GetCurrentSettingAsync()).BrandName;

        public async Task<string> GetApiKeyAsync() => (await GetCurrentSettingAsync()).ApiKey;

        public async Task<bool> SaveAsync(SaveSettingViewModel model)
        {
            var setting = await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync();
            if (setting is null)
            {
                setting = new StoreSetting();
                await unitOfWork.StoreSettingRepository.AddAsync(setting);
            }

            setting.Update(model.ApiKey, model.BrandName);
            settingsFileStore.Save(model.ApiKey, model.BrandName); // عشان تفضل محفوظة بعد إعادة التشغيل

            return await unitOfWork.SaveChangesAsync() > 0;
        }

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
