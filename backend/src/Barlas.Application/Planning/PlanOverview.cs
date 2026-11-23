using Barlas.Application.Common;
using Barlas.Domain.Orders;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Planning;

/// <summary>یک قدم در خط زمانی زیربرنامه با تاریخ پیش‌بینی‌شده و واقعی.</summary>
public sealed record Milestone(string Key, string Title, DateOnly? PlannedDate, DateTime? ActualAtUtc, int? DelayDays);

/// <summary>پیشرفت یک مرحله در کل زیربرنامه.</summary>
public sealed record StageProgress(Guid StageId, string Name, DateOnly? PlannedDate, int PairsDone, int PairsTotal, int Percent,
    DateTime? FirstAtUtc, DateTime? FinishedAtUtc, int? DelayDays);

public sealed record BundleCell(Guid StageId, bool InRoute, DateTime? DoneAtUtc, string? WorkerName, int ReworkCount);

public sealed record BundleProgress(Guid Id, string Number, string ModelCode, string ColorName, int Pairs, BundleStatus Status,
    int Percent, string? NextStage, IReadOnlyList<BundleCell> Cells, DateTime? FinishedAtUtc, DateTime? InWarehouseAtUtc);

public sealed record WorkerShare(string Name, string PersonnelCode, int Pairs, decimal Wage);

public sealed record PlanOverview(
    Guid Id, string Number, PlanStatus Status, string UnitName,
    Guid? SalesOrderId, string? OrderNumber, string? CustomerName, DateOnly? OrderDueDate, SalesOrderStatus? OrderStatus,
    int OrderPairs, int SurplusPairs, int TotalPairs, int PairsInWarehouse, int Percent,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, DateTime? ActualStartUtc, DateTime? ActualEndUtc, int? DelayDays,
    decimal LaborCost,
    IReadOnlyList<Milestone> Milestones, IReadOnlyList<StageProgress> Stages, IReadOnlyList<BundleProgress> Bundles,
    IReadOnlyList<WorkerShare> Workers);

public partial class PlanningService
{
    /// <summary>نمای ۰ تا ۱۰۰ زیربرنامه برای صفحه اختصاصی آن.</summary>
    public async Task<PlanOverview> GetOverviewAsync(Guid planId, CancellationToken ct)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Lines).Include(p => p.StageTargets)
            .FirstOrDefaultAsync(p => p.Id == planId, ct) ?? throw AppException.NotFound("زیربرنامه");
        var unit = await db.Units.AsNoTracking().FirstAsync(u => u.Id == plan.UnitId, ct);
        var order = plan.SalesOrderId is { } oid ? await db.SalesOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid, ct) : null;
        var bundles = await db.Bundles.AsNoTracking().Include(b => b.Sizes).Include(b => b.Scans)
            .Where(b => b.ProductionPlanId == planId).OrderBy(b => b.Number).ToListAsync(ct);
        var stageNames = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var models = await db.ShoeModels.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var workerIds = bundles.SelectMany(b => b.Scans).Select(s => s.WorkerId).Distinct().ToList();
        var workers = await db.Workers.AsNoTracking().Where(w => workerIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        var modelIds = plan.Lines.Select(l => l.ShoeModelId).Distinct().ToList();
        var wages = new WageTable(await db.PieceRates.AsNoTracking()
            .Where(r => r.ShoeModelId == null || modelIds.Contains(r.ShoeModelId.Value)).ToListAsync(ct));
        var today = LocalCalendar.Date(clock.UtcNow);

        // ستون‌های ماتریس: مراحل زمان‌بندی، وگرنه اجتماع مسیر دسته‌ها.
        var stageIds = plan.StageTargets.Count > 0
            ? plan.StageTargets.OrderBy(t => t.PlannedDate).Select(t => t.StageId).ToList()
            : bundles.SelectMany(b => b.RouteStageIds).Distinct().ToList();
        foreach (var extra in bundles.SelectMany(b => b.RouteStageIds).Distinct().Except(stageIds).ToList()) stageIds.Add(extra);
        var targets = plan.StageTargets.ToDictionary(t => t.StageId, t => t.PlannedDate);

        // ---------- دسته‌ها ----------
        var bundleRows = new List<BundleProgress>();
        var stageDone = stageIds.ToDictionary(id => id, _ => 0);
        var stageTotal = stageIds.ToDictionary(id => id, _ => 0);
        var stageLast = new Dictionary<Guid, DateTime>();
        var stageFirst = new Dictionary<Guid, DateTime>();
        int unitsDone = 0, unitsTotal = 0;

        foreach (var b in bundles)
        {
            var route = b.RouteStageIds;
            var doneCount = b.Status is BundleStatus.Finished or BundleStatus.InWarehouse ? route.Count
                : b.LastCompletedStageId is { } last ? route.ToList().IndexOf(last) + 1 : 0;
            var cells = new List<BundleCell>();
            foreach (var sid in stageIds)
            {
                var idx = route.ToList().IndexOf(sid);
                if (idx < 0) { cells.Add(new BundleCell(sid, false, null, null, 0)); continue; }
                var scans = b.Scans.Where(s => s.StageId == sid).OrderBy(s => s.ScannedAtUtc).ToList();
                var completed = scans.LastOrDefault(s => s.Kind == ScanKind.Completed);
                var done = idx < doneCount;
                stageTotal[sid] += b.TotalPairs;
                if (done)
                {
                    stageDone[sid] += b.TotalPairs;
                    if (completed is not null && (!stageLast.TryGetValue(sid, out var l) || completed.ScannedAtUtc > l))
                        stageLast[sid] = completed.ScannedAtUtc;
                }
                var first = scans.FirstOrDefault(s => s.Kind == ScanKind.Completed);
                if (first is not null && (!stageFirst.TryGetValue(sid, out var f) || first.ScannedAtUtc < f))
                    stageFirst[sid] = first.ScannedAtUtc;
                cells.Add(new BundleCell(sid, true, done ? completed?.ScannedAtUtc : null,
                    done && completed is not null && workers.TryGetValue(completed.WorkerId, out var w) ? w.FullName : null,
                    scans.Count(s => s.Kind == ScanKind.Rework)));
            }

            // هر مرحله یک واحد کار؛ ورود به انبار هم یک واحد آخر.
            var steps = route.Count + 1;
            var stepsDone = doneCount + (b.Status == BundleStatus.InWarehouse ? 1 : 0);
            unitsDone += stepsDone * b.TotalPairs;
            unitsTotal += steps * b.TotalPairs;
            bundleRows.Add(new BundleProgress(b.Id, b.Number, models[b.ShoeModelId], colors[b.ColorId], b.TotalPairs, b.Status,
                Percent(stepsDone, steps), b.NextStageId is { } next ? stageNames[next] : null, cells, b.FinishedAtUtc, b.InWarehouseAtUtc));
        }

        var stages = stageIds.Select(sid =>
        {
            DateTime? finished = stageTotal[sid] > 0 && stageDone[sid] == stageTotal[sid] && stageLast.TryGetValue(sid, out var l) ? l : null;
            var planned = targets.TryGetValue(sid, out var d) ? d : (DateOnly?)null;
            return new StageProgress(sid, stageNames[sid], planned, stageDone[sid], stageTotal[sid], Percent(stageDone[sid], stageTotal[sid]),
                stageFirst.TryGetValue(sid, out var f) ? f : null, finished,
                LocalCalendar.DelayDays(planned, LocalCalendar.Date(finished), today));
        }).ToList();

        // ---------- خط زمانی ----------
        var allScans = bundles.SelectMany(b => b.Scans).ToList();
        DateTime? actualStart = allScans.Count > 0 ? allScans.Min(s => s.ScannedAtUtc) : null;
        var milestones = new List<Milestone>
        {
            new("created", "ثبت زیربرنامه", null, plan.SubmittedAtUtc ?? plan.CreatedAtUtc, null),
        };
        if (plan.ApprovedAtUtc is not null || plan.Status == PlanStatus.AwaitingApproval)
            milestones.Add(new("approved", "تأیید مازاد", null, plan.ApprovedAtUtc, null));
        milestones.Add(new("released", "صدور دسته و کاردکس", plan.PlannedStartDate, plan.ReleasedAtUtc,
            LocalCalendar.DelayDays(plan.PlannedStartDate, LocalCalendar.Date(plan.ReleasedAtUtc), today)));
        milestones.Add(new("started", "شروع تولید (اولین اسکن)", plan.PlannedStartDate, actualStart,
            LocalCalendar.DelayDays(plan.PlannedStartDate, LocalCalendar.Date(actualStart), today)));
        milestones.AddRange(stages.Select(s => new Milestone("stage:" + s.StageId, "پایان " + s.Name, s.PlannedDate, s.FinishedAtUtc, s.DelayDays)));
        milestones.Add(new("completed", "ورود کامل به انبار", plan.PlannedEndDate, plan.CompletedAtUtc,
            LocalCalendar.DelayDays(plan.PlannedEndDate, LocalCalendar.Date(plan.CompletedAtUtc), today)));
        if (order is not null)
            milestones.Add(new("delivered", "ارسال کامل سفارش", order.DueDate, order.ShippedAtUtc,
                LocalCalendar.DelayDays(order.DueDate, LocalCalendar.Date(order.ShippedAtUtc), today)));

        // ---------- کارگران ----------
        var bundleModel = bundles.ToDictionary(b => b.Id, b => b.ShoeModelId);
        var workerRows = allScans.Where(s => s.Kind == ScanKind.Completed)
            .GroupBy(s => s.WorkerId)
            .Select(g => new WorkerShare(
                workers.TryGetValue(g.Key, out var w) ? w.FullName : "?", w?.PersonnelCode ?? "",
                g.Sum(s => s.Pairs),
                g.Sum(s => s.Pairs * wages.Rate(bundleModel[s.BundleId], s.StageId, LocalCalendar.Date(s.ScannedAtUtc)))))
            .OrderByDescending(x => x.Pairs).ToList();

        return new PlanOverview(plan.Id, plan.Number, plan.Status, unit.Name,
            order?.Id, order?.ExternalNumber, order?.CustomerName, order?.DueDate, order?.Status,
            plan.OrderPairs, plan.SurplusPairs, plan.TotalPairs,
            bundles.Where(b => b.Status == BundleStatus.InWarehouse).Sum(b => b.TotalPairs),
            bundles.Count == 0 ? 0 : Percent(unitsDone, unitsTotal),
            plan.PlannedStartDate, plan.PlannedEndDate, actualStart, plan.CompletedAtUtc,
            LocalCalendar.DelayDays(plan.PlannedEndDate, LocalCalendar.Date(plan.CompletedAtUtc), today),
            workerRows.Sum(w => w.Wage),
            milestones, stages, bundleRows, workerRows);
    }

    private static int Percent(int done, int total) => total == 0 ? 0 : (int)Math.Floor(100.0 * done / total);
}
