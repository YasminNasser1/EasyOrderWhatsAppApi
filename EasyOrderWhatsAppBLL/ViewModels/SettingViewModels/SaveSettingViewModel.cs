namespace EasyOrderWhatsAppBLL.ViewModels.SettingViewModels
{
    public class SaveSettingViewModel
    {
        public string? ApiKey { get; set; }
        public string? BrandName { get; set; }
        public string? TenantId { get; set; } = "default";
    }
}
