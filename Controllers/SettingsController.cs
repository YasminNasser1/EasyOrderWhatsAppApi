using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // إعدادات المتجر (اسم البراند + مفتاح EasyOrders API) - يدعم التاجر الافتراضي أو تاجر محدد
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IStoreSettingService storeSettingService;

        public SettingsController(IStoreSettingService storeSettingService)
        {
            this.storeSettingService = storeSettingService;
        }

        [HttpGet("/api/webhook/apikey-status")]
        public async Task<IActionResult> GetStatus([FromQuery] string? tenantId = "default")
        {
            return Ok(await storeSettingService.GetStatusAsync(tenantId));
        }

        // بوابة الواتساب بتبعت الإعدادات كـ JSON
        [HttpPost("/api/webhook/save-apikey")]
        public async Task<IActionResult> SaveFromJson([FromBody] SaveSettingViewModel model)
        {
            bool isSaved = await storeSettingService.SaveAsync(model);
            return Ok(new { success = isSaved });
        }

        // فورم الإعدادات في صفحة الواتساب (لما تكون مفتوحة من بورت 5055)
        [HttpPost("/save-apikey")]
        public async Task<IActionResult> SaveFromForm([FromForm] SaveSettingViewModel model)
        {
            await storeSettingService.SaveAsync(model);
            string redirectUrl = string.IsNullOrWhiteSpace(model.TenantId) || model.TenantId == "default"
                ? WhatsAppController.DashboardRoute
                : $"{WhatsAppController.DashboardRoute}/{model.TenantId}";
            return Redirect(redirectUrl);
        }
    }
}
