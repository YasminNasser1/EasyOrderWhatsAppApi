using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // إعدادات المتجر (اسم البراند + مفتاح EasyOrders API)
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IStoreSettingService storeSettingService;

        public SettingsController(IStoreSettingService storeSettingService)
        {
            this.storeSettingService = storeSettingService;
        }

        [HttpGet("/api/webhook/apikey-status")]
        public async Task<IActionResult> GetStatus()
        {
            return Ok(await storeSettingService.GetStatusAsync());
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
            return Redirect(WhatsAppController.DashboardRoute);
        }
    }
}
