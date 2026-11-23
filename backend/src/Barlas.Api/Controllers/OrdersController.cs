using Barlas.Application.Orders;
using Barlas.Domain.Identity;
using Barlas.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController(OrderQueries queries) : ControllerBase
{
    /// <summary>سفارش‌های واحد کاربر (مالک و مدیر کارخانه: همه واحدها).</summary>
    [HttpGet]
    public Task<IReadOnlyList<OrderSummary>> List([FromQuery] SalesOrderStatus? status, CancellationToken ct) =>
        queries.ListAsync(status, ct);

    [HttpGet("{id:guid}")]
    public Task<OrderDetail> Get(Guid id, CancellationToken ct) => queries.GetAsync(id, ct);

    /// <summary>ورود فایل اکسل سفارش‌های تهران. تکراری‌ها نادیده گرفته می‌شوند.</summary>
    [Authorize(Policy = nameof(Permission.ImportOrders))]
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IntakeResult> Import(IFormFile file, [FromServices] IOrderSource source,
        [FromServices] OrderIntakeService intake, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var orders = await source.ReadAsync(stream, ct);
        return await intake.IntakeAsync(orders, ct);
    }
}
