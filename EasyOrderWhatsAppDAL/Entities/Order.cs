using EasyOrderWhatsAppDAL.Entities.Enums;

namespace EasyOrderWhatsAppDAL.Entities
{
    public class Order : BaseEntity
    {
        public string OrderUuid { get; set; } = null!;
        public string ShortId { get; set; } = null!;
        public string? StoreId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string ProductsText { get; set; } = null!;
        public decimal TotalCost { get; set; }
        public Status Status { get; set; } = Status.انتظار;
        public string? CancelReason { get; set; }
    }
}
