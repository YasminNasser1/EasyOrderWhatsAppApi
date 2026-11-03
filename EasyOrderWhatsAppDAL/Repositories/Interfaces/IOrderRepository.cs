using EasyOrderWhatsAppDAL.Entities;
using System.Linq.Expressions;

namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface IOrderRepository
    {
        IQueryable<Order> GetAll(Expression<Func<Order, bool>>? condition = default);
        Task<Order?> GetByIdAsync(int Id);
        Task<Order?> GetByShortIdOrUuidAsync(string? shortId, string? orderUuid);
        Task<Order?> GetLatestByPhoneAsync(string phone);
        Task AddAsync(Order order);
        void Update(Order order);
        void Delete(Order order);
        int CountOrders();
    }
}
