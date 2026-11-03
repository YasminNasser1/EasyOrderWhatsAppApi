using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface IStoreSettingRepository
    {
        Task<StoreSetting?> GetCurrentSettingAsync();
        Task AddAsync(StoreSetting setting);
        void Update(StoreSetting setting);
    }
}
