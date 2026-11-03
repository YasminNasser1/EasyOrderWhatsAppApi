namespace EasyOrderWhatsAppDAL.Data.FileStorage
{
    // حفظ إعدادات المتجر في ملفات نصية عشان تفضل موجودة بعد ما نقفل ونفتح الجهاز
    // (بوابة الواتساب Node.js بتقرا ملف الـ API Key ده كمان عشان المزامنة التلقائية)
    public interface ISettingsFileStore
    {
        string ReadApiKey();
        string ReadBrandName();
        void Save(string? apiKey, string? brandName);
    }
}
