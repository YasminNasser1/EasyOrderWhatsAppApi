using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class TenantRepository : GenericRepository<Tenant>, ITenantRepository
    {
        public TenantRepository(EasyOrderDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<Tenant?> GetByKeyAsync(string storeKey)
        {
            if (string.IsNullOrWhiteSpace(storeKey)) return null;

            var normalizedKey = storeKey.Trim().ToLower();
            return await GetAll(t => t.StoreKey.ToLower() == normalizedKey)
                .FirstOrDefaultAsync();
        }
    }
}
