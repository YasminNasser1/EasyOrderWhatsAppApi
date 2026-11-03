using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using System.Text.Json;

namespace EasyOrderWhatsAppBLL.Helpers
{
    public static class OrderHelperMethod
    {
        // 1. دالة بسيطة لتنسيق رقم الموبايل المصري عشان الواتساب يقبله (مثال: 011 -> 2011)
        public static string FormatEgyptianPhone(string phone)
        {
            phone = phone.Trim().Replace(" ", "").Replace("+", "").Replace("-", "");

            if (phone.StartsWith("01") && phone.Length == 11)
            {
                return "2" + phone;
            }
            return phone;
        }

        // 2. دالة تجهيز نص رسالة الواتساب بنفس شكل موقع Confirm-It بشكل ديناميكي لأي براند
        public static string BuildConfirmationMessage(CreateOrderViewModel order, string brandName)
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

        // 3. دالة استخراج بيانات الأوردر من الـ JSON اللي جاي من موقع EasyOrders بطريقة سهلة
        public static CreateOrderViewModel? ParseEasyOrderJson(string rawJson)
        {
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            JsonElement root = doc.RootElement;

            string? phone = GetStringProperty(root, "phone");
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            string orderUuid = GetStringProperty(root, "id") ?? "";
            string shortId = GetStringProperty(root, "short_id") ?? orderUuid;
            string? storeId = GetStringProperty(root, "store_id");
            string fullName = GetStringProperty(root, "full_name") ?? "عميلنا العزيز";
            string totalStr = GetStringProperty(root, "total_cost") ?? "0";
            decimal.TryParse(totalStr, out decimal totalCost);

            // تجميع أسماء المنتجات والمقاس واللون
            var productsList = new List<string>();
            if (root.TryGetProperty("cart_items", out JsonElement cartItems) && cartItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cartItems.EnumerateArray())
                {
                    string qty = GetStringProperty(item, "quantity") ?? "1";
                    string productName = "منتج";

                    if (item.TryGetProperty("product", out JsonElement productObj) && productObj.ValueKind == JsonValueKind.Object)
                    {
                        productName = GetStringProperty(productObj, "name") ?? "منتج";
                    }

                    // استخراج المقاس واللون لو موجودين
                    var variantProps = new List<string>();
                    if (item.TryGetProperty("variant", out JsonElement variantObj) && variantObj.ValueKind == JsonValueKind.Object)
                    {
                        if (variantObj.TryGetProperty("variation_props", out JsonElement propsArray) && propsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var prop in propsArray.EnumerateArray())
                            {
                                string? val = GetStringProperty(prop, "variation_prop");
                                if (!string.IsNullOrWhiteSpace(val))
                                    variantProps.Add(val);
                            }
                        }
                    }

                    string variantText = variantProps.Any() ? " - " + string.Join(" - ", variantProps) : "";
                    string qtyText = qty != "1" ? $" (x{qty})" : "";
                    productsList.Add($"{productName}{variantText}{qtyText}");
                }
            }

            return new CreateOrderViewModel
            {
                OrderUuid = orderUuid,
                ShortId = shortId,
                StoreId = storeId,
                CustomerName = fullName,
                Phone = FormatEgyptianPhone(phone),
                ProductsText = productsList.Any() ? string.Join("\n", productsList) : "منتجات الطلب",
                TotalCost = totalCost
            };
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement val))
            {
                if (val.ValueKind == JsonValueKind.String)
                    return val.GetString();
                if (val.ValueKind == JsonValueKind.Number)
                    return val.GetRawText();
            }
            return null;
        }
    }
}
