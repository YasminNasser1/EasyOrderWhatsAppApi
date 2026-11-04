using AutoMapper;
using EasyOrderWhatsAppBLL.Constants;
using EasyOrderWhatsAppBLL.Helpers;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Entities.Enums;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using System.Net.Http.Json;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly IWhatsAppGatewayService whatsAppGatewayService;
        private readonly IEasyOrdersApiService easyOrdersApiService;
        private readonly IStoreSettingService storeSettingService;
        private readonly IHttpClientFactory httpClientFactory;

        public OrderService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IWhatsAppGatewayService whatsAppGatewayService,
            IEasyOrdersApiService easyOrdersApiService,
            IStoreSettingService storeSettingService,
            IHttpClientFactory httpClientFactory)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.whatsAppGatewayService = whatsAppGatewayService;
            this.easyOrdersApiService = easyOrdersApiService;
            this.storeSettingService = storeSettingService;
            this.httpClientFactory = httpClientFactory;
        }

        #region 1. استقبال أوردر جديد من EasyOrders وإرسال رسالة التأكيد

        public async Task<bool> ProcessIncomingWebhookOrderAsync(string rawJson, string? tenantId = "default")
        {
            var orderVm = EasyOrderJsonParser.Parse(rawJson);
            if (orderVm is null)
                return true; // طلب اختبار فاضي من EasyOrders عند حفظ اللينك

            var activeTenant = NormalizeTenant(tenantId);
            orderVm.TenantId = activeTenant;
            await SaveNewOrderAsync(orderVm);

            string brandName = await storeSettingService.GetBrandNameAsync(activeTenant);
            var confirmation = new SendConfirmationViewModel
            {
                Phone = orderVm.Phone,
                Message = ConfirmationMessageBuilder.Build(orderVm, brandName),
                OrderUuid = orderVm.OrderUuid,
                OrderId = orderVm.ShortId,
                StoreId = orderVm.StoreId ?? "",
                CustomerName = orderVm.CustomerName,
                BrandName = brandName,
                TenantId = activeTenant
            };

            return await whatsAppGatewayService.SendOrderConfirmationAsync(confirmation);
        }

        // 2. استقبال أوردر جديد من أي موقع خارجي (Custom Website / Laravel / WordPress / Shopify)
        public async Task<bool> ProcessGenericOrderAsync(CustomOrderWebhookViewModel customOrder, string? tenantId = "default")
        {
            if (customOrder is null || string.IsNullOrWhiteSpace(customOrder.Phone))
                return false;

            var activeTenant = NormalizeTenant(tenantId);
            string formattedPhone = PhoneNumberHelper.FormatEgyptianPhone(customOrder.Phone);
            string orderId = !string.IsNullOrWhiteSpace(customOrder.OrderId)
                ? customOrder.OrderId.Trim()
                : new Random().Next(1000, 9999).ToString();

            string orderUuid = Guid.NewGuid().ToString();
            string savedBrand = await storeSettingService.GetBrandNameAsync(activeTenant);
            string brandName = !string.IsNullOrWhiteSpace(customOrder.BrandName)
                ? customOrder.BrandName.Trim()
                : savedBrand;

            var order = new Order
            {
                OrderUuid = orderUuid,
                ShortId = orderId,
                CustomerName = string.IsNullOrWhiteSpace(customOrder.CustomerName) ? "عميلنا العزيز" : customOrder.CustomerName.Trim(),
                Phone = formattedPhone,
                ProductsText = string.IsNullOrWhiteSpace(customOrder.Products) ? "منتجات الطلب" : customOrder.Products.Trim(),
                TotalCost = customOrder.TotalCost,
                CallbackUrl = customOrder.CallbackUrl?.Trim(),
                TenantId = activeTenant,
                Status = OrderStatus.Pending
            };

            await unitOfWork.OrderRepository.AddAsync(order);
            await unitOfWork.SaveChangesAsync();

            var orderVm = new CreateOrderViewModel
            {
                OrderUuid = orderUuid,
                ShortId = orderId,
                CustomerName = order.CustomerName,
                Phone = formattedPhone,
                ProductsText = order.ProductsText,
                TotalCost = order.TotalCost,
                TenantId = activeTenant,
                CallbackUrl = order.CallbackUrl
            };

            var confirmation = new SendConfirmationViewModel
            {
                Phone = formattedPhone,
                Message = ConfirmationMessageBuilder.Build(orderVm, brandName),
                OrderUuid = orderUuid,
                OrderId = orderId,
                StoreId = "",
                CustomerName = order.CustomerName,
                BrandName = brandName,
                TenantId = activeTenant
            };

            return await whatsAppGatewayService.SendOrderConfirmationAsync(confirmation);
        }

        private async Task SaveNewOrderAsync(CreateOrderViewModel orderVm)
        {
            var order = mapper.Map<Order>(orderVm);
            await unitOfWork.OrderRepository.AddAsync(order);
            await unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region 2. التعامل مع ضغطة زرار العميل (تأكيد / إلغاء / سبب الإلغاء / دعم)

        public async Task<bool> HandleCustomerActionAsync(OrderActionViewModel actionModel)
        {
            var localOrder = await unitOfWork.OrderRepository.GetByShortIdOrUuidAsync(actionModel.OrderId, actionModel.OrderUuid);
            var reference = new EasyOrderReference(actionModel.OrderUuid, actionModel.OrderId, actionModel.StoreId);
            var tenantKey = NormalizeTenant(localOrder?.TenantId ?? actionModel.TenantId);
            string apiKey = await storeSettingService.GetApiKeyAsync(tenantKey);

            return actionModel.Action?.ToLower() switch
            {
                CustomerActions.Confirm => await ConfirmOrderAsync(localOrder, reference, apiKey),
                CustomerActions.Cancel => await CancelOrderAsync(localOrder, reference, apiKey),
                CustomerActions.CancelReason => await SaveCancelReasonAsync(localOrder, reference, actionModel.Reason, apiKey),
                CustomerActions.Support => await RequestSupportAsync(localOrder, reference, apiKey),
                _ => false
            };
        }

        private async Task<bool> ConfirmOrderAsync(Order? localOrder, EasyOrderReference reference, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.Confirm());
            await NotifyCustomWebsiteCallbackAsync(localOrder, CustomerActions.Confirm);

            bool isUpdated = await easyOrdersApiService.UpdateOrderStatusAsync(reference, EasyOrdersStatuses.Confirmed, apiKey);
            await easyOrdersApiService.AddOrderNoteAsync(reference, OrderNotes.Confirmed, apiKey);
            return isUpdated;
        }

        private async Task<bool> CancelOrderAsync(Order? localOrder, EasyOrderReference reference, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.Cancel());
            await NotifyCustomWebsiteCallbackAsync(localOrder, CustomerActions.Cancel);

            return await easyOrdersApiService.UpdateOrderStatusAsync(reference, EasyOrdersStatuses.Canceled, apiKey);
        }

        private async Task<bool> SaveCancelReasonAsync(Order? localOrder, EasyOrderReference reference, string? reason, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.SetCancelReason(reason));
            await NotifyCustomWebsiteCallbackAsync(localOrder, CustomerActions.CancelReason);

            return await easyOrdersApiService.AddOrderNoteAsync(reference, OrderNotes.CancelReason(reason), apiKey);
        }

        private async Task<bool> RequestSupportAsync(Order? localOrder, EasyOrderReference reference, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.RequestSupport());
            await NotifyCustomWebsiteCallbackAsync(localOrder, CustomerActions.Support);

            return await easyOrdersApiService.AddOrderNoteAsync(reference, OrderNotes.SupportRequested, apiKey);
        }

        // لو الأوردر جاي من موقع خارجي (Custom Website) وفيه رابط Callback، بنبعت لموقعهم إشعار بالحالة الجديدة
        private async Task NotifyCustomWebsiteCallbackAsync(Order? order, string action)
        {
            if (order is null || string.IsNullOrWhiteSpace(order.CallbackUrl))
                return;

            try
            {
                var client = httpClientFactory.CreateClient();
                var payload = new
                {
                    orderId = order.ShortId,
                    orderUuid = order.OrderUuid,
                    status = order.Status.ToString(),
                    action,
                    cancelReason = order.CancelReason,
                    updatedAt = DateTime.Now
                };
                await client.PostAsJsonAsync(order.CallbackUrl, payload);
            }
            catch { }
        }

        // دالة واحدة لتحديث الأوردر في الداتابيز بدل تكرار نفس الـ 3 سطور في كل أكشن
        private async Task UpdateLocalOrderAsync(Order? order, Action<Order> applyChange)
        {
            if (order is null)
                return;

            applyChange(order);
            unitOfWork.OrderRepository.Update(order);
            await unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region 3. عرض الأوردرات

        public IEnumerable<OrderViewModel> GetAllOrders(string? tenantId = null)
        {
            var query = unitOfWork.OrderRepository.GetAll();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var key = NormalizeTenant(tenantId);
                query = query.Where(o => o.TenantId != null && o.TenantId.ToLower() == key);
            }
            return mapper.Map<IEnumerable<OrderViewModel>>(query);
        }

        public async Task<OrderViewModel?> GetOrderAsync(int id)
        {
            var order = await unitOfWork.OrderRepository.GetByIdAsync(id);
            return order is null ? null : mapper.Map<OrderViewModel>(order);
        }

        private static string NormalizeTenant(string? tenantId) =>
            string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId.Trim().ToLower();

        #endregion
    }
}
