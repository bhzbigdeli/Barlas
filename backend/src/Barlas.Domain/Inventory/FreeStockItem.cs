using Barlas.Domain.Common;
using Barlas.Domain.Orders;

namespace Barlas.Domain.Inventory;

/// <summary>
/// موجودی آزاد انبار محصول: جفت‌هایی که به هیچ سفارشی تعلق ندارند (معمولاً مازاد تولید).
/// هر واحد موجودی خودش را دارد؛ انتقال بین واحدها فقط با دستور مالک یا مدیر کارخانه.
/// </summary>
public class FreeStockItem : AuditableEntity, IUnitScoped
{
    private FreeStockItem() { }

    public FreeStockItem(Guid unitId, SkuKey sku)
    {
        UnitId = unitId;
        ShoeModelId = sku.ShoeModelId;
        ColorId = sku.ColorId;
        Size = sku.Size;
    }

    public Guid UnitId { get; private set; }
    public Guid ShoeModelId { get; private set; }
    public Guid ColorId { get; private set; }
    public int Size { get; private set; }
    public int Quantity { get; private set; }

    public SkuKey Sku => new(ShoeModelId, ColorId, Size);

    public void Add(int qty)
    {
        if (qty <= 0) throw new DomainException("تعداد ورودی باید مثبت باشد.");
        Quantity += qty;
    }

    /// <summary>تا جای ممکن از موجودی برمی‌دارد و تعداد برداشته‌شده را برمی‌گرداند.</summary>
    public int Take(int requested)
    {
        var taken = Math.Min(requested, Quantity);
        Quantity -= taken;
        return taken;
    }
}

/// <summary>سابقه هر حرکت موجودی آزاد (ورود مازاد، رزرو برای سفارش، انتقال).</summary>
public class StockMovement : AuditableEntity, IUnitScoped
{
    private StockMovement() { }

    public StockMovement(Guid unitId, SkuKey sku, int quantityDelta, StockMovementReason reason, Guid? salesOrderLineId, Guid? bundleId)
    {
        UnitId = unitId;
        ShoeModelId = sku.ShoeModelId;
        ColorId = sku.ColorId;
        Size = sku.Size;
        QuantityDelta = quantityDelta;
        Reason = reason;
        SalesOrderLineId = salesOrderLineId;
        BundleId = bundleId;
    }

    public Guid UnitId { get; private set; }
    public Guid ShoeModelId { get; private set; }
    public Guid ColorId { get; private set; }
    public int Size { get; private set; }
    public int QuantityDelta { get; private set; }
    public StockMovementReason Reason { get; private set; }
    public Guid? SalesOrderLineId { get; private set; }
    public Guid? BundleId { get; private set; }
}

public enum StockMovementReason
{
    SurplusFromProduction = 1,
    ReservedForOrder = 2,
    TransferIn = 3,
    TransferOut = 4,
    Adjustment = 5,
}

/// <summary>
/// قدم اول چرخه: هر سفارش اول با موجودی آزاد سنجیده می‌شود؛ فقط باقیمانده به تولید می‌رود.
/// </summary>
public static class FreeStockAllocator
{
    public static IReadOnlyList<StockMovement> Allocate(SalesOrder order, IEnumerable<FreeStockItem> stock, DateTime nowUtc)
    {
        var bySku = stock.Where(s => s.UnitId == order.UnitId).ToDictionary(s => s.Sku);
        var movements = new List<StockMovement>();

        foreach (var line in order.Lines)
        foreach (var size in line.Sizes.Where(s => s.Outstanding > 0))
        {
            var sku = new SkuKey(line.ShoeModelId, line.ColorId, size.Size);
            if (!bySku.TryGetValue(sku, out var item)) continue;

            var taken = item.Take(size.Outstanding);
            if (taken == 0) continue;

            size.AddFromStock(taken);
            movements.Add(new StockMovement(order.UnitId, sku, -taken, StockMovementReason.ReservedForOrder, line.Id, null));
        }

        order.RefreshReadiness(nowUtc);
        return movements;
    }
}
