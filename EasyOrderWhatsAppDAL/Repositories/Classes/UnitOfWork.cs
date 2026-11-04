using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EasyOrderDbContext dbContext;

        public UnitOfWork(
            IOrderRepository orderRepository,
            IStoreSettingRepository storeSettingRepository,
            ITenantRepository tenantRepository,
            EasyOrderDbContext dbContext)
        {
            OrderRepository = orderRepository;
            StoreSettingRepository = storeSettingRepository;
            TenantRepository = tenantRepository;
            this.dbContext = dbContext;
        }

        public IOrderRepository OrderRepository { get; }
        public IStoreSettingRepository StoreSettingRepository { get; }
        public ITenantRepository TenantRepository { get; }

        public async Task<int> SaveChangesAsync()
        {
            return await dbContext.SaveChangesAsync();
        }
    }
}
