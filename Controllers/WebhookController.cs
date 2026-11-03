using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // استقبال الأحداث من بره: أوردر جديد من EasyOrders + ضغطة زرار العميل من بوابة الواتساب
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly IOrderService orderService;

        public WebhookController(IOrderService orderService)
        {
            this.orderService = orderService;
        }

        // 1. استقبال الأوردر الجديد من موقع EasyOrders
        [HttpPost("easyorder")]
        [HttpPost("/")]
        public async Task<IActionResult> ReceiveEasyOrder()
        {
            using var reader = new StreamReader(Request.Body);
            string rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawBody))
                return Ok(new { success = true, message = "Webhook verified successfully" });

            bool isSent = await orderService.ProcessIncomingWebhookOrderAsync(rawBody);
            return Ok(new { success = isSent });
        }

        // 2. استقبال رد العميل من الواتساب (تأكيد / إلغاء / سبب الإلغاء / دعم)
        [HttpPost("order-action")]
        public async Task<IActionResult> HandleOrderAction([FromBody] OrderActionViewModel actionModel)
        {
            bool isHandled = await orderService.HandleCustomerActionAsync(actionModel);
            return Ok(new { success = isHandled });
        }

        // 3. للتأكد إن السيرفر شغال
        [HttpGet("easyorder")]
        public IActionResult HealthCheck()
        {
            return Ok("✅ السيرفر شغال بنظام 3-Tier Architecture (DAL + BLL + PL) وجاهز لاستقبال الأوردرات!");
        }
    }
}
