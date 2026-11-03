using EasyOrderWhatsAppDAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyOrderWhatsAppDAL.Data.Configurations
{
    internal class OrderConfigurations : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.Property(X => X.CustomerName)
                   .HasMaxLength(200);

            builder.Property(X => X.Phone)
                   .HasMaxLength(20);

            builder.Property(X => X.ShortId)
                   .HasMaxLength(50);

            builder.Property(X => X.OrderUuid)
                   .HasMaxLength(100);
        }
    }
}
