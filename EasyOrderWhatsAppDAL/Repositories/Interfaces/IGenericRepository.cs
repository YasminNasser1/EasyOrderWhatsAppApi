using EasyOrderWhatsAppDAL.Entities;
using System.Linq.Expressions;

namespace EasyOrderWhatsAppDAL.Repositories.Interfaces
{
    public interface IGenericRepository<TEntity> where TEntity : BaseEntity
    {
        IQueryable<TEntity> GetAll(Expression<Func<TEntity, bool>>? condition = default);
        Task<TEntity?> GetByIdAsync(int id);
        Task AddAsync(TEntity entity);
        void Update(TEntity entity);
        void Delete(TEntity entity);
    }
}
