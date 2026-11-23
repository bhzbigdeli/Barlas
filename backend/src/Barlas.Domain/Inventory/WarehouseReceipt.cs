using Barlas.Domain.Common;
using Barlas.Domain.Orders;
using Barlas.Domain.Production;

namespace Barlas.Domain.Inventory;

/// <summary>
/// ورود یک دسته تمام‌شده به انبار محصول: اول سهم سفارش پر می‌شود، بقیه مازاد است و به موجودی آزاد می‌رود.
/// </summary>
public static class WarehouseReceipt
{
    public sealed record Result(int ToOrder, int ToFreeStock, IReadOnlyList<StockMovement> Movements);

    /// <param name="order">سفارش زیربرنامه این دسته (برای تولید انباری null).</param>
    /// <param name="freeStock">ردیف‌های موجودی آزاد موجود؛ ردیف‌های جدید با getOrCreate ساخته می‌شوند.</param>
    public static Result Receive(Bundle bundle, SalesOrder? order, Func<SkuKey, FreeStockItem> getOrCreateFreeStock, DateTime nowUtc)
    {
        bundle.MarkInWarehouse(nowUtc);

        var line = order?.Lines.FirstOrDefault(l => l.ShoeModelId == bundle.ShoeModelId && l.ColorId == bundle.ColorId);
        var movements = new List<StockMovement>();
        int toOrder = 0, toStock = 0;

        foreach (var size in bundle.Sizes)
        {
            var orderSize = line?.Sizes.FirstOrDefault(s => s.Size == size.Size);
            var forOrder = Math.Min(size.Quantity, orderSize?.Outstanding ?? 0);
            if (forOrder > 0)
            {
                orderSize!.AddFromProduction(forOrder);
                toOrder += forOrder;
            }

            var surplus = size.Quantity - forOrder;
            if (surplus > 0)
            {
                var sku = new SkuKey(bundle.ShoeModelId, bundle.ColorId, size.Size);
                getOrCreateFreeStock(sku).Add(surplus);
                movements.Add(new StockMovement(bundle.UnitId, sku, surplus, StockMovementReason.SurplusFromProduction, null, bundle.Id));
                toStock += surplus;
            }
        }

        order?.RefreshReadiness(nowUtc);
        return new Result(toOrder, toStock, movements);
    }
}
