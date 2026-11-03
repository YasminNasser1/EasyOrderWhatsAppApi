using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EasyOrderWhatsAppPL.Controllers
{
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IOrderService orderService;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IWebHostEnvironment env;

        public WebhookController(
            IOrderService orderService,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment env)
        {
            this.orderService = orderService;
            this.httpClientFactory = httpClientFactory;
            this.env = env;
        }

        // 1. استقبال الأوردر الجديد من موقع EasyOrders (Webhook)
        [HttpPost("/api/webhook/easyorder")]
        [HttpPost("/")]
        public async Task<IActionResult> ReceiveEasyOrder()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            string rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Ok(new { success = true, message = "Webhook verified successfully" });
            }

            bool result = await orderService.ProcessIncomingWebhookOrder(rawBody);
            return Ok(new { success = result });
        }

        // 2. استقبال رد العميل من الواتساب (تأكيد / إلغاء / سبب الإلغاء / دعم) وتحديث الموقع
        [HttpPost("/api/webhook/order-action")]
        public async Task<IActionResult> HandleOrderAction([FromBody] OrderActionViewModel viewModel)
        {
            if (viewModel is null)
                return BadRequest(new { success = false });

            bool result = await orderService.HandleCustomerOrderAction(viewModel);
            return Ok(new { success = result });
        }

        // 3. عرض كل الأوردرات المسجلة في السيستم
        [HttpGet("/api/orders")]
        public IActionResult Index()
        {
            var model = orderService.GetAllOrders();
            return Ok(model);
        }

        // 4. قراءة وحفظ إعدادات المتجر (اسم البراند + مفتاح الـ Public API)
        [HttpGet("/api/webhook/apikey-status")]
        public async Task<IActionResult> GetApiKeyStatus()
        {
            var status = await orderService.GetStoreSettingStatus();
            return Ok(new
            {
                hasKey = status.hasKey,
                maskedKey = status.maskedKey,
                brandName = status.brandName
            });
        }

        [HttpPost("/api/webhook/save-apikey")]
        public async Task<IActionResult> SaveApiKey([FromBody] SaveSettingViewModel viewModel)
        {
            if (viewModel is null)
                return BadRequest(new { success = false });

            bool result = await orderService.SaveStoreSetting(viewModel);

            // حفظ نسخة في ملف نصي عشان تفضل محفوظة دايماً لما نقفل ونفتح الجهاز
            if (!string.IsNullOrWhiteSpace(viewModel.ApiKey))
                System.IO.File.WriteAllText(Path.Combine(env.ContentRootPath, "easyorders_apikey.txt"), viewModel.ApiKey.Trim());

            if (!string.IsNullOrWhiteSpace(viewModel.BrandName))
                System.IO.File.WriteAllText(Path.Combine(env.ContentRootPath, "easyorders_brandname.txt"), viewModel.BrandName.Trim());

            return Ok(new { success = result });
        }

        [HttpGet("/api/webhook/easyorder")]
        public IActionResult HealthCheck()
        {
            return Ok("✅ السيرفر شغال بنظام 3-Tier Architecture (DAL + BLL + PL) وجاهز لاستقبال الأوردرات!");
        }

        [HttpGet("/whatsapp")]
        public async Task<IActionResult> WhatsAppDashboard()
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                string html = await client.GetStringAsync("http://localhost:3001/");
                return Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                return Content("<h2 style='font-family:sans-serif;text-align:center;margin-top:50px;'>⏳ جاري تشغيل بوابة الواتساب... حدث الصفحة بعد 5 ثواني</h2>", "text/html; charset=utf-8");
            }
        }

        [HttpGet("/logout")]
        [HttpPost("/logout")]
        [HttpGet("/whatsapp/logout")]
        [HttpPost("/whatsapp/logout")]
        public async Task<IActionResult> LogoutWhatsApp()
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                await client.PostAsync("http://localhost:3001/logout", null);
            }
            catch { }

            return Redirect("/whatsapp");
        }

        [HttpPost("/save-apikey")]
        public async Task<IActionResult> SaveApiKeyFromForm([FromForm] string? brandName, [FromForm] string? apiKey)
        {
            var viewModel = new SaveSettingViewModel
            {
                BrandName = brandName,
                ApiKey = apiKey
            };

            await orderService.SaveStoreSetting(viewModel);

            if (!string.IsNullOrWhiteSpace(apiKey))
                System.IO.File.WriteAllText(Path.Combine(env.ContentRootPath, "easyorders_apikey.txt"), apiKey.Trim());

            if (!string.IsNullOrWhiteSpace(brandName))
                System.IO.File.WriteAllText(Path.Combine(env.ContentRootPath, "easyorders_brandname.txt"), brandName.Trim());

            return Redirect("/whatsapp");
        }
    }
}
