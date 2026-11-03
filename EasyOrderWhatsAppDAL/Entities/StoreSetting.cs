namespace EasyOrderWhatsAppDAL.Entities
{
    public class StoreSetting : BaseEntity
    {
        public string BrandName { get; set; } = "Outlix";
        public string ApiKey { get; set; } = "";
        public string WebhookSecret { get; set; } = "123456";
    }
}
