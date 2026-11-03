using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface IStoreSettingRepository : IGenericRepository<StoreSetting>
    {
        Task<StoreSetting?> GetCurrentSettingAsync();
    }
}
