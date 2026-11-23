using Barlas.Application.Reports;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barlas.Api.Controllers;

/// <summary>گزارش‌ها. بازه پیش‌فرض ۳۰ روز اخیر (تاریخ‌ها به وقت تهران).</summary>
[ApiController]
[Authorize(Policy = nameof(Permission.ViewReports))]
[Route("api/reports")]
public class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public Task<ReportSummary> Summary(DateOnly? from, DateOnly? to, CancellationToken ct) => reports.SummaryAsync(reports.Range(from, to), ct);

    [HttpGet("delays")]
    public Task<DelayReport> Delays(CancellationToken ct) => reports.DelaysAsync(ct);

    [HttpGet("orders")]
    public Task<IReadOnlyList<OrderTimeline>> Orders(DateOnly? from, DateOnly? to, CancellationToken ct) => reports.OrdersAsync(reports.Range(from, to), ct);

    [HttpGet("daily")]
    public Task<DailyProduction> Daily(DateOnly? from, DateOnly? to, CancellationToken ct) => reports.DailyAsync(reports.Range(from, to), ct);

    [HttpGet("workers")]
    public Task<IReadOnlyList<WorkerOutput>> Workers(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        reports.WorkersReportAsync(reports.Range(from, to), ct);

    [HttpGet("wip")]
    public Task<WipReport> Wip(CancellationToken ct) => reports.WipAsync(ct);
}
