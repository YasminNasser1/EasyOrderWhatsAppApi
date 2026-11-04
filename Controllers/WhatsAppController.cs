using EasyOrderWhatsAppBLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // عرض صفحة الواتساب (QR Code أو متصل) على بورت 5055 لكل تاجر على حدة
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        public const string DashboardRoute = "/whatsapp";
        private const string GatewayStartingHtml =
            "<h2 style='font-family:sans-serif;text-align:center;margin-top:50px;'>⏳ جاري تشغيل بوابة الواتساب... حدث الصفحة بعد ثوانٍ</h2>";

        private static readonly HashSet<string> ReservedRoutes = new(StringComparer.OrdinalIgnoreCase)
        {
            "api", "whatsapp", "save-apikey", "logout", "swagger", "favicon.ico"
        };

        private readonly IWhatsAppGatewayService whatsAppGatewayService;

        public WhatsAppController(IWhatsAppGatewayService whatsAppGatewayService)
        {
            this.whatsAppGatewayService = whatsAppGatewayService;
        }

        // لوحة التحكم للتاجر الافتراضي
        [HttpGet(DashboardRoute)]
        public async Task<IActionResult> Dashboard()
        {
            string html = await whatsAppGatewayService.GetDashboardHtmlAsync("default") ?? GatewayStartingHtml;
            return Content(html, "text/html; charset=utf-8");
        }

        // لوحة تحكم مستقلة لتاجر معين: /whatsapp/{tenantKey} أو /{tenantKey}
        [HttpGet(DashboardRoute + "/{tenantKey}")]
        [HttpGet("/{tenantKey}")]
        public async Task<IActionResult> TenantDashboard([FromRoute] string tenantKey)
        {
            if (ReservedRoutes.Contains(tenantKey))
            {
                return NotFound();
            }

            string html = await whatsAppGatewayService.GetDashboardHtmlAsync(tenantKey) ?? GatewayStartingHtml;
            return Content(html, "text/html; charset=utf-8");
        }

        // تسجيل الخروج للتاجر الافتراضي
        [AcceptVerbs("GET", "POST", Route = "/logout")]
        [AcceptVerbs("GET", "POST", Route = DashboardRoute + "/logout")]
        public async Task<IActionResult> Logout()
        {
            await whatsAppGatewayService.LogoutAsync("default");
            return Redirect(DashboardRoute);
        }

        // تسجيل الخروج لتاجر معين
        [AcceptVerbs("GET", "POST", Route = "/logout/{tenantKey}")]
        [AcceptVerbs("GET", "POST", Route = DashboardRoute + "/logout/{tenantKey}")]
        public async Task<IActionResult> TenantLogout([FromRoute] string tenantKey)
        {
            await whatsAppGatewayService.LogoutAsync(tenantKey);
            return Redirect($"{DashboardRoute}/{tenantKey}");
        }
    }
}
