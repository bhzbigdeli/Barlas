using Barlas.Domain.Catalog;
using Barlas.Domain.Common;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;

namespace Barlas.Domain.Tests;

public class ShipmentTests
{
    private static readonly Guid Unit = Guid.NewGuid(), Model = Guid.NewGuid(), Color = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>سفارش ۱۰ جفت ۴۰ و ۱۰ جفت ۴۱ که ۶ جفت ۴۰ از موجودی آزاد گرفته.</summary>
    private static (SalesOrder Order, SalesOrderLine Line) OrderWithStock()
    {
        var order = new SalesOrder(Unit, "T-1", new DateOnly(2026, 11, 24), null, null);
        var line = order.AddLine(Model, Color, null, new Dictionary<int, int> { [40] = 10, [41] = 10 });
        var stock = new FreeStockItem(Unit, new SkuKey(Model, Color, 40));
        stock.Add(6);
        FreeStockAllocator.Allocate(order, [stock]);
        return (order, line);
    }

    [Fact]
    public void Ready_pairs_can_be_shipped_partially()
    {
        var (order, line) = OrderWithStock();

        var shipment = order.Ship("T-1-S1", [new ShipmentQuantity(line.Id, 40, 6)], Now);

        Assert.Equal(6, shipment.TotalPairs);
        Assert.Equal(SalesOrderStatus.PartiallyShipped, order.Status);
        Assert.Equal(6, line.Size(40).Shipped);
        Assert.Equal(0, line.Size(40).ReadyToShip);
        Assert.Equal(6, order.ShippedPairs);
    }

    [Fact]
    public void Cannot_ship_more_than_ready()
    {
        var (order, line) = OrderWithStock();
        var ex = Assert.Throws<DomainException>(() => order.Ship("S", [new ShipmentQuantity(line.Id, 41, 1)], Now));
        Assert.Contains("41", ex.Message);
    }

    [Fact]
    public void Shipping_everything_closes_the_order()
    {
        var order = new SalesOrder(Unit, "T-2", new DateOnly(2026, 11, 24), null, null);
        var line = order.AddLine(Model, Color, null, new Dictionary<int, int> { [40] = 4 });
        var stock = new FreeStockItem(Unit, new SkuKey(Model, Color, 40));
        stock.Add(4);
        FreeStockAllocator.Allocate(order, [stock]);

        order.Ship("S1", [new ShipmentQuantity(line.Id, 40, 1)], Now);
        Assert.Equal(SalesOrderStatus.ReadyToShip, order.Status);   // باقیمانده آماده است
        order.Ship("S2", [new ShipmentQuantity(line.Id, 40, 3)], Now);
        Assert.Equal(SalesOrderStatus.Shipped, order.Status);
        Assert.Throws<DomainException>(() => order.Ship("S3", [new ShipmentQuantity(line.Id, 40, 1)], Now));
    }

    [Fact]
    public void Product_without_series_cannot_be_ordered_by_series_count()
    {
        var model = new ShoeModel("W500", "زنانه 500", Guid.NewGuid(), null, new Dictionary<int, int>(), "زنانه صنعتی");
        Assert.Equal(0, model.PairsPerSeries);
        Assert.Throws<DomainException>(() => model.Expand(2));
    }
}
