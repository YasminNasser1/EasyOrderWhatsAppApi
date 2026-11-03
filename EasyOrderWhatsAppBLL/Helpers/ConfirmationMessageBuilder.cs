using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;

namespace EasyOrderWhatsAppBLL.Helpers
{
    public static class ConfirmationMessageBuilder
    {
        // تجهيز نص رسالة الواتساب بنفس شكل موقع Confirm-It لأي براند
        public static string Build(CreateOrderViewModel order, string brandName)
        {
            string brandDisplay = string.IsNullOrWhiteSpace(brandName) ? "متجرنا" : $"براند {brandName}";

            return
                $"أهلاً أ/{order.CustomerName} 👋\n" +
                $"حضرتك طلبت من {brandDisplay} :\n\n" +
                $"{order.ProductsText}\n\n" +
                $"إجمالي الطلب: {order.TotalCost:0.00} ج.م\n" +
                $"رقم الطلب: {order.ShortId}\n\n" +
                $"من فضلك أكد علشان نرتب التوصيل:";
        }
    }
}
