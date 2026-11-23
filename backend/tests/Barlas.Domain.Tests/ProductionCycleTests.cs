using Barlas.Domain.Common;
using Barlas.Domain.Identity;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;

namespace Barlas.Domain.Tests;

/// <summary>آزمون چرخه کار طبق سند طرح کلی: موجودی آزاد → زیربرنامه → دسته → مراحل → انبار → مازاد.</summary>
public class ProductionCycleTests
{
    private static readonly Guid Unit = Guid.NewGuid();
    private static readonly Guid OtherUnit = Guid.NewGuid();
    private static readonly Guid Model = Guid.NewGuid();
    private static readonly Guid Color = Guid.NewGuid();
    private static readonly Guid[] Route = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);

    private static SalesOrder Order(params (int size, int qty)[] sizes)
    {
        var order = new SalesOrder(Unit, "T-1", new DateOnly(2026, 11, 24), null, null);
        order.AddLine(Model, Color, null, sizes.ToDictionary(s => s.size, s => s.qty));
        return order;
    }

    private static FreeStockItem Stock(Guid unit, int size, int qty)
    {
        var item = new FreeStockItem(unit, new SkuKey(Model, Color, size));
        item.Add(qty);
        return item;
    }

    private static Bundle NewBundle(params (int size, int qty)[] sizes) =>
        new(Unit, Guid.NewGuid(), "B-1", Model, Color, sizes.ToDictionary(s => s.size, s => s.qty), Route);

    [Fact]
    public void Order_takes_from_free_stock_first_and_only_rest_goes_to_production()
    {
        var order = Order((40, 10), (41, 10));
        var stock = new[] { Stock(Unit, 40, 4), Stock(Unit, 41, 25) };

        var moves = FreeStockAllocator.Allocate(order, stock, Now);

        var line = order.Lines[0];
        Assert.Equal(6, line.Size(40).Outstanding);
        Assert.Equal(0, line.Size(41).Outstanding);
        Assert.Equal(0, stock[0].Quantity);
        Assert.Equal(15, stock[1].Quantity);
        Assert.Equal(2, moves.Count);
        Assert.All(moves, m => Assert.Equal(StockMovementReason.ReservedForOrder, m.Reason));
    }

    [Fact]
    public void Free_stock_of_another_unit_is_never_used()
    {
        var order = Order((40, 10));
        var moves = FreeStockAllocator.Allocate(order, [Stock(OtherUnit, 40, 50)], Now);

        Assert.Empty(moves);
        Assert.Equal(10, order.Lines[0].Size(40).Outstanding);
    }

    [Fact]
    public void Order_fully_covered_by_stock_becomes_ready_to_ship()
    {
        var order = Order((40, 3));
        FreeStockAllocator.Allocate(order, [Stock(Unit, 40, 3)], Now);
        Assert.Equal(SalesOrderStatus.ReadyToShip, order.Status);
    }

    [Theory]
    [InlineData(20, PlanStatus.Approved)]          // دقیقاً ۲۰٪ ← بدون تأیید
    [InlineData(21, PlanStatus.AwaitingApproval)]  // بیش از ۲۰٪ ← تأیید مدیر واحد
    public void Surplus_above_threshold_needs_unit_manager_approval(int surplus, PlanStatus expected)
    {
        var plan = new ProductionPlan(Unit, "P-1", Guid.NewGuid());
        plan.SetLine(new SkuKey(Model, Color, 40), null, orderQuantity: 100, surplusQuantity: surplus);

        plan.Submit(0.20m, Now);

        Assert.Equal(expected, plan.Status);
    }

    [Fact]
    public void Approval_only_applies_to_plans_awaiting_it()
    {
        var plan = new ProductionPlan(Unit, "P-1", null);
        plan.SetLine(new SkuKey(Model, Color, 40), null, 10, 10);
        Assert.Throws<DomainException>(() => plan.Approve(Guid.NewGuid(), Now));

        plan.Submit(0.20m, Now);
        plan.Approve(Guid.NewGuid(), Now);
        Assert.Equal(PlanStatus.Approved, plan.Status);
    }

    [Fact]
    public void Bundle_stages_must_follow_route_order()
    {
        var bundle = NewBundle((40, 12));
        var worker = Guid.NewGuid();

        Assert.Throws<DomainException>(() => bundle.CompleteStage(Route[1], worker, worker, Now));

        bundle.CompleteStage(Route[0], worker, worker, Now);
        bundle.CompleteStage(Route[1], worker, worker, Now);
        Assert.Equal(BundleStatus.InProgress, bundle.Status);
        Assert.Equal(Route[2], bundle.NextStageId);

        bundle.CompleteStage(Route[2], worker, worker, Now);
        Assert.Equal(BundleStatus.Finished, bundle.Status);
        Assert.Null(bundle.NextStageId);
        Assert.Equal(3, bundle.Scans.Count);
        Assert.All(bundle.Scans, s => Assert.Equal(12, s.Pairs));
    }

    [Fact]
    public void Rework_sends_bundle_back_to_an_earlier_stage()
    {
        var bundle = NewBundle((40, 5));
        var w = Guid.NewGuid();
        bundle.CompleteStage(Route[0], w, w, Now);
        bundle.CompleteStage(Route[1], w, w, Now);

        bundle.SendBackForRework(Route[1], w, w, Now, "دوخت ایراد دارد");

        Assert.Equal(Route[1], bundle.NextStageId);
        Assert.Throws<DomainException>(() => bundle.SendBackForRework(Route[2], w, w, Now, null));
    }

    [Fact]
    public void Warehouse_receipt_fills_order_first_and_rest_goes_to_free_stock()
    {
        var order = Order((40, 10), (41, 5));
        var bundle = NewBundle((40, 12), (41, 5), (42, 3));
        var w = Guid.NewGuid();
        foreach (var stage in Route) bundle.CompleteStage(stage, w, w, Now);

        var stock = new Dictionary<SkuKey, FreeStockItem>();
        var result = WarehouseReceipt.Receive(bundle, order,
            sku => stock.TryGetValue(sku, out var s) ? s : stock[sku] = new FreeStockItem(Unit, sku), Now);

        Assert.Equal(15, result.ToOrder);
        Assert.Equal(5, result.ToFreeStock);
        Assert.Equal(2, stock[new SkuKey(Model, Color, 40)].Quantity);
        Assert.Equal(3, stock[new SkuKey(Model, Color, 42)].Quantity);
        Assert.Equal(SalesOrderStatus.ReadyToShip, order.Status);
        Assert.Equal(BundleStatus.InWarehouse, bundle.Status);
        Assert.Equal(Now, bundle.InWarehouseAtUtc);
        Assert.Equal(Now, order.ReadyAtUtc);
    }

    [Fact]
    public void Unfinished_bundle_cannot_enter_warehouse()
    {
        var bundle = NewBundle((40, 1));
        Assert.Throws<DomainException>(() => WarehouseReceipt.Receive(bundle, null, sku => new FreeStockItem(Unit, sku), Now));
    }

    [Fact]
    public void Qr_tokens_are_unique_and_unguessable()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => NewBundle((40, 1)).QrToken).ToHashSet();
        Assert.Equal(200, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(24, t.Length));
    }
}
