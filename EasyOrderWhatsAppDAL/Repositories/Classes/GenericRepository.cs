using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EasyOrderWhatsAppDAL.Repositories.Classes
{
    public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : BaseEntity
    {
        protected readonly EasyOrderDbContext dbContext;

        public GenericRepository(EasyOrderDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public IQueryable<TEntity> GetAll(Expression<Func<TEntity, bool>>? condition = default)
        {
            IQueryable<TEntity> query = dbContext.Set<TEntity>();
            if (condition is not null)
            {
                query = query.Where(condition);
            }
            return query.OrderByDescending(entity => entity.CreatedAt);
        }

        public async Task<TEntity?> GetByIdAsync(int id)
        {
            return await dbContext.Set<TEntity>().FirstOrDefaultAsync(entity => entity.Id == id);
        }

        public async Task AddAsync(TEntity entity)
        {
            await dbContext.Set<TEntity>().AddAsync(entity);
        }

        public void Update(TEntity entity)
        {
            dbContext.Set<TEntity>().Update(entity);
        }

        public void Delete(TEntity entity)
        {
            dbContext.Set<TEntity>().Remove(entity);
        }
    }
}
