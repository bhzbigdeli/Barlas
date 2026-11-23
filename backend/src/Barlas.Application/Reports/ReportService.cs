using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Orders;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Reports;

public sealed record DateRange(DateOnly From, DateOnly To);

// ---------- خلاصه ----------

public sealed record ReportSummary(DateRange Range, int OrdersReceived, int PairsOrdered, int PairsProduced, int PairsShipped,
    int OrdersShipped, int OrdersShippedOnTime, int? OnTimePercent, double? AvgLeadDays, int OpenOrders, int LateOpenOrders,
    int PairsInProgress, decimal LaborCost);

// ---------- دیرکردها ----------

public sealed record LateOrder(Guid Id, string Number, string? CustomerName, string UnitName, SalesOrderStatus Status, int Pairs,
    int ShippedPairs, DateOnly? DueDate, DateTime? ShippedAtUtc, int DelayDays);

public sealed record LatePlan(Guid Id, string Number, string? OrderNumber, PlanStatus Status, int Percent,
    DateOnly? PlannedEndDate, DateTime? CompletedAtUtc, int DelayDays);

public sealed record LateStage(Guid PlanId, string PlanNumber, string StageName, DateOnly PlannedDate, int PairsDone, int PairsTotal, int DelayDays);

public sealed record AtRiskOrder(Guid Id, string Number, string? CustomerName, DateOnly DueDate, int DaysLeft, int Pairs, int ReadyPairs);

public sealed record DelayReport(IReadOnlyList<LateOrder> Orders, IReadOnlyList<AtRiskOrder> AtRisk, IReadOnlyList<LatePlan> Plans,
    IReadOnlyList<LateStage> Stages);

// ---------- زمان‌بندی سفارش‌ها ----------

public sealed record OrderTimeline(Guid Id, string Number, string? CustomerName, string UnitName, SalesOrderStatus Status, int Pairs,
    int ShippedPairs, DateOnly OrderDate, DateTime ReceivedAtUtc, DateTime? PlannedAtUtc, DateTime? InProductionAtUtc,
    DateTime? ReadyAtUtc, DateTime? FirstShipmentAtUtc, DateTime? ShippedAtUtc, DateOnly? DueDate, int? DelayDays, int? LeadDays);

// ---------- تولید روزانه ----------

public sealed record DailyRow(DateOnly Date, IReadOnlyDictionary<string, int> PairsByStage, int Total);

public sealed record DailyProduction(IReadOnlyList<string> Stages, IReadOnlyList<DailyRow> Days, IReadOnlyDictionary<string, int> Totals);

// ---------- کارگران ----------

public sealed record WorkerOutput(Guid WorkerId, string PersonnelCode, string Name, string UnitName, int Scans, int Pairs, int Reworks,
    int ActiveDays, decimal Wage, IReadOnlyDictionary<string, int> PairsByStage);

// ---------- کار در جریان ----------

public sealed record WipStage(string StageName, int Bundles, int Pairs, double AvgWaitDays, double MaxWaitDays);

public sealed record WipBundle(Guid PlanId, string PlanNumber, string BundleNumber, string ModelCode, string ColorName, int Pairs,
    string Waiting, DateTime SinceUtc, double WaitDays);

public sealed record WipReport(IReadOnlyList<WipStage> Stages, IReadOnlyList<WipBundle> Oldest);

/// <summary>
/// گزارش‌های مدیریتی: دیرکردها (پیش‌بینی در برابر واقعی)، زمان‌بندی سفارش‌ها، تولید روزانه،
/// کارکرد و دستمزد کارگران، کار در جریان. فیلتر واحد خودکار از DbContext می‌آید.
/// </summary>
public class ReportService(IAppDbContext db, IClock clock)
{
    private DateOnly Today => LocalCalendar.Date(clock.UtcNow);

    public DateRange Range(DateOnly? from, DateOnly? to)
    {
        var t = to ?? Today;
        var f = from ?? t.AddDays(-29);
        if (f > t) throw new AppException("تاریخ شروع بازه بعد از پایان آن است.");
        if (t.DayNumber - f.DayNumber > 366) throw new AppException("بازه گزارش حداکثر یک سال است.");
        return new DateRange(f, t);
    }

    public async Task<ReportSummary> SummaryAsync(DateRange range, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Utc(range);
        var today = Today;
        var received = await db.SalesOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Sizes)
            .Where(o => o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc < toUtc).ToListAsync(ct);
        var shipped = await db.SalesOrders.AsNoTracking()
            .Where(o => o.ShippedAtUtc >= fromUtc && o.ShippedAtUtc < toUtc).ToListAsync(ct);
        var shippedPairs = await db.Shipments.AsNoTracking().Where(s => s.ShippedAtUtc >= fromUtc && s.ShippedAtUtc < toUtc)
            .SelectMany(s => s.Lines).SumAsync(l => (int?)l.Quantity, ct) ?? 0;
        var produced = await db.Bundles.AsNoTracking().Include(b => b.Sizes)
            .Where(b => b.InWarehouseAtUtc >= fromUtc && b.InWarehouseAtUtc < toUtc).ToListAsync(ct);
        var open = await db.SalesOrders.AsNoTracking()
            .Where(o => o.Status != SalesOrderStatus.Shipped && o.Status != SalesOrderStatus.Cancelled)
            .Select(o => o.DueDate).ToListAsync(ct);
        var inProgress = await db.Bundles.AsNoTracking().Include(b => b.Sizes)
            .Where(b => b.Status != BundleStatus.InWarehouse).ToListAsync(ct);
        var workers = await WorkersAsync(range, ct);

        var onTime = shipped.Count(o => o.DueDate is null || LocalCalendar.Date(o.ShippedAtUtc!.Value) <= o.DueDate);
        return new ReportSummary(range, received.Count, received.Sum(o => o.TotalPairs), produced.Sum(b => b.TotalPairs), shippedPairs,
            shipped.Count, onTime, shipped.Count == 0 ? null : (int)Math.Round(100.0 * onTime / shipped.Count),
            shipped.Count == 0 ? null : Math.Round(shipped.Average(o => (o.ShippedAtUtc!.Value - o.CreatedAtUtc).TotalDays), 1),
            open.Count, open.Count(d => d is { } due && due < today),
            inProgress.Sum(b => b.TotalPairs), workers.Sum(w => w.Wage));
    }

    public async Task<DelayReport> DelaysAsync(CancellationToken ct)
    {
        var today = Today;
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var orders = await db.SalesOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Sizes)
            .Where(o => o.DueDate != null && o.Status != SalesOrderStatus.Cancelled).ToListAsync(ct);

        var lateOrders = orders
            .Select(o => (o, Delay: LocalCalendar.DelayDays(o.DueDate, LocalCalendar.Date(o.ShippedAtUtc), today)))
            .Where(x => x.Delay > 0)
            .Select(x => new LateOrder(x.o.Id, x.o.ExternalNumber, x.o.CustomerName, units[x.o.UnitId], x.o.Status, x.o.TotalPairs,
                x.o.ShippedPairs, x.o.DueDate, x.o.ShippedAtUtc, x.Delay!.Value))
            .OrderByDescending(x => x.ShippedAtUtc is null).ThenByDescending(x => x.DelayDays).ToList();

        var atRisk = orders
            .Where(o => o.ShippedAtUtc is null && o.DueDate >= today && o.DueDate!.Value.DayNumber - today.DayNumber <= 7
                && o.Status != SalesOrderStatus.ReadyToShip)
            .Select(o => new AtRiskOrder(o.Id, o.ExternalNumber, o.CustomerName, o.DueDate!.Value, o.DueDate.Value.DayNumber - today.DayNumber,
                o.TotalPairs, o.FulfilledPairs))
            .OrderBy(o => o.DaysLeft).ToList();

        var plans = await db.Plans.AsNoTracking().Include(p => p.StageTargets)
            .Where(p => p.Status != PlanStatus.Cancelled && p.PlannedEndDate != null).ToListAsync(ct);
        var planIds = plans.Select(p => p.Id).ToList();
        var bundles = await db.Bundles.AsNoTracking().Include(b => b.Sizes).Include(b => b.Scans)
            .Where(b => planIds.Contains(b.ProductionPlanId)).ToListAsync(ct);
        var byPlan = bundles.ToLookup(b => b.ProductionPlanId);
        var orderNumbers = orders.ToDictionary(o => o.Id, o => o.ExternalNumber);
        var stageNames = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var latePlans = new List<LatePlan>();
        var lateStages = new List<LateStage>();
        foreach (var p in plans)
        {
            var pb = byPlan[p.Id].ToList();
            var delay = LocalCalendar.DelayDays(p.PlannedEndDate, LocalCalendar.Date(p.CompletedAtUtc), today);
            if (delay > 0)
                latePlans.Add(new LatePlan(p.Id, p.Number, p.SalesOrderId is { } oid ? orderNumbers.GetValueOrDefault(oid) : null, p.Status,
                    PlanPercent(pb), p.PlannedEndDate, p.CompletedAtUtc, delay.Value));

            if (p.Status == PlanStatus.Completed) continue;
            foreach (var t in p.StageTargets.Where(t => t.PlannedDate < today))
            {
                var withStage = pb.Where(b => b.RouteStageIds.Contains(t.StageId)).ToList();
                var done = withStage.Where(b => StageDone(b, t.StageId)).Sum(b => b.TotalPairs);
                var total = withStage.Sum(b => b.TotalPairs);
                if (total == 0 || done < total)
                    lateStages.Add(new LateStage(p.Id, p.Number, stageNames[t.StageId], t.PlannedDate, done, total,
                        today.DayNumber - t.PlannedDate.DayNumber));
            }
        }

        return new DelayReport(lateOrders, atRisk, latePlans.OrderByDescending(p => p.DelayDays).ToList(),
            lateStages.OrderByDescending(s => s.DelayDays).ToList());
    }

    public async Task<IReadOnlyList<OrderTimeline>> OrdersAsync(DateRange range, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Utc(range);
        var today = Today;
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        // سفارش‌های ثبت‌شده در بازه، به‌اضافه همه سفارش‌های باز.
        var orders = await db.SalesOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Sizes)
            .Where(o => (o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc < toUtc)
                || (o.Status != SalesOrderStatus.Shipped && o.Status != SalesOrderStatus.Cancelled))
            .OrderByDescending(o => o.CreatedAtUtc).Take(500).ToListAsync(ct);
        var ids = orders.Select(o => o.Id).ToList();
        var firstShipments = await db.Shipments.AsNoTracking().Where(s => ids.Contains(s.SalesOrderId))
            .GroupBy(s => s.SalesOrderId).Select(g => new { g.Key, First = g.Min(s => s.ShippedAtUtc) })
            .ToDictionaryAsync(x => x.Key, x => x.First, ct);

        return orders.Select(o => new OrderTimeline(o.Id, o.ExternalNumber, o.CustomerName, units[o.UnitId], o.Status, o.TotalPairs,
            o.ShippedPairs, o.OrderDate, o.CreatedAtUtc, o.PlannedAtUtc, o.InProductionAtUtc, o.ReadyAtUtc,
            firstShipments.TryGetValue(o.Id, out var f) ? f : null, o.ShippedAtUtc, o.DueDate,
            LocalCalendar.DelayDays(o.DueDate, LocalCalendar.Date(o.ShippedAtUtc), today),
            o.ShippedAtUtc is { } s ? (int)Math.Round((s - o.CreatedAtUtc).TotalDays) : null)).ToList();
    }

    public async Task<DailyProduction> DailyAsync(DateRange range, CancellationToken ct)
    {
        var scans = await ScansAsync(range, ct);
        var stageNames = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var completed = scans.Where(s => s.Scan.Kind == ScanKind.Completed).ToList();
        var stages = completed.Select(s => s.Scan.StageId).Distinct().Select(id => stageNames[id]).Order().ToList();
        var days = completed.GroupBy(s => LocalCalendar.Date(s.Scan.ScannedAtUtc)).OrderBy(g => g.Key)
            .Select(g =>
            {
                var byStage = g.GroupBy(s => stageNames[s.Scan.StageId]).ToDictionary(x => x.Key, x => x.Sum(s => s.Scan.Pairs));
                return new DailyRow(g.Key, byStage, byStage.Values.Sum());
            }).ToList();
        var totals = completed.GroupBy(s => stageNames[s.Scan.StageId]).ToDictionary(g => g.Key, g => g.Sum(s => s.Scan.Pairs));
        return new DailyProduction(stages, days, totals);
    }

    public Task<IReadOnlyList<WorkerOutput>> WorkersReportAsync(DateRange range, CancellationToken ct) => WorkersAsync(range, ct);

    public async Task<WipReport> WipAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var bundles = await db.Bundles.AsNoTracking().Include(b => b.Sizes).Include(b => b.Scans)
            .Where(b => b.Status != BundleStatus.InWarehouse).ToListAsync(ct);
        var planIds = bundles.Select(b => b.ProductionPlanId).Distinct().ToList();
        var plans = await db.Plans.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var stageNames = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var models = await db.ShoeModels.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var rows = bundles.Select(b =>
        {
            var waiting = b.Status == BundleStatus.Finished ? "آماده ورود به انبار" : stageNames[b.NextStageId!.Value];
            var since = b.Scans.Count > 0 ? b.Scans.Max(s => s.ScannedAtUtc) : plans[b.ProductionPlanId].ReleasedAtUtc ?? b.CreatedAtUtc;
            var plan = plans[b.ProductionPlanId];
            return new WipBundle(plan.Id, plan.Number, b.Number, models[b.ShoeModelId], colors[b.ColorId], b.TotalPairs, waiting, since,
                Math.Round((now - since).TotalDays, 1));
        }).ToList();

        var stages = rows.GroupBy(r => r.Waiting)
            .Select(g => new WipStage(g.Key, g.Count(), g.Sum(r => r.Pairs), Math.Round(g.Average(r => r.WaitDays), 1), g.Max(r => r.WaitDays)))
            .OrderByDescending(s => s.Pairs).ToList();
        return new WipReport(stages, rows.OrderByDescending(r => r.WaitDays).Take(30).ToList());
    }

    // ---------- کمکی ----------

    private async Task<IReadOnlyList<WorkerOutput>> WorkersAsync(DateRange range, CancellationToken ct)
    {
        var scans = await ScansAsync(range, ct);
        var workerIds = scans.Select(s => s.Scan.WorkerId).Distinct().ToList();
        var workers = await db.Workers.AsNoTracking().Where(w => workerIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var stageNames = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var wages = new WageTable(await db.PieceRates.AsNoTracking().ToListAsync(ct));

        return scans.GroupBy(s => s.Scan.WorkerId).Select(g =>
        {
            var w = workers.GetValueOrDefault(g.Key);
            var done = g.Where(s => s.Scan.Kind == ScanKind.Completed).ToList();
            return new WorkerOutput(g.Key, w?.PersonnelCode ?? "", w?.FullName ?? "?", w is null ? "" : units.GetValueOrDefault(w.UnitId, ""),
                g.Count(), done.Sum(s => s.Scan.Pairs), g.Count(s => s.Scan.Kind == ScanKind.Rework),
                done.Select(s => LocalCalendar.Date(s.Scan.ScannedAtUtc)).Distinct().Count(),
                done.Sum(s => s.Scan.Pairs * wages.Rate(s.ModelId, s.Scan.StageId, LocalCalendar.Date(s.Scan.ScannedAtUtc))),
                done.GroupBy(s => stageNames[s.Scan.StageId]).ToDictionary(x => x.Key, x => x.Sum(s => s.Scan.Pairs)));
        }).OrderByDescending(w => w.Pairs).ToList();
    }

    private sealed record ScanRow(StageScan Scan, Guid ModelId);

    /// <summary>اسکن‌های بازه، فقط از دسته‌هایی که کاربر می‌بیند (فیلتر واحد روی Bundle).</summary>
    private async Task<List<ScanRow>> ScansAsync(DateRange range, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Utc(range);
        return await (from s in db.StageScans.AsNoTracking()
                      join b in db.Bundles on s.BundleId equals b.Id
                      where s.ScannedAtUtc >= fromUtc && s.ScannedAtUtc < toUtc
                      select new ScanRow(s, b.ShoeModelId)).ToListAsync(ct);
    }

    private static (DateTime From, DateTime To) Utc(DateRange r) =>
        (LocalCalendar.StartUtc(r.From), LocalCalendar.StartUtc(r.To.AddDays(1)));

    private static bool StageDone(Bundle b, Guid stageId)
    {
        if (b.Status is BundleStatus.Finished or BundleStatus.InWarehouse) return true;
        if (b.LastCompletedStageId is not { } last) return false;
        var route = b.RouteStageIds.ToList();
        return route.IndexOf(stageId) <= route.IndexOf(last);
    }

    private static int PlanPercent(IReadOnlyList<Bundle> bundles)
    {
        int done = 0, total = 0;
        foreach (var b in bundles)
        {
            var steps = b.RouteStageIds.Count + 1;
            var d = b.Status switch
            {
                BundleStatus.InWarehouse => steps,
                BundleStatus.Finished => steps - 1,
                _ => b.LastCompletedStageId is { } l ? b.RouteStageIds.ToList().IndexOf(l) + 1 : 0,
            };
            done += d * b.TotalPairs;
            total += steps * b.TotalPairs;
        }
        return total == 0 ? 0 : (int)Math.Floor(100.0 * done / total);
    }
}
