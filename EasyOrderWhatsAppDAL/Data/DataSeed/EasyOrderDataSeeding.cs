using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppDAL.Data.DataSeed
{
    public static class EasyOrderDataSeeding
    {
        public static async Task SeedData(EasyOrderDbContext dbContext, string defaultBrandName, string defaultApiKey)
        {
            if (!dbContext.StoreSettings.Any())
            {
                await dbContext.StoreSettings.AddAsync(new StoreSetting
                {
                    BrandName = string.IsNullOrWhiteSpace(defaultBrandName) ? "Outlix" : defaultBrandName,
                    ApiKey = defaultApiKey ?? "",
                    WebhookSecret = "123456",
                    CreatedAt = DateTime.Now
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
