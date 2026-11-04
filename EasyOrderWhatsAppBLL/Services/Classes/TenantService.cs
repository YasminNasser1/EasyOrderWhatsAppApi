using AutoMapper;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.TenantViewModels;
using EasyOrderWhatsAppDAL.Entities;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyOrderWhatsAppBLL.Services.Classes
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly ILogger<TenantService> logger;

        public TenantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<TenantService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.logger = logger;
        }

        public async Task<TenantViewModel?> GetTenantByKeyAsync(string storeKey)
        {
            var tenant = await unitOfWork.TenantRepository.GetByKeyAsync(storeKey);
            return tenant == null ? null : mapper.Map<TenantViewModel>(tenant);
        }

        public async Task<IEnumerable<TenantViewModel>> GetAllTenantsAsync()
        {
            var tenants = await unitOfWork.TenantRepository.GetAll().ToListAsync();
            return mapper.Map<IEnumerable<TenantViewModel>>(tenants);
        }

        public async Task<TenantViewModel> RegisterTenantAsync(RegisterTenantViewModel model)
        {
            var normalizedKey = model.StoreKey.Trim().ToLower();
            var existing = await unitOfWork.TenantRepository.GetByKeyAsync(normalizedKey);
            if (existing != null)
            {
                throw new InvalidOperationException($"المتجر بالمعرف '{normalizedKey}' مسجل بالفعل مسبقاً.");
            }

            var tenant = mapper.Map<Tenant>(model);
            tenant.StoreKey = normalizedKey;
            tenant.IsActive = true;

            await unitOfWork.TenantRepository.AddAsync(tenant);
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("✅ تم تسجيل تاجر جديد بنجاح: {StoreKey} ({BrandName})", tenant.StoreKey, tenant.BrandName);
            return mapper.Map<TenantViewModel>(tenant);
        }

        public async Task<TenantViewModel?> UpdateTenantAsync(string storeKey, UpdateTenantViewModel model)
        {
            var tenant = await unitOfWork.TenantRepository.GetByKeyAsync(storeKey);
            if (tenant == null) return null;

            tenant.Update(model.BrandName, model.ApiKey, model.CallbackUrl, model.IsActive);
            unitOfWork.TenantRepository.Update(tenant);
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("🔄 تم تحديث بيانات التاجر: {StoreKey}", storeKey);
            return mapper.Map<TenantViewModel>(tenant);
        }

        public async Task<TenantViewModel> EnsureTenantExistsAsync(string storeKey, string? brandName = null)
        {
            var normalizedKey = string.IsNullOrWhiteSpace(storeKey) ? "default" : storeKey.Trim().ToLower();
            var existing = await unitOfWork.TenantRepository.GetByKeyAsync(normalizedKey);
            if (existing != null)
            {
                return mapper.Map<TenantViewModel>(existing);
            }

            var newTenant = new Tenant
            {
                StoreKey = normalizedKey,
                BrandName = string.IsNullOrWhiteSpace(brandName) ? normalizedKey : brandName.Trim(),
                IsActive = true
            };

            await unitOfWork.TenantRepository.AddAsync(newTenant);
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("🆕 تم إنشاء تاجر جديد تلقائياً عند أول طلب: {StoreKey}", normalizedKey);
            return mapper.Map<TenantViewModel>(newTenant);
        }
    }
}
