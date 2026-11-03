namespace EasyOrderWhatsAppBLL.ViewModels.OrderViewModels
{
    // بيانات تعريف الأوردر على موقع EasyOrders (ممكن يكون معانا الـ Uuid أو رقم الطلب القصير بس)
    public record EasyOrderReference(string? OrderUuid, string? ShortId, string? StoreId)
    {
        public bool HasValidUuid => !string.IsNullOrWhiteSpace(OrderUuid) && OrderUuid.Contains('-');
        public bool IsComplete => HasValidUuid && !string.IsNullOrWhiteSpace(StoreId);
    }
}
