using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class OrderRepository : IOrderRepository
    {
        private readonly EasyOrderDbContext dbContext;

        public OrderRepository(EasyOrderDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task AddAsync(Order order) => await dbContext.Orders.AddAsync(order);

        public int CountOrders() => dbContext.Orders.Count();

        public void Delete(Order order) => dbContext.Orders.Remove(order);

        public IQueryable<Order> GetAll(Expression<Func<Order, bool>>? condition = default)
        {
            if (condition is not null)
            {
                return dbContext.Orders.Where(condition).OrderByDescending(X => X.CreatedAt);
            }
            return dbContext.Orders.OrderByDescending(X => X.CreatedAt);
        }

        public async Task<Order?> GetByIdAsync(int Id)
        {
            return await dbContext.Orders.Where(X => X.Id == Id).FirstOrDefaultAsync();
        }

        public async Task<Order?> GetByShortIdOrUuidAsync(string? shortId, string? orderUuid)
        {
            return await dbContext.Orders
                .Where(X => (!string.IsNullOrEmpty(shortId) && X.ShortId == shortId) ||
                            (!string.IsNullOrEmpty(orderUuid) && X.OrderUuid == orderUuid))
                .OrderByDescending(X => X.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Order?> GetLatestByPhoneAsync(string phone)
        {
            return await dbContext.Orders
                .Where(X => X.Phone == phone)
                .OrderByDescending(X => X.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public void Update(Order order) => dbContext.Orders.Update(order);
    }
}
