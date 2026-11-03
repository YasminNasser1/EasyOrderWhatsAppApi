namespace EasyOrderWhatsAppBLL.Constants
{
    // الأكشنز اللي بوابة الواتساب بتبعتها لما العميل يضغط على زرار
    public static class CustomerActions
    {
        public const string Confirm = "confirm";
        public const string Cancel = "cancel";
        public const string CancelReason = "cancel_reason";
        public const string Support = "support";
    }

    // الملاحظات اللي بتتكتب جوه الأوردر على موقع EasyOrders
    public static class OrderNotes
    {
        public const string Confirmed = "✅ تم تأكيد الطلب من العميل عبر الواتساب";
        public const string SupportRequested = "📞 طلب العميل التواصل مع خدمة العملاء عبر الواتساب";

        public static string CancelReason(string? reason) => $"❌ سبب إلغاء الطلب (من العميل عبر واتساب): {reason}";
    }
}
