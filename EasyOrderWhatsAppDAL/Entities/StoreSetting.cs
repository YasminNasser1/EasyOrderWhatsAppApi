namespace EasyOrderWhatsAppDAL.Entities
{
    public class StoreSetting : BaseEntity
    {
        public string BrandName { get; set; } = "";
        public string ApiKey { get; set; } = "";

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        // تحديث الإعدادات: أي قيمة فاضية بنسيب القديمة زي ما هي
        public void Update(string? apiKey, string? brandName)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
                ApiKey = apiKey.Trim();

            if (!string.IsNullOrWhiteSpace(brandName))
                BrandName = brandName.Trim();

            MarkAsUpdated();
        }
    }
}
