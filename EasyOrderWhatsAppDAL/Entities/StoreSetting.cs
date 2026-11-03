namespace EasyOrderWhatsAppDAL.Entities
{
    public class StoreSetting : BaseEntity
    {
        public string BrandName { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string WebhookSecret { get; set; } = "123456";
    }
}
