using EasyOrderWhatsAppDAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EasyOrderWhatsAppDAL.Data.Contexts
{
    public class EasyOrderDbContext : DbContext
    {
        public EasyOrderDbContext(DbContextOptions<EasyOrderDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        #region DbSets

        public DbSet<Order> Orders { get; set; }
        public DbSet<StoreSetting> StoreSettings { get; set; }

        #endregion
    }
}
