namespace EasyOrderWhatsAppBLL.Services.Interfaces
{
    public interface IWhatsAppService
    {
        Task<bool> SendOrderConfirmationAsync(
            string phone,
            string message,
            string? orderUuid,
            string? shortId,
            string? storeId,
            string? customerName,
            string brandName);

        Task<bool> UpdateOrderStatusOnEasyOrdersAsync(string? orderUuid, string? shortId, string status, string apiKey);
        Task<bool> AddOrderNoteOnEasyOrdersAsync(string? orderUuid, string? shortId, string? storeId, string noteText, string apiKey);
    }
}
