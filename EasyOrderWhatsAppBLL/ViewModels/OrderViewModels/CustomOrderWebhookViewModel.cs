namespace EasyOrderWhatsAppBLL.ViewModels.OrderViewModels
{
    // نموذج استقبال طلب جديد من أي موقع خارجي (Custom Website / Laravel / WordPress / WooCommerce / Shopify)
    public class CustomOrderWebhookViewModel
    {
        // رقم الأوردر في موقع العميل (مثال: "1001" أو "ORD-5432")
        public string? OrderId { get; set; }

        // اسم العميل
        public string CustomerName { get; set; } = "عميلنا العزيز";

        // رقم موبايل العميل
        public string Phone { get; set; } = null!;

        // نص المنتجات وتفاصيلها (اختياري: مثال "تيشيرت أسود L (x1)")
        public string? Products { get; set; }

        // إجمالي سعر الطلب بالجنيه
        public decimal TotalCost { get; set; }

        // اسم البراند (اختياري: لو مش مبعوت بناخد اسم البراند المحفوظ في الإعدادات أو بروفايل الواتساب)
        public string? BrandName { get; set; }

        // رابط اختياري لموقع العميل: لما العميل يضغط (تأكيد أو إلغاء)، بنبعت إشعار فوري للرابط ده عشان يحدث الأوردر عندهم تلقائياً!
        public string? CallbackUrl { get; set; }
    }
}
