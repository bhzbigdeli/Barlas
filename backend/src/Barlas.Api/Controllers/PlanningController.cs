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

    [Authorize(Policy = nameof(Permission.ManagePlans))]
    [HttpPut("plans/{id:guid}/schedule")]
    public async Task<PlanOverview> Schedule(Guid id, ScheduleRequest request, CancellationToken ct)
    {
        await planning.ScheduleAsync(id, request, ct);
        return await planning.GetOverviewAsync(id, ct);
    }

    /// <summary>نمای کامل زیربرنامه: پیشرفت، تاریخ‌های پیش‌بینی‌شده و واقعی هر قدم، ماتریس دسته × مرحله.</summary>
    [HttpGet("plans/{id:guid}/overview")]
    public Task<PlanOverview> Overview(Guid id, CancellationToken ct) => planning.GetOverviewAsync(id, ct);

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

    /// <summary>ارسال (کامل یا جزئی) سفارش به تهران.</summary>
    [Authorize(Policy = nameof(Permission.ManageWarehouse))]
    [HttpPost("orders/{id:guid}/shipments")]
    public Task<ShipmentDto> Ship(Guid id, ShipRequest request, CancellationToken ct) => planning.ShipAsync(id, request, ct);
}
