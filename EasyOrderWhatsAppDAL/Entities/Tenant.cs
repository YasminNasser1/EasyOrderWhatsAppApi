namespace EasyOrderWhatsAppDAL.Entities
{
    public class Tenant : BaseEntity
    {
        // المفتاح الفريد للمتجر اللي بيظهر في الـ URL، زي: brand-x أو store1
        public string StoreKey { get; set; } = null!;

        // اسم البراند التجاري للمتجر اللي بيظهر للعميل في رسالة الواتساب
        public string BrandName { get; set; } = "";

        // مفتاح EasyOrders Public API Key (لو المتجر مربوط بـ EasyOrders)
        public string? ApiKey { get; set; }

        // رابط الـ Webhook الخاص بنظام المتجر نفسه لإبلاغه بنتيجة تأكيد/إلغاء الطلب
        public string? CallbackUrl { get; set; }

        // حالة المتجر (مفعل / معطل)
        public bool IsActive { get; set; } = true;

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public void Update(string? brandName, string? apiKey, string? callbackUrl, bool? isActive = null)
        {
            if (!string.IsNullOrWhiteSpace(brandName))
                BrandName = brandName.Trim();

            if (apiKey != null)
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();

            if (callbackUrl != null)
                CallbackUrl = string.IsNullOrWhiteSpace(callbackUrl) ? null : callbackUrl.Trim();

            if (isActive.HasValue)
                IsActive = isActive.Value;

            MarkAsUpdated();
        }
    }
}
