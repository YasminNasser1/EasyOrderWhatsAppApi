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
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string? CancelReason { get; set; }
        public string? CallbackUrl { get; set; }
        public string? TenantId { get; set; } = "default";

        #region Behaviors (Encapsulation: الأوردر هو اللي بيغير حالته بنفسه)

        public void Confirm() => ChangeStatus(OrderStatus.Confirmed);

        public void Cancel() => ChangeStatus(OrderStatus.Canceled);

        public void RequestSupport() => ChangeStatus(OrderStatus.SupportRequested);

        public void SetCancelReason(string? reason)
        {
            CancelReason = reason;
            MarkAsUpdated();
        }

        private void ChangeStatus(OrderStatus newStatus)
        {
            Status = newStatus;
            MarkAsUpdated();
        }

        #endregion
    }
}
