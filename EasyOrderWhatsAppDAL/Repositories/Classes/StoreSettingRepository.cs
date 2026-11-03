using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class StoreSettingRepository : GenericRepository<StoreSetting>, IStoreSettingRepository
    {
        public StoreSettingRepository(EasyOrderDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<StoreSetting?> GetCurrentSettingAsync()
        {
            return await dbContext.StoreSettings.FirstOrDefaultAsync();
        }
    }
}
