namespace EasyOrderWhatsAppBLL.ViewModels.OrderViewModels
{
    // البيانات اللي بنبعتها لبوابة الواتساب عشان تبعت رسالة التأكيد للعميل
    public class SendConfirmationViewModel
    {
        public string Phone { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string OrderUuid { get; set; } = "";
        public string OrderId { get; set; } = "";
        public string StoreId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string TenantId { get; set; } = "default";
    }
}
