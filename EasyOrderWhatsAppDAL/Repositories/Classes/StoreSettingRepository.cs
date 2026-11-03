using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class StoreSettingRepository : IStoreSettingRepository
    {
        private readonly EasyOrderDbContext dbContext;

        public StoreSettingRepository(EasyOrderDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<StoreSetting?> GetCurrentSettingAsync()
        {
            return await dbContext.StoreSettings.FirstOrDefaultAsync();
        }

        public async Task AddAsync(StoreSetting setting) => await dbContext.StoreSettings.AddAsync(setting);

        public void Update(StoreSetting setting) => dbContext.StoreSettings.Update(setting);
    }
}
