using EasyOrderWhatsAppBLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EasyOrderWhatsAppPL.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService orderService;

        public OrdersController(IOrderService orderService)
        {
            this.orderService = orderService;
        }

        [HttpGet]
        public IActionResult Index([FromQuery] string? tenantId = null)
        {
            return Ok(orderService.GetAllOrders(tenantId));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var order = await orderService.GetOrderAsync(id);
            return order is null ? NotFound() : Ok(order);
        }
    }
}
