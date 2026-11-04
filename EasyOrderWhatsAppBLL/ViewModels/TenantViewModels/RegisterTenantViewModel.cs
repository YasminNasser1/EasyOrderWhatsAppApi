using System.ComponentModel.DataAnnotations;

namespace EasyOrderWhatsAppBLL.ViewModels.TenantViewModels
{
    public class RegisterTenantViewModel
    {
        [Required(ErrorMessage = "معرف المتجر (StoreKey) مطلوب")]
        [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "معرف المتجر يجب أن يحتوي على حروف إنجليزية وأرقام وعلامات - أو _ فقط")]
        public string StoreKey { get; set; } = null!;

        [Required(ErrorMessage = "اسم البراند التجاري مطلوب")]
        public string BrandName { get; set; } = null!;

        public string? ApiKey { get; set; }

        public string? CallbackUrl { get; set; }
    }
}
