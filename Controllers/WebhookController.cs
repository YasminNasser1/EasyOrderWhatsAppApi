using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // استقبال الأحداث: أوردرات من EasyOrders أو مواقع مخصصة + ضغطة زرار العميل من بوابة الواتساب
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly IOrderService orderService;

        public WebhookController(IOrderService orderService)
        {
            this.orderService = orderService;
        }

        // 1. استقبال أوردر جديد من EasyOrders (للتاجر الافتراضي أو تاجر محدد)
        [HttpPost("easyorder")]
        [HttpPost("easyorder/{tenantKey}")]
        [HttpPost("/")]
        public async Task<IActionResult> ReceiveEasyOrder([FromRoute] string? tenantKey = "default")
        {
            using var reader = new StreamReader(Request.Body);
            string rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawBody))
                return Ok(new { success = true, message = "Webhook verified successfully" });

            bool isSent = await orderService.ProcessIncomingWebhookOrderAsync(rawBody, tenantKey);
            return Ok(new { success = isSent, tenant = tenantKey });
        }

        // 2. استقبال أوردر جديد من أي موقع خارجي مخصص (Custom Website / Laravel / WordPress / Shopify)
        [HttpPost("custom")]
        [HttpPost("order")]
        [HttpPost("custom/{tenantKey}")]
        [HttpPost("order/{tenantKey}")]
        public async Task<IActionResult> ReceiveCustomOrder(
            [FromBody] CustomOrderWebhookViewModel model,
            [FromRoute] string? tenantKey = "default")
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Phone))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم هاتف العميل مطلوب (phone is required)"
                });
            }

            bool isSent = await orderService.ProcessGenericOrderAsync(model, tenantKey);
            return Ok(new
            {
                success = isSent,
                tenant = tenantKey,
                message = isSent
                    ? $"✅ تم استلام الطلب للتاجر ({tenantKey}) وإرسال رسالة تأكيد الواتساب بنجاح!"
                    : $"⚠️ تم استلام الطلب ولكن تعذر إرسال رسالة الواتساب للتاجر ({tenantKey}) (تأكدي من ربط رقم الواتساب الخاص بالتاجر)"
            });
        }

        // 3. استقبال رد وضغطات أزرار العميل من بوابة الواتساب (تأكيد / إلغاء / سبب الإلغاء / دعم)
        [HttpPost("order-action")]
        public async Task<IActionResult> HandleOrderAction([FromBody] OrderActionViewModel actionModel)
        {
            bool isHandled = await orderService.HandleCustomerActionAsync(actionModel);
            return Ok(new { success = isHandled });
        }

        // 4. فحص صحة الخدمة
        [HttpGet("easyorder")]
        public IActionResult HealthCheck()
        {
            return Ok("✅ منصة الواتساب المتعددة (Multi-Tenant SaaS) شغالة وجاهزة لاستقبال الأوردرات لجميع التجار!");
        }
    }
}
