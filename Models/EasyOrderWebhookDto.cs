using System.Text.Json.Serialization;

namespace EasyOrderWhatsAppApi.Models
{
    public class EasyOrderWebhookDto
    {
        [JsonPropertyName("id")]
        public string? OrderId { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("gov")]
        public string? Governorate { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("cost")]
        public decimal SubTotal { get; set; }

        [JsonPropertyName("shipping_cost")]
        public decimal ShippingCost { get; set; }

        [JsonPropertyName("total_cost")]
        public decimal TotalCost { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("cart_items")]
        public List<CartItemDto>? CartItems { get; set; }
    }

    public class CartItemDto
    {
        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("product")]
        public ProductDto? Product { get; set; }
    }

    public class ProductDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
