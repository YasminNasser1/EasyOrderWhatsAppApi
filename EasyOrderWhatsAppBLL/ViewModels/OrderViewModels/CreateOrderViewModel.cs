namespace EasyOrderWhatsAppBLL.ViewModels.OrderViewModels
{
    public class CreateOrderViewModel
    {
        public string OrderUuid { get; set; } = null!;
        public string ShortId { get; set; } = null!;
        public string? StoreId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string ProductsText { get; set; } = null!;
        public decimal TotalCost { get; set; }
        public string? TenantId { get; set; } = "default";
        public string? CallbackUrl { get; set; }
    }
}
