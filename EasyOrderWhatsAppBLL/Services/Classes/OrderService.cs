using AutoMapper;
using EasyOrderWhatsAppBLL.Constants;
using EasyOrderWhatsAppBLL.Helpers;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly IWhatsAppGatewayService whatsAppGatewayService;
        private readonly IEasyOrdersApiService easyOrdersApiService;
        private readonly IStoreSettingService storeSettingService;

        public OrderService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IWhatsAppGatewayService whatsAppGatewayService,
            IEasyOrdersApiService easyOrdersApiService,
            IStoreSettingService storeSettingService)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.whatsAppGatewayService = whatsAppGatewayService;
            this.easyOrdersApiService = easyOrdersApiService;
            this.storeSettingService = storeSettingService;
        }

        #region 1. استقبال أوردر جديد من EasyOrders وإرسال رسالة التأكيد

        public async Task<bool> ProcessIncomingWebhookOrderAsync(string rawJson)
        {
            var orderVm = EasyOrderJsonParser.Parse(rawJson);
            if (orderVm is null)
                return true; // طلب اختبار فاضي من EasyOrders عند حفظ اللينك

            await SaveNewOrderAsync(orderVm);

            string brandName = await storeSettingService.GetBrandNameAsync();
            var confirmation = new SendConfirmationViewModel
            {
                Phone = orderVm.Phone,
                Message = ConfirmationMessageBuilder.Build(orderVm, brandName),
                OrderUuid = orderVm.OrderUuid,
                OrderId = orderVm.ShortId,
                StoreId = orderVm.StoreId ?? "",
                CustomerName = orderVm.CustomerName,
                BrandName = brandName
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
            string apiKey = await storeSettingService.GetApiKeyAsync();

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

            bool isUpdated = await easyOrdersApiService.UpdateOrderStatusAsync(reference, EasyOrdersStatuses.Confirmed, apiKey);
            await easyOrdersApiService.AddOrderNoteAsync(reference, OrderNotes.Confirmed, apiKey);
            return isUpdated;
        }

        private async Task<bool> CancelOrderAsync(Order? localOrder, EasyOrderReference reference, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.Cancel());
            return await easyOrdersApiService.UpdateOrderStatusAsync(reference, EasyOrdersStatuses.Canceled, apiKey);
        }

        private async Task<bool> SaveCancelReasonAsync(Order? localOrder, EasyOrderReference reference, string? reason, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.SetCancelReason(reason));
            return await easyOrdersApiService.AddOrderNoteAsync(reference, OrderNotes.CancelReason(reason), apiKey);
        }

        private async Task<bool> RequestSupportAsync(Order? localOrder, EasyOrderReference reference, string apiKey)
        {
            await UpdateLocalOrderAsync(localOrder, order => order.RequestSupport());
            return await easyOrdersApiService.AddOrderNoteAsync(reference, OrderNotes.SupportRequested, apiKey);
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

        public IEnumerable<OrderViewModel> GetAllOrders()
        {
            var orders = unitOfWork.OrderRepository.GetAll();
            return mapper.Map<IEnumerable<OrderViewModel>>(orders);
        }

        public async Task<OrderViewModel?> GetOrderAsync(int id)
        {
            var order = await unitOfWork.OrderRepository.GetByIdAsync(id);
            return order is null ? null : mapper.Map<OrderViewModel>(order);
        }

        #endregion
    }
}
