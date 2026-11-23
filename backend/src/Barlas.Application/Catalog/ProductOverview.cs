using Barlas.Application.Common;
using Barlas.Domain.Catalog;
using Barlas.Domain.Orders;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Catalog;

public sealed record ProductOrderRow(Guid OrderId, string OrderNumber, string? CustomerName, string ColorName, int Pairs, int ShippedPairs,
    SalesOrderStatus Status, DateOnly OrderDate, DateOnly? DueDate);

public sealed record ProductPlanRow(Guid PlanId, string PlanNumber, PlanStatus Status, int Pairs, DateOnly? PlannedEndDate, DateTime? CompletedAtUtc);

public sealed record ProductStockRow(string UnitName, string ColorName, int Size, int Quantity);

public sealed record ProductWipRow(string StageName, int Bundles, int Pairs);

public sealed record ProductOverview(ProductDto Product, string? RouteStages, int OrderedPairs, int ProducedPairs, int ShippedPairs,
    int FreeStockPairs, decimal LaborPerPair, IReadOnlyList<ProductOrderRow> Orders, IReadOnlyList<ProductPlanRow> Plans,
    IReadOnlyList<ProductStockRow> Stock, IReadOnlyList<ProductWipRow> Wip);

public sealed record ImageFile(byte[] Data, string ContentType, DateTime UploadedAtUtc);

public partial class CatalogService
{
    /// <summary>صفحه اختصاصی محصول: مشخصات، دستمزد، سفارش‌ها، زیربرنامه‌ها، موجودی و کار در جریان.</summary>
    public async Task<ProductOverview> GetProductOverviewAsync(Guid id, CancellationToken ct)
    {
        var product = await GetProductAsync(id, ct);
        var route = await db.Routes.AsNoTracking().Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == product.RouteId, ct);
        var stages = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var orders = await db.SalesOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Sizes)
            .Where(o => o.Lines.Any(l => l.ShoeModelId == id)).OrderByDescending(o => o.OrderDate).Take(100).ToListAsync(ct);
        var orderRows = orders.SelectMany(o => o.Lines.Where(l => l.ShoeModelId == id).Select(l => new ProductOrderRow(
                o.Id, o.ExternalNumber, o.CustomerName, colors.GetValueOrDefault(l.ColorId, "?"), l.TotalPairs, l.Sizes.Sum(s => s.Shipped),
                o.Status, o.OrderDate, o.DueDate)))
            .ToList();

        var plans = await db.Plans.AsNoTracking().Include(p => p.Lines)
            .Where(p => p.Lines.Any(l => l.ShoeModelId == id)).OrderByDescending(p => p.CreatedAtUtc).Take(100).ToListAsync(ct);
        var planRows = plans.Select(p => new ProductPlanRow(p.Id, p.Number, p.Status,
            p.Lines.Where(l => l.ShoeModelId == id).Sum(l => l.OrderQuantity + l.SurplusQuantity), p.PlannedEndDate, p.CompletedAtUtc)).ToList();

        var stock = await db.FreeStock.AsNoTracking().Where(s => s.ShoeModelId == id && s.Quantity > 0).ToListAsync(ct);
        var bundles = await db.Bundles.AsNoTracking().Include(b => b.Sizes).Where(b => b.ShoeModelId == id).ToListAsync(ct);
        var wip = bundles.Where(b => b.Status is BundleStatus.Issued or BundleStatus.InProgress or BundleStatus.Finished)
            .GroupBy(b => b.Status == BundleStatus.Finished ? "آماده ورود به انبار" : stages[b.NextStageId!.Value])
            .Select(g => new ProductWipRow(g.Key, g.Count(), g.Sum(b => b.TotalPairs))).OrderBy(r => r.StageName).ToList();

        var today = LocalCalendar.Date(clock.UtcNow);
        var wages = new WageTable(await db.PieceRates.AsNoTracking().Where(r => r.ShoeModelId == id || r.ShoeModelId == null).ToListAsync(ct));
        var routeStages = route?.Steps.Select(s => s.StageId).ToList() ?? [];

        return new ProductOverview(product,
            route is null ? null : string.Join(" ← ", routeStages.Select(s => stages[s])),
            orderRows.Sum(r => r.Pairs),
            bundles.Where(b => b.Status == BundleStatus.InWarehouse).Sum(b => b.TotalPairs),
            orderRows.Sum(r => r.ShippedPairs),
            stock.Sum(s => s.Quantity),
            routeStages.Sum(s => wages.Rate(id, s, today)),
            orderRows, planRows,
            stock.Select(s => new ProductStockRow(units[s.UnitId], colors.GetValueOrDefault(s.ColorId, "?"), s.Size, s.Quantity))
                .OrderBy(r => r.UnitName).ThenBy(r => r.ColorName).ThenBy(r => r.Size).ToList(),
            wip);
    }

    public async Task<ImageFile?> GetImageAsync(Guid productId, CancellationToken ct) =>
        await db.ProductImages.AsNoTracking().Where(i => i.ShoeModelId == productId)
            .Select(i => new ImageFile(i.Data, i.ContentType, i.UploadedAtUtc)).FirstOrDefaultAsync(ct);

    public async Task SetImageAsync(Guid productId, string contentType, byte[] data, CancellationToken ct)
    {
        if (!await db.ShoeModels.AnyAsync(m => m.Id == productId, ct)) throw AppException.NotFound("محصول");
        var image = await db.ProductImages.FirstOrDefaultAsync(i => i.ShoeModelId == productId, ct);
        if (image is null) db.ProductImages.Add(new ProductImage(productId, contentType, data, clock.UtcNow));
        else image.Replace(contentType, data, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteImageAsync(Guid productId, CancellationToken ct)
    {
        var image = await db.ProductImages.FirstOrDefaultAsync(i => i.ShoeModelId == productId, ct);
        if (image is null) return;
        db.ProductImages.Remove(image);
        await db.SaveChangesAsync(ct);
    }
}
