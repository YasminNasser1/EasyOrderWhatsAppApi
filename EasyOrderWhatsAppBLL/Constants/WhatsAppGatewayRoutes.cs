namespace EasyOrderWhatsAppBLL.Constants
{
    // عنوان بوابة الواتساب (Node.js) اللي شغالة على نفس الجهاز
    public static class WhatsAppGatewayRoutes
    {
        public const string BaseUrl = "http://localhost:3001";
        public const string Dashboard = "/";
        public const string SendMessage = "/send-message";
        public const string Logout = "/logout";
    }
}
