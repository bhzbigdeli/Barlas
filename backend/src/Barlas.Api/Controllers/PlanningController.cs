using Barlas.Application.Planning;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class PlanningController(PlanningService planning) : ControllerBase
{
    /// <summary>باقیمانده برنامه‌ریزی‌نشده سفارش و زیربرنامه‌هایش.</summary>
    [HttpGet("orders/{id:guid}/planning")]
    public Task<OrderPlanning> OrderPlanning(Guid id, CancellationToken ct) => planning.GetOrderPlanningAsync(id, ct);

    [Authorize(Policy = nameof(Permission.ManagePlans))]
    [HttpPost("plans")]
    public Task<PlanDetail> Create(CreatePlanRequest request, CancellationToken ct) => planning.CreatePlanAsync(request, ct);

    [HttpGet("plans")]
    public Task<IReadOnlyList<PlanSummary>> List(CancellationToken ct) => planning.ListPlansAsync(ct);

    [HttpGet("plans/{id:guid}")]
    public Task<PlanDetail> Get(Guid id, CancellationToken ct) => planning.GetPlanAsync(id, ct);

    [Authorize(Policy = nameof(Permission.ApproveSurplus))]
    [HttpPost("plans/{id:guid}/approve")]
    public Task<PlanDetail> Approve(Guid id, CancellationToken ct) => planning.ApproveAsync(id, ct);

    /// <summary>صدور دسته‌ها و کاردکس‌ها.</summary>
    [Authorize(Policy = nameof(Permission.ManagePlans))]
    [HttpPost("plans/{id:guid}/release")]
    public Task<PlanDetail> Release(Guid id, ReleaseRequest request, CancellationToken ct) => planning.ReleaseAsync(id, request, ct);

    /// <summary>ورود دسته تمام‌شده به انبار با اسکن QR کاردکس.</summary>
    [Authorize(Policy = nameof(Permission.ManageWarehouse))]
    [HttpPost("warehouse/receive")]
    public Task<ReceiveResult> Receive(ReceiveRequest request, CancellationToken ct) => planning.ReceiveAsync(request, ct);

    [Authorize(Policy = nameof(Permission.ManageWarehouse))]
    [HttpGet("warehouse/free-stock")]
    public Task<IReadOnlyList<FreeStockRow>> FreeStock(CancellationToken ct) => planning.ListFreeStockAsync(ct);

    /// <summary>ارسال سفارش کامل به تهران.</summary>
    [Authorize(Policy = nameof(Permission.ManageWarehouse))]
    [HttpPost("orders/{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, CancellationToken ct)
    {
        await planning.ShipAsync(id, ct);
        return NoContent();
    }
}
