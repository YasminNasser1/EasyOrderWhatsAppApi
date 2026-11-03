using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;

namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    // التعامل مع موقع EasyOrders نفسه (تحديث حالة الأوردر + إضافة ملاحظات)
    public interface IEasyOrdersApiService
    {
        Task<bool> UpdateOrderStatusAsync(EasyOrderReference order, string status, string apiKey);
        Task<bool> AddOrderNoteAsync(EasyOrderReference order, string note, string apiKey);
    }
}
