namespace EasyOrderWhatsAppBLL.Helpers
{
    public static class PhoneNumberHelper
    {
        // تنسيق رقم الموبايل المصري عشان الواتساب يقبله (مثال: 011xxxxxxxx -> 2011xxxxxxxx)
        public static string FormatEgyptianPhone(string phone)
        {
            string cleanPhone = new string(phone.Where(char.IsDigit).ToArray());

            bool isLocalEgyptianNumber = cleanPhone.StartsWith("01") && cleanPhone.Length == 11;
            return isLocalEgyptianNumber ? "2" + cleanPhone : cleanPhone;
        }
    }
}
