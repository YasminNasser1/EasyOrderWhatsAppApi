using EasyOrderWhatsAppBLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // عرض صفحة الواتساب (QR Code) على بورت 5055 + تسجيل الخروج
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        public const string DashboardRoute = "/whatsapp";
        private const string GatewayStartingHtml =
            "<h2 style='font-family:sans-serif;text-align:center;margin-top:50px;'>⏳ جاري تشغيل بوابة الواتساب... حدث الصفحة بعد 5 ثواني</h2>";

        private readonly IWhatsAppGatewayService whatsAppGatewayService;

        public WhatsAppController(IWhatsAppGatewayService whatsAppGatewayService)
        {
            this.whatsAppGatewayService = whatsAppGatewayService;
        }

        [HttpGet(DashboardRoute)]
        public async Task<IActionResult> Dashboard()
        {
            string html = await whatsAppGatewayService.GetDashboardHtmlAsync() ?? GatewayStartingHtml;
            return Content(html, "text/html; charset=utf-8");
        }

        [AcceptVerbs("GET", "POST", Route = "/logout")]
        [AcceptVerbs("GET", "POST", Route = DashboardRoute + "/logout")]
        public async Task<IActionResult> Logout()
        {
            await whatsAppGatewayService.LogoutAsync();
            return Redirect(DashboardRoute);
        }
    }
}
