using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using System.Text.Json;

namespace EasyOrderWhatsAppBLL.Helpers
{
    public static class EasyOrderJsonParser
    {
        private const string DefaultCustomerName = "عميلنا العزيز";
        private const string DefaultProductName = "منتج";

        // استخراج بيانات الأوردر من الـ JSON اللي جاي من موقع EasyOrders
        public static CreateOrderViewModel? Parse(string rawJson)
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            JsonElement root = document.RootElement;

            string? phone = root.GetText("phone");
            if (string.IsNullOrWhiteSpace(phone))
                return null; // طلب اختبار فاضي من EasyOrders

            string orderUuid = root.GetText("id") ?? "";
            decimal.TryParse(root.GetText("total_cost"), out decimal totalCost);

            return new CreateOrderViewModel
            {
                OrderUuid = orderUuid,
                ShortId = root.GetText("short_id") ?? orderUuid,
                StoreId = root.GetText("store_id"),
                CustomerName = root.GetText("full_name") ?? DefaultCustomerName,
                Phone = PhoneNumberHelper.FormatEgyptianPhone(phone),
                ProductsText = BuildProductsText(root),
                TotalCost = totalCost
            };
        }

        private static string BuildProductsText(JsonElement root)
        {
            if (!root.TryGetProperty("cart_items", out JsonElement cartItems) || cartItems.ValueKind != JsonValueKind.Array)
                return "منتجات الطلب";

            var lines = cartItems.EnumerateArray().Select(BuildProductLine).ToList();
            return lines.Any() ? string.Join("\n", lines) : "منتجات الطلب";
        }

        // مثال للنتيجة: "تيشيرت - أسود - XL (x2)"
        private static string BuildProductLine(JsonElement item)
        {
            string productName = item.GetChild("product")?.GetText("name") ?? DefaultProductName;
            string quantity = item.GetText("quantity") ?? "1";

            var variantValues = GetVariantValues(item);
            string variantText = variantValues.Any() ? " - " + string.Join(" - ", variantValues) : "";
            string quantityText = quantity != "1" ? $" (x{quantity})" : "";

            return $"{productName}{variantText}{quantityText}";
        }

        private static List<string> GetVariantValues(JsonElement item)
        {
            JsonElement? variant = item.GetChild("variant");
            if (variant is null || !variant.Value.TryGetProperty("variation_props", out JsonElement props) || props.ValueKind != JsonValueKind.Array)
                return new List<string>();

            return props.EnumerateArray()
                        .Select(prop => prop.GetText("variation_prop"))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .ToList();
        }

        #region JsonElement Extensions (عشان قراءة الـ JSON تبقى أسهل وأوضح)

        private static string? GetText(this JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        private static JsonElement? GetChild(this JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out JsonElement child) &&
                child.ValueKind == JsonValueKind.Object)
            {
                return child;
            }
            return null;
        }

        #endregion
    }
}
