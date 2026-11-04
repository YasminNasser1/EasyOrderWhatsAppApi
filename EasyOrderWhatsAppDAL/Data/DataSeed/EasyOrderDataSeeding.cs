using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Data.FileStorage;
using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppDAL.Data.DataSeed
{
    public static class EasyOrderDataSeeding
    {
        // أول ما السيرفر يشتغل: بنحمّل إعدادات المتجر المحفوظة في الملفات جوه الداتابيز
        public static async Task SeedData(EasyOrderDbContext dbContext, ISettingsFileStore settingsFileStore)
        {
            if (!dbContext.StoreSettings.Any())
            {
                await dbContext.StoreSettings.AddAsync(new StoreSetting
                {
                    ApiKey = settingsFileStore.ReadApiKey(),
                    BrandName = settingsFileStore.ReadBrandName()
                });
                await dbContext.SaveChangesAsync();
            }

            if (!dbContext.Tenants.Any())
            {
                var defaultSetting = dbContext.StoreSettings.FirstOrDefault();
                await dbContext.Tenants.AddAsync(new Tenant
                {
                    StoreKey = "default",
                    BrandName = defaultSetting?.BrandName ?? settingsFileStore.ReadBrandName(),
                    ApiKey = defaultSetting?.ApiKey ?? settingsFileStore.ReadApiKey(),
                    IsActive = true
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
