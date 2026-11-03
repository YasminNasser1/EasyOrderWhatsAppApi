using AutoMapper;
using EasyOrderWhatsAppBLL.Helpers;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.OrderViewModels;
using EasyOrderWhatsAppBLL.ViewModels.SettingViewModels;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Entities.Enums;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly IWhatsAppService whatsAppService;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper, IWhatsAppService whatsAppService)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.whatsAppService = whatsAppService;
        }

        // 1. استقبال الأوردر الجديد من EasyOrders وحفظه وإرسال رسالة الواتساب للعميل
        public async Task<bool> ProcessIncomingWebhookOrder(string rawJson)
        {
            try
            {
                var orderVm = OrderHelperMethod.ParseEasyOrderJson(rawJson);
                if (orderVm is null)
                    return true; // في حالة طلب الاختبار الفارغ من EasyOrders عند حفظ اللينك

                var setting = await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync();
                string brandName = setting?.BrandName ?? "Outlix";

                // تحويل الـ ViewModel إلى Entity وحفظه عن طريق الـ UnitOfWork (بنفس طريقة MetalCompany)
                var mappedOrder = mapper.Map<Order>(orderVm);
                mappedOrder.Status = Status.انتظار;
                mappedOrder.CreatedAt = DateTime.Now;

                await unitOfWork.OrderRepository.AddAsync(mappedOrder);
                await unitOfWork.SaveChangesAsync();

                // تجهيز نص الرسالة وإرسالها للعميل على الواتساب
                string message = OrderHelperMethod.BuildConfirmationMessage(orderVm, brandName);

                await whatsAppService.SendOrderConfirmationAsync(
                    orderVm.Phone,
                    message,
                    orderVm.OrderUuid,
                    orderVm.ShortId,
                    orderVm.StoreId,
                    orderVm.CustomerName,
                    brandName);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // 2. التعامل مع ضغطة الزرار من العميل (تأكيد / إلغاء / سبب الإلغاء / دعم) وتحديث موقع EasyOrders
        public async Task<bool> HandleCustomerOrderAction(OrderActionViewModel actionModel)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(actionModel.Action))
                    return false;

                var setting = await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync();
                string apiKey = setting?.ApiKey ?? "";

                var order = await unitOfWork.OrderRepository.GetByShortIdOrUuidAsync(actionModel.OrderId, actionModel.OrderUuid);
                string action = actionModel.Action.ToLower();

                if (action == "confirm")
                {
                    if (order is not null)
                    {
                        order.Status = Status.تأكيد;
                        order.UpdatedAt = DateTime.Now;
                        unitOfWork.OrderRepository.Update(order);
                        await unitOfWork.SaveChangesAsync();
                    }

                    bool updated = await whatsAppService.UpdateOrderStatusOnEasyOrdersAsync(actionModel.OrderUuid, actionModel.OrderId, "confirmed", apiKey);
                    await whatsAppService.AddOrderNoteOnEasyOrdersAsync(actionModel.OrderUuid, actionModel.OrderId, actionModel.StoreId, "✅ تم تأكيد الطلب من العميل عبر الواتساب", apiKey);
                    return updated;
                }
                else if (action == "cancel")
                {
                    if (order is not null)
                    {
                        order.Status = Status.لاغى;
                        order.UpdatedAt = DateTime.Now;
                        unitOfWork.OrderRepository.Update(order);
                        await unitOfWork.SaveChangesAsync();
                    }

                    return await whatsAppService.UpdateOrderStatusOnEasyOrdersAsync(actionModel.OrderUuid, actionModel.OrderId, "canceled", apiKey);
                }
                else if (action == "cancel_reason")
                {
                    if (order is not null)
                    {
                        order.CancelReason = actionModel.Reason;
                        order.UpdatedAt = DateTime.Now;
                        unitOfWork.OrderRepository.Update(order);
                        await unitOfWork.SaveChangesAsync();
                    }

                    string note = $"❌ سبب إلغاء الطلب (من العميل عبر واتساب): {actionModel.Reason}";
                    return await whatsAppService.AddOrderNoteOnEasyOrdersAsync(actionModel.OrderUuid, actionModel.OrderId, actionModel.StoreId, note, apiKey);
                }
                else if (action == "support")
                {
                    if (order is not null)
                    {
                        order.Status = Status.دعم;
                        order.UpdatedAt = DateTime.Now;
                        unitOfWork.OrderRepository.Update(order);
                        await unitOfWork.SaveChangesAsync();
                    }

                    return await whatsAppService.AddOrderNoteOnEasyOrdersAsync(actionModel.OrderUuid, actionModel.OrderId, actionModel.StoreId, "📞 طلب العميل التواصل مع خدمة العملاء عبر الواتساب", apiKey);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<OrderViewModel> GetAllOrders()
        {
            var orders = unitOfWork.OrderRepository.GetAll();
            return mapper.Map<IEnumerable<OrderViewModel>>(orders);
        }

        public async Task<OrderViewModel?> GetOrder(int id)
        {
            var order = await unitOfWork.OrderRepository.GetByIdAsync(id);
            if (order == null) return null;
            return mapper.Map<OrderViewModel>(order);
        }

        public async Task<(bool hasKey, string maskedKey, string brandName)> GetStoreSettingStatus()
        {
            var setting = await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync();
            string key = setting?.ApiKey ?? "";
            string brand = setting?.BrandName ?? "Outlix";
            string masked = string.IsNullOrWhiteSpace(key)
                ? ""
                : (key.Length > 8 ? key.Substring(0, 4) + "****" + key.Substring(key.Length - 4) : "****");

            return (!string.IsNullOrWhiteSpace(key), masked, brand);
        }

        public async Task<bool> SaveStoreSetting(SaveSettingViewModel model)
        {
            try
            {
                var setting = await unitOfWork.StoreSettingRepository.GetCurrentSettingAsync();
                if (setting is null)
                {
                    setting = new StoreSetting
                    {
                        ApiKey = model.ApiKey?.Trim() ?? "",
                        BrandName = string.IsNullOrWhiteSpace(model.BrandName) ? "Outlix" : model.BrandName.Trim(),
                        CreatedAt = DateTime.Now
                    };
                    await unitOfWork.StoreSettingRepository.AddAsync(setting);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(model.ApiKey))
                        setting.ApiKey = model.ApiKey.Trim();

                    if (!string.IsNullOrWhiteSpace(model.BrandName))
                        setting.BrandName = model.BrandName.Trim();

                    setting.UpdatedAt = DateTime.Now;
                    unitOfWork.StoreSettingRepository.Update(setting);
                }

                return await unitOfWork.SaveChangesAsync() > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
