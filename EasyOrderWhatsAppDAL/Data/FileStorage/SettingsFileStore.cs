namespace EasyOrderWhatsAppDAL.Data.FileStorage
{
    public class SettingsFileStore : ISettingsFileStore
    {
        private const string ApiKeyFileName = "easyorders_apikey.txt";
        private const string BrandNameFileName = "easyorders_brandname.txt";

        private readonly string folderPath;

        public SettingsFileStore(string folderPath)
        {
            this.folderPath = folderPath;
        }

        public string ReadApiKey() => ReadFile(ApiKeyFileName);

        public string ReadBrandName() => ReadFile(BrandNameFileName);

        public void Save(string? apiKey, string? brandName)
        {
            WriteFileIfNotEmpty(ApiKeyFileName, apiKey);
            WriteFileIfNotEmpty(BrandNameFileName, brandName);
        }

        private string ReadFile(string fileName)
        {
            string fullPath = Path.Combine(folderPath, fileName);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath).Trim() : "";
        }

        private void WriteFileIfNotEmpty(string fileName, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            File.WriteAllText(Path.Combine(folderPath, fileName), value.Trim());
        }
    }
}
