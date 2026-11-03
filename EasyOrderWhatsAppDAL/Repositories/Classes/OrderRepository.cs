using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class OrderRepository : GenericRepository<Order>, IOrderRepository
    {
        public OrderRepository(EasyOrderDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<Order?> GetByShortIdOrUuidAsync(string? shortId, string? orderUuid)
        {
            return await GetAll(order =>
                    (!string.IsNullOrEmpty(shortId) && order.ShortId == shortId) ||
                    (!string.IsNullOrEmpty(orderUuid) && order.OrderUuid == orderUuid))
                .FirstOrDefaultAsync();
        }
    }
}
