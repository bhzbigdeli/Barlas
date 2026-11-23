using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Common;
using Barlas.Domain.Identity;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Planning;

public sealed record PlanningRow(Guid SalesOrderLineId, Guid ShoeModelId, string ModelCode, string ModelName,
    Guid ColorId, string ColorName, int Size, int Ordered, int FromStock, int AlreadyPlanned, int Unplanned);

public sealed record PlanSummary(Guid Id, string Number, PlanStatus Status, int OrderPairs, int SurplusPairs, int BundleCount);

public sealed record OrderPlanning(Guid OrderId, string ExternalNumber, SalesOrderStatus Status, decimal SurplusThreshold,
    IReadOnlyList<PlanningRow> Rows, IReadOnlyList<PlanSummary> Plans);

public sealed record NewPlanLine(Guid ShoeModelId, Guid ColorId, int Size, int OrderQuantity, int SurplusQuantity);

public sealed record CreatePlanRequest(Guid SalesOrderId, IReadOnlyList<NewPlanLine> Lines);

public sealed record ReleaseRequest(int MaxPairsPerBundle);

public sealed record PlanLineDto(string ModelCode, string ModelName, string ColorName, int Size, int OrderQuantity, int SurplusQuantity);

public sealed record BundleDto(Guid Id, string Number, string QrToken, string ModelCode, string ModelName, string ColorName,
    IReadOnlyList<BundleSizeDto> Sizes, int Pairs, BundleStatus Status, string? LastStage, string? NextStage, IReadOnlyList<string> Route);

public sealed record BundleSizeDto(int Size, int Quantity);

public sealed record PlanDetail(Guid Id, string Number, Guid UnitId, string UnitName, Guid? SalesOrderId, string? OrderNumber,
    PlanStatus Status, decimal SurplusRatio, decimal SurplusThreshold, IReadOnlyList<PlanLineDto> Lines, IReadOnlyList<BundleDto> Bundles);

public sealed record ReceiveRequest(string QrToken);

public sealed record ReceiveResult(string BundleNumber, int ToOrder, int ToFreeStock, string? OrderNumber, SalesOrderStatus? OrderStatus);

public sealed record FreeStockRow(string UnitName, string ModelCode, string ModelName, string ColorName, int Size, int Quantity);

/// <summary>
/// مسیر سفارش در کارخانه: زیربرنامه (مدیر فنی) ← تأیید مازاد ← صدور دسته‌ها ← … ← ورود به انبار ← ارسال.
/// </summary>
public class PlanningService(IAppDbContext db, ICurrentUser user, IClock clock)
{
    // ---------- برنامه‌ریزی سفارش ----------

    public async Task<OrderPlanning> GetOrderPlanningAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        var unit = await db.Units.FirstAsync(u => u.Id == order.UnitId, ct);
        var planned = await PlannedByLineSizeAsync(order.Id, ct);
        var models = await db.ShoeModels.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var rows = order.Lines.OrderBy(l => l.LineNumber)
            .SelectMany(l => l.Sizes.OrderBy(s => s.Size).Select(s =>
            {
                var already = planned.GetValueOrDefault((l.Id, s.Size));
                var unplanned = Math.Max(0, s.Quantity - s.FromStock - s.FromProduction - already);
                return new PlanningRow(l.Id, l.ShoeModelId, models[l.ShoeModelId].Code, models[l.ShoeModelId].Name,
                    l.ColorId, colors[l.ColorId], s.Size, s.Quantity, s.FromStock, already, unplanned);
            }))
            .ToList();

        var plans = await db.Plans.AsNoTracking().Include(p => p.Lines).Where(p => p.SalesOrderId == order.Id)
            .OrderBy(p => p.Number).ToListAsync(ct);
        var bundleCounts = await db.Bundles.Where(b => plans.Select(p => p.Id).Contains(b.ProductionPlanId))
            .GroupBy(b => b.ProductionPlanId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return new OrderPlanning(order.Id, order.ExternalNumber, order.Status, unit.SurplusApprovalThreshold, rows,
            plans.Select(p => new PlanSummary(p.Id, p.Number, p.Status, p.OrderPairs, p.SurplusPairs, bundleCounts.GetValueOrDefault(p.Id))).ToList());
    }

    /// <summary>مدیر فنی باقیمانده سفارش (و مازاد مجاز) را به یک زیربرنامه می‌برد و ثبت می‌کند.</summary>
    public async Task<PlanDetail> CreatePlanAsync(CreatePlanRequest request, CancellationToken ct)
    {
        var order = await LoadOrderAsync(request.SalesOrderId, ct, track: true);
        if (order.Status is SalesOrderStatus.Shipped or SalesOrderStatus.Cancelled)
            throw new AppException("این سفارش بسته شده است.");

        var unit = await db.Units.FirstAsync(u => u.Id == order.UnitId, ct);
        var planned = await PlannedByLineSizeAsync(order.Id, ct);
        var number = await NextPlanNumberAsync(unit.Id, unit.Code, ct);
        var plan = new ProductionPlan(unit.Id, number, order.Id);

        foreach (var l in request.Lines.Where(l => l.OrderQuantity + l.SurplusQuantity > 0))
        {
            var line = order.Lines.FirstOrDefault(x => x.ShoeModelId == l.ShoeModelId && x.ColorId == l.ColorId)
                ?? throw new AppException("این مدل و رنگ در سفارش نیست.");
            var size = line.Sizes.FirstOrDefault(s => s.Size == l.Size)
                ?? throw new AppException($"سایز {l.Size} در سفارش نیست.");
            var unplanned = size.Outstanding - planned.GetValueOrDefault((line.Id, l.Size));
            if (l.OrderQuantity > unplanned)
                throw new AppException($"سایز {l.Size}: سهم سفارش ({l.OrderQuantity}) از باقیمانده برنامه‌ریزی‌نشده ({unplanned}) بیشتر است.");

            plan.SetLine(new SkuKey(l.ShoeModelId, l.ColorId, l.Size), line.Id, l.OrderQuantity, l.SurplusQuantity);
        }

        if (plan.Lines.Count == 0) throw new AppException("زیربرنامه خالی است.");
        plan.Submit(unit.SurplusApprovalThreshold);
        if (order.Status == SalesOrderStatus.Received) order.MarkPlanned();

        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(plan.Id, ct);
    }

    public async Task<PlanDetail> ApproveAsync(Guid planId, CancellationToken ct)
    {
        if (!user.Has(Permission.ApproveSurplus)) throw AppException.Forbidden();
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct) ?? throw AppException.NotFound("زیربرنامه");
        plan.Approve(user.UserId!.Value, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(planId, ct);
    }

    /// <summary>صدور دسته‌ها و آماده‌شدن کاردکس‌ها برای چاپ.</summary>
    public async Task<PlanDetail> ReleaseAsync(Guid planId, ReleaseRequest request, CancellationToken ct)
    {
        var plan = await db.Plans.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw AppException.NotFound("زیربرنامه");

        var modelIds = plan.Lines.Select(l => l.ShoeModelId).Distinct().ToList();
        var routeByModel = await db.ShoeModels.Where(m => modelIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.RouteId, ct);
        var routes = await db.Routes.Include(r => r.Steps).Where(r => routeByModel.Values.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        var bundles = BundleSplitter.Split(plan, request.MaxPairsPerBundle,
            modelId => routes[routeByModel[modelId]].Steps.Select(s => s.StageId).ToList());
        db.Bundles.AddRange(bundles);

        if (plan.SalesOrderId is { } orderId)
        {
            var order = await db.SalesOrders.FirstAsync(o => o.Id == orderId, ct);
            if (order.Status == SalesOrderStatus.Planned) order.MarkInProduction();
        }

        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(planId, ct);
    }

    public async Task<PlanDetail> GetPlanAsync(Guid planId, CancellationToken ct)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw AppException.NotFound("زیربرنامه");
        var unit = await db.Units.AsNoTracking().FirstAsync(u => u.Id == plan.UnitId, ct);
        var orderNumber = plan.SalesOrderId is { } oid
            ? await db.SalesOrders.Where(o => o.Id == oid).Select(o => o.ExternalNumber).FirstOrDefaultAsync(ct) : null;
        var models = await db.ShoeModels.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var stages = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var bundles = await db.Bundles.AsNoTracking().Include(b => b.Sizes).Where(b => b.ProductionPlanId == planId)
            .OrderBy(b => b.Number).ToListAsync(ct);

        return new PlanDetail(plan.Id, plan.Number, unit.Id, unit.Name, plan.SalesOrderId, orderNumber, plan.Status,
            plan.SurplusRatio == decimal.MaxValue ? 1 : plan.SurplusRatio, unit.SurplusApprovalThreshold,
            plan.Lines.OrderBy(l => models[l.ShoeModelId].Code).ThenBy(l => l.Size)
                .Select(l => new PlanLineDto(models[l.ShoeModelId].Code, models[l.ShoeModelId].Name, colors[l.ColorId], l.Size, l.OrderQuantity, l.SurplusQuantity))
                .ToList(),
            bundles.Select(b => new BundleDto(b.Id, b.Number, b.QrToken, models[b.ShoeModelId].Code, models[b.ShoeModelId].Name, colors[b.ColorId],
                b.Sizes.OrderBy(s => s.Size).Select(s => new BundleSizeDto(s.Size, s.Quantity)).ToList(), b.TotalPairs, b.Status,
                b.LastCompletedStageId is { } last ? stages[last] : null,
                b.NextStageId is { } next ? stages[next] : null,
                b.RouteStageIds.Select(id => stages[id]).ToList())).ToList());
    }

    public async Task<IReadOnlyList<PlanSummary>> ListPlansAsync(CancellationToken ct)
    {
        var plans = await db.Plans.AsNoTracking().Include(p => p.Lines).OrderByDescending(p => p.CreatedAtUtc).Take(200).ToListAsync(ct);
        var ids = plans.Select(p => p.Id).ToList();
        var counts = await db.Bundles.Where(b => ids.Contains(b.ProductionPlanId)).GroupBy(b => b.ProductionPlanId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return plans.Select(p => new PlanSummary(p.Id, p.Number, p.Status, p.OrderPairs, p.SurplusPairs, counts.GetValueOrDefault(p.Id))).ToList();
    }

    // ---------- انبار و ارسال ----------

    /// <summary>ورود دسته تمام‌شده به انبار: اول سهم سفارش، بقیه به موجودی آزاد.</summary>
    public async Task<ReceiveResult> ReceiveAsync(ReceiveRequest request, CancellationToken ct)
    {
        var bundle = await db.Bundles.Include(b => b.Sizes).FirstOrDefaultAsync(b => b.QrToken == request.QrToken.Trim(), ct)
            ?? throw AppException.NotFound("دسته");
        var plan = await db.Plans.FirstAsync(p => p.Id == bundle.ProductionPlanId, ct);
        var order = plan.SalesOrderId is { } oid
            ? await db.SalesOrders.Include(o => o.Lines).ThenInclude(l => l.Sizes).FirstAsync(o => o.Id == oid, ct)
            : null;

        var stock = await db.FreeStock.Where(s => s.UnitId == bundle.UnitId && s.ShoeModelId == bundle.ShoeModelId && s.ColorId == bundle.ColorId)
            .ToDictionaryAsync(s => s.Sku, ct);

        var result = WarehouseReceipt.Receive(bundle, order, sku =>
        {
            if (stock.TryGetValue(sku, out var item)) return item;
            item = new FreeStockItem(bundle.UnitId, sku);
            db.FreeStock.Add(item);
            return stock[sku] = item;
        });
        db.StockMovements.AddRange(result.Movements);

        var others = await db.Bundles.Where(b => b.ProductionPlanId == plan.Id && b.Id != bundle.Id).Select(b => b.Status).ToListAsync(ct);
        if (others.All(s => s == BundleStatus.InWarehouse)) plan.MarkCompleted();

        await db.SaveChangesAsync(ct);
        return new ReceiveResult(bundle.Number, result.ToOrder, result.ToFreeStock, order?.ExternalNumber, order?.Status);
    }

    public async Task ShipAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw AppException.NotFound("سفارش");
        if (order.Status != SalesOrderStatus.ReadyToShip) throw new AppException("سفارش هنوز آماده ارسال نیست.");
        order.MarkShipped();
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FreeStockRow>> ListFreeStockAsync(CancellationToken ct)
    {
        var items = await db.FreeStock.AsNoTracking().Where(s => s.Quantity > 0).ToListAsync(ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var models = await db.ShoeModels.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return items.Select(s => new FreeStockRow(units[s.UnitId], models[s.ShoeModelId].Code, models[s.ShoeModelId].Name, colors[s.ColorId], s.Size, s.Quantity))
            .OrderBy(r => r.UnitName).ThenBy(r => r.ModelCode).ThenBy(r => r.ColorName).ThenBy(r => r.Size).ToList();
    }

    // ---------- کمکی ----------

    private async Task<SalesOrder> LoadOrderAsync(Guid id, CancellationToken ct, bool track = false)
    {
        var q = db.SalesOrders.Include(o => o.Lines).ThenInclude(l => l.Sizes).AsQueryable();
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw AppException.NotFound("سفارش");
    }

    /// <summary>سهم سفارشی که در زیربرنامه‌های فعال آمده ولی هنوز به انبار نرسیده.</summary>
    private async Task<Dictionary<(Guid LineId, int Size), int>> PlannedByLineSizeAsync(Guid orderId, CancellationToken ct)
    {
        var lines = await db.Plans.AsNoTracking()
            .Where(p => p.SalesOrderId == orderId && p.Status != PlanStatus.Cancelled && p.Status != PlanStatus.Completed)
            .SelectMany(p => p.Lines)
            .Where(l => l.SalesOrderLineId != null)
            .ToListAsync(ct);
        return lines.GroupBy(l => (l.SalesOrderLineId!.Value, l.Size)).ToDictionary(g => g.Key, g => g.Sum(l => l.OrderQuantity));
    }

    private async Task<string> NextPlanNumberAsync(Guid unitId, string unitCode, CancellationToken ct)
    {
        var count = await db.Plans.IgnoreQueryFilters().CountAsync(p => p.UnitId == unitId, ct);
        return $"{unitCode}-{count + 1:0000}";
    }
}
