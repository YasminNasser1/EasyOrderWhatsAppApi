namespace EasyOrderWhatsAppBLL.Constants
{
    // كل لينكات موقع EasyOrders في مكان واحد بدل ما تبقى مكررة في كذا ملف
    public static class EasyOrdersApiRoutes
    {
        public const string BaseUrl = "https://api.easy-orders.net/api/v1/external-apps";
        public const string ApiKeyHeader = "Api-Key";

        public static string OrderById(string orderUuid) => $"{BaseUrl}/orders/{orderUuid}";
        public static string OrderByShortId(string shortId) => $"{BaseUrl}/orders/short/{shortId}";
        public static string OrderStatus(string orderUuid) => $"{BaseUrl}/orders/{orderUuid}/status";
        public static string OrderNotes => $"{BaseUrl}/order-notes";
    }

    // الحالات اللي موقع EasyOrders بيفهمها
    public static class EasyOrdersStatuses
    {
        public const string Confirmed = "confirmed";
        public const string Canceled = "canceled";
    }
}
