using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface ITenantRepository : IGenericRepository<Tenant>
    {
        Task<Tenant?> GetByKeyAsync(string storeKey);
    }
}
