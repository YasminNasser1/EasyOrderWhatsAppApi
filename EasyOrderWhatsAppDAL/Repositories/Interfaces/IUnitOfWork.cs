namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface IUnitOfWork
    {
        public IOrderRepository OrderRepository { get; }
        public IStoreSettingRepository StoreSettingRepository { get; }
        public Task<int> SaveChangesAsync();
    }
}
