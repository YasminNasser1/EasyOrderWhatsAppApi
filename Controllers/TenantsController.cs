using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppBLL.ViewModels.TenantViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    // إدارة التجار والمتاجر المشتركة في منصة SaaS
    [ApiController]
    [Route("api/tenants")]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService tenantService;

        public TenantsController(ITenantService tenantService)
        {
            this.tenantService = tenantService;
        }

        // عرض كل التجار المسجلين في المنصة
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenants = await tenantService.GetAllTenantsAsync();
            return Ok(tenants);
        }

        // عرض بيانات تاجر معين بالـ StoreKey بتاعه
        [HttpGet("{storeKey}")]
        public async Task<IActionResult> GetByKey(string storeKey)
        {
            var tenant = await tenantService.GetTenantByKeyAsync(storeKey);
            return tenant == null ? NotFound(new { message = $"المتجر '{storeKey}' غير موجود" }) : Ok(tenant);
        }

        // تسجيل تاجر / متجر جديد في المنصة
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterTenantViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var created = await tenantService.RegisterTenantAsync(model);
                return CreatedAtAction(nameof(GetByKey), new { storeKey = created.StoreKey }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        // تعديل بيانات تاجر
        [HttpPut("{storeKey}")]
        public async Task<IActionResult> Update(string storeKey, [FromBody] UpdateTenantViewModel model)
        {
            var updated = await tenantService.UpdateTenantAsync(storeKey, model);
            return updated == null ? NotFound(new { message = $"المتجر '{storeKey}' غير موجود" }) : Ok(updated);
        }
    }
}
