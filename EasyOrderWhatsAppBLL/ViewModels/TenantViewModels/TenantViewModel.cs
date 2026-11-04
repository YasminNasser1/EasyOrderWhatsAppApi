namespace EasyOrderWhatsAppBLL.ViewModels.TenantViewModels
{
    public class TenantViewModel
    {
        public int Id { get; set; }
        public string StoreKey { get; set; } = null!;
        public string BrandName { get; set; } = "";
        public string? ApiKey { get; set; }
        public string MaskedKey => string.IsNullOrWhiteSpace(ApiKey) || ApiKey.Length <= 8
            ? "غير مضبوط"
            : $"{ApiKey[..4]}...{ApiKey[^4..]}";
        public string? CallbackUrl { get; set; }
        public bool IsActive { get; set; }
        public string WhatsAppDashboardUrl => $"/whatsapp/{StoreKey}";
        public string WebhookUrl => $"/api/webhook/order/{StoreKey}";
        public DateTime CreatedAt { get; set; }
    }
}
