using EasyOrderWhatsAppBLL.Constants;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class EasyOrdersApiService : IEasyOrdersApiService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<EasyOrdersApiService> logger;

        public EasyOrdersApiService(HttpClient httpClient, ILogger<EasyOrdersApiService> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        // 1. تحديث حالة الطلب على موقع EasyOrders (confirmed / canceled)
        public async Task<bool> UpdateOrderStatusAsync(EasyOrderReference order, string status, string apiKey)
        {
            var resolvedOrder = await ResolveOrderAsync(order, apiKey);
            if (resolvedOrder is null)
                return false;

            return await SendAsync(HttpMethod.Patch, EasyOrdersApiRoutes.OrderStatus(resolvedOrder.OrderUuid!), apiKey, new { status });
        }

        // 2. كتابة ملاحظة جوه الأوردر على موقع EasyOrders (زي سبب الإلغاء)
        public async Task<bool> AddOrderNoteAsync(EasyOrderReference order, string note, string apiKey)
        {
            var resolvedOrder = await ResolveOrderAsync(order, apiKey);
            if (resolvedOrder is null)
                return false;

            var body = new
            {
                order_id = resolvedOrder.OrderUuid,
                note,
                store_id = resolvedOrder.StoreId ?? "",
                type = "public"
            };
            return await SendAsync(HttpMethod.Post, EasyOrdersApiRoutes.OrderNotes, apiKey, body);
        }

        // لو معانا رقم الطلب القصير بس، بنسأل EasyOrders عن الـ Uuid والـ StoreId بتوعه
        private async Task<EasyOrderReference?> ResolveOrderAsync(EasyOrderReference order, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogWarning("⚠️ لم يتم إدخال مفتاح EasyOrders Public API Key بعد!");
                return null;
            }

            if (order.IsComplete)
                return order;

            string lookupUrl = order.HasValidUuid
                ? EasyOrdersApiRoutes.OrderById(order.OrderUuid!)
                : EasyOrdersApiRoutes.OrderByShortId(order.ShortId ?? order.OrderUuid ?? "");

            try
            {
                using var request = CreateRequest(HttpMethod.Get, lookupUrl, apiKey);
                using var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return order.HasValidUuid ? order : null;

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = document.RootElement;

                string? orderUuid = order.HasValidUuid ? order.OrderUuid : root.GetProperty("id").GetString();
                string? storeId = order.StoreId ?? (root.TryGetProperty("store_id", out var storeProp) ? storeProp.GetString() : null);

                return order with { OrderUuid = orderUuid, StoreId = storeId };
            }
            catch (Exception ex)
            {
                logger.LogError("❌ تعذر جلب بيانات الأوردر من EasyOrders: {Error}", ex.Message);
                return order.HasValidUuid ? order : null;
            }
        }

        // دالة واحدة لإرسال أي طلب لموقع EasyOrders بدل تكرار نفس الكود في كل دالة
        private async Task<bool> SendAsync(HttpMethod method, string url, string apiKey, object body)
        {
            try
            {
                using var request = CreateRequest(method, url, apiKey);
                request.Content = JsonContent.Create(body);

                using var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.LogError("❌ خطأ أثناء الاتصال بموقع EasyOrders: {Error}", ex.Message);
                return false;
            }
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add(EasyOrdersApiRoutes.ApiKeyHeader, apiKey);
            return request;
        }
    }
}
