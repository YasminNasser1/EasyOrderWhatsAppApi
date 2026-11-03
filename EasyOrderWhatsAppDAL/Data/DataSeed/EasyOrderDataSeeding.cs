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
            if (dbContext.StoreSettings.Any())
                return;

            await dbContext.StoreSettings.AddAsync(new StoreSetting
            {
                ApiKey = settingsFileStore.ReadApiKey(),
                BrandName = settingsFileStore.ReadBrandName()
            });
            await dbContext.SaveChangesAsync();
        }
    }
}
