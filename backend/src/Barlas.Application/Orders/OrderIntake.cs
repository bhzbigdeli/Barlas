using Barlas.Application.Abstractions;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Orders;

/// <summary>سفارش خوانده‌شده از سیستم فروش تهران، مستقل از منبع (API یا اکسل).</summary>
public sealed record ImportedOrder(
    string SupplierCode,
    string ExternalNumber,
    DateOnly OrderDate,
    DateOnly? DueDate,
    string? CustomerName,
    IReadOnlyList<ImportedOrderLine> Lines);

/// <summary>
/// ردیف سفارش: یا تعداد هر سایز (QuantitiesBySize) یا تعداد سری (SeriesCount) که طبق سری همان محصول باز می‌شود؛
/// اگر هر دو باشد جمع می‌شوند.
/// </summary>
public sealed record ImportedOrderLine(string ModelCode, string ColorCode, IReadOnlyDictionary<int, int> QuantitiesBySize, int? SeriesCount = null);

/// <summary>
/// منبع سفارش‌ها. روش اتصال به سیستم فروش تهران هنوز قطعی نیست؛
/// فعلاً پیاده‌سازی اکسل داریم و پیاده‌سازی API بعداً همین رابط را پیاده می‌کند.
/// </summary>
public interface IOrderSource
{
    Task<IReadOnlyList<ImportedOrder>> ReadAsync(Stream input, CancellationToken ct);
}

public sealed record IntakeResult(int Created, int Skipped, IReadOnlyList<string> Errors);

/// <summary>
/// دریافت سفارش: تخصیص به واحد با کد تامین‌کننده، ساخت سفارش، و سنجش فوری با موجودی آزاد.
/// سفارش تکراری (همان شماره در همان واحد) نادیده گرفته می‌شود تا ورود دوباره فایل بی‌خطر باشد.
/// </summary>
public class OrderIntakeService(IAppDbContext db)
{
    public async Task<IntakeResult> IntakeAsync(IEnumerable<ImportedOrder> orders, CancellationToken ct)
    {
        var units = await db.Units.Where(u => u.IsActive).ToDictionaryAsync(u => u.SupplierCode, ct);
        var models = await db.ShoeModels.Include(m => m.Series).Where(m => m.IsActive).ToDictionaryAsync(m => m.Code, ct);
        var colors = await db.Colors.ToDictionaryAsync(c => c.Code, c => c.Id, ct);

        int created = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var imported in orders)
        {
            if (!units.TryGetValue(imported.SupplierCode, out var unit))
            {
                errors.Add($"سفارش {imported.ExternalNumber}: کد تامین‌کننده «{imported.SupplierCode}» به هیچ واحدی تعلق ندارد.");
                continue;
            }

            // دریافت سفارش برای همه واحدهاست، پس فیلتر واحدِ کاربر جاری را کنار می‌گذاریم.
            var exists = await db.SalesOrders.IgnoreQueryFilters()
                .AnyAsync(o => o.UnitId == unit.Id && o.ExternalNumber == imported.ExternalNumber, ct);
            if (exists) { skipped++; continue; }

            var order = new SalesOrder(unit.Id, imported.ExternalNumber, imported.OrderDate, imported.DueDate, imported.CustomerName);
            var lineErrors = new List<string>();
            foreach (var l in imported.Lines)
            {
                if (!models.TryGetValue(l.ModelCode.Trim().ToUpperInvariant(), out var model)) { lineErrors.Add($"مدل «{l.ModelCode}» تعریف نشده"); continue; }
                if (!colors.TryGetValue(l.ColorCode.Trim().ToUpperInvariant(), out var colorId)) { lineErrors.Add($"رنگ «{l.ColorCode}» تعریف نشده"); continue; }

                var quantities = new Dictionary<int, int>(l.QuantitiesBySize);
                if (l.SeriesCount is > 0)
                    foreach (var (size, qty) in model.Expand(l.SeriesCount.Value))
                        quantities[size] = quantities.GetValueOrDefault(size) + qty;

                order.AddLine(model.Id, colorId, l.SeriesCount, quantities);
            }

            if (lineErrors.Count > 0)
            {
                errors.Add($"سفارش {imported.ExternalNumber}: {string.Join("، ", lineErrors)}");
                continue;
            }

            db.SalesOrders.Add(order);

            var stock = await db.FreeStock.IgnoreQueryFilters().Where(s => s.UnitId == unit.Id && s.Quantity > 0).ToListAsync(ct);
            db.StockMovements.AddRange(FreeStockAllocator.Allocate(order, stock));
            created++;
        }

        await db.SaveChangesAsync(ct);
        return new IntakeResult(created, skipped, errors);
    }
}
