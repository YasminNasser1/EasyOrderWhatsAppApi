using EasyOrderWhatsAppDAL.Entities;

namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<Order?> GetByShortIdOrUuidAsync(string? shortId, string? orderUuid);
    }
}
