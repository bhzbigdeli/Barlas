using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Catalog;

public sealed record SeriesSizeDto(int Size, int Quantity);

public sealed record ProductDto(
    Guid Id, string Code, string Name, string? Group, Guid RouteId, string RouteName, Guid? SoleId, string? SoleName,
    bool IsActive, IReadOnlyList<SeriesSizeDto> Series, int PairsPerSeries, IReadOnlyList<PieceRateDto> Rates,
    string? Description = null, DateTime? ImageUploadedAtUtc = null);

/// <summary>دستمزد کارمزدی یک مرحله برای یک جفت (ریال)، از تاریخ آنالیز.</summary>
public sealed record PieceRateDto(string StageName, decimal RatePerPair, DateOnly EffectiveFrom);

public sealed record SaveProductRequest(
    string Code, string Name, Guid RouteId, Guid? SoleId, IReadOnlyList<SeriesSizeDto> Series, bool IsActive = true,
    string? Group = null, string? Description = null);

public sealed record CodedItemDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record SaveCodedItemRequest(string Code, string Name, bool IsActive = true);

public sealed record RouteDto(Guid Id, string Name, IReadOnlyList<string> Stages);

/// <summary>تعریف محصول و اطلاعات پایه (رنگ، زیره). خواندن برای همه، نوشتن با مجوز «تعریف محصول».</summary>
public partial class CatalogService(IAppDbContext db, IClock clock)
{
    // ---------- محصول ----------

    public async Task<IReadOnlyList<ProductDto>> ListProductsAsync(CancellationToken ct)
    {
        var models = await db.ShoeModels.AsNoTracking().Include(m => m.Series).OrderBy(m => m.Code).ToListAsync(ct);
        var (routes, soles) = await LookupsAsync(ct);
        var images = await db.ProductImages.ToDictionaryAsync(i => i.ShoeModelId, i => i.UploadedAtUtc, ct);
        return models.Select(m => ToDto(m, routes, soles) with { ImageUploadedAtUtc = images.TryGetValue(m.Id, out var at) ? at : null }).ToList();
    }

    public async Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct)
    {
        var model = await db.ShoeModels.AsNoTracking().Include(m => m.Series).FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw AppException.NotFound("محصول");
        var (routes, soles) = await LookupsAsync(ct);
        var stages = await db.Stages.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var route = await db.Routes.AsNoTracking().Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == model.RouteId, ct);
        var order = route?.Steps.ToDictionary(x => x.StageId, x => x.Sequence) ?? [];
        var rates = (await db.PieceRates.AsNoTracking().Where(r => r.ShoeModelId == id).ToListAsync(ct))
            .OrderBy(r => order.GetValueOrDefault(r.StageId, int.MaxValue)).ThenByDescending(r => r.EffectiveFrom)
            .Select(r => new PieceRateDto(stages[r.StageId], r.RatePerPair, r.EffectiveFrom)).ToList();
        var image = await db.ProductImages.Where(i => i.ShoeModelId == id).Select(i => (DateTime?)i.UploadedAtUtc).FirstOrDefaultAsync(ct);
        return ToDto(model, routes, soles, rates) with { ImageUploadedAtUtc = image };
    }

    public async Task<ProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.ShoeModels.AnyAsync(m => m.Code == code, ct))
            throw new AppException($"کد محصول «{code}» قبلاً تعریف شده است.", 409);
        await EnsureReferencesAsync(request, ct);

        var model = new ShoeModel(code, request.Name, request.RouteId, request.SoleId, ToSeries(request.Series), request.Group);
        model.SetActive(request.IsActive);
        model.SetDescription(request.Description);
        db.ShoeModels.Add(model);
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(model.Id, ct);
    }

    /// <summary>کد محصول بعد از ساخت عوض نمی‌شود (سفارش‌های تهران با همین کد می‌آیند).</summary>
    public async Task<ProductDto> UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        var model = await db.ShoeModels.Include(m => m.Series).FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw AppException.NotFound("محصول");
        await EnsureReferencesAsync(request, ct);

        // سایز تازه در سری → Added، سایز حذف‌شده → Delete (شناسه‌ها ValueGeneratedNever‌اند).
        model.Update(request.Name, request.RouteId, request.SoleId, ToSeries(request.Series), request.Group);
        model.SetActive(request.IsActive);
        model.SetDescription(request.Description);
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(id, ct);
    }

    private async Task EnsureReferencesAsync(SaveProductRequest request, CancellationToken ct)
    {
        if (!await db.Routes.AnyAsync(r => r.Id == request.RouteId, ct)) throw AppException.NotFound("مسیر مراحل");
        if (request.SoleId is { } soleId && !await db.Soles.AnyAsync(s => s.Id == soleId, ct)) throw AppException.NotFound("زیره");
        if (request.Series.GroupBy(s => s.Size).Any(g => g.Count() > 1))
            throw new AppException("یک سایز دو بار در سری آمده است.");
    }

    private static Dictionary<int, int> ToSeries(IEnumerable<SeriesSizeDto> series) =>
        series.ToDictionary(s => s.Size, s => s.Quantity);

    private async Task<(Dictionary<Guid, string> Routes, Dictionary<Guid, string> Soles)> LookupsAsync(CancellationToken ct) =>
        (await db.Routes.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct),
         await db.Soles.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct));

    private static ProductDto ToDto(ShoeModel m, IReadOnlyDictionary<Guid, string> routes, IReadOnlyDictionary<Guid, string> soles,
        IReadOnlyList<PieceRateDto>? rates = null) =>
        new(m.Id, m.Code, m.Name, m.Group, m.RouteId, routes.GetValueOrDefault(m.RouteId, "?"),
            m.SoleId, m.SoleId is { } s ? soles.GetValueOrDefault(s) : null, m.IsActive,
            m.Series.Select(x => new SeriesSizeDto(x.Size, x.Quantity)).ToList(), m.PairsPerSeries, rates ?? [], m.Description);

    // ---------- رنگ و زیره ----------

    public Task<IReadOnlyList<CodedItemDto>> ListColorsAsync(CancellationToken ct) => ListAsync(db.Colors, ct);
    public Task<IReadOnlyList<CodedItemDto>> ListSolesAsync(CancellationToken ct) => ListAsync(db.Soles, ct);

    public Task<CodedItemDto> CreateColorAsync(SaveCodedItemRequest r, CancellationToken ct) =>
        CreateAsync(db.Colors, r, () => new Color(r.Code, r.Name), "رنگ", ct);
    public Task<CodedItemDto> CreateSoleAsync(SaveCodedItemRequest r, CancellationToken ct) =>
        CreateAsync(db.Soles, r, () => new Sole(r.Code, r.Name), "زیره", ct);

    public Task<CodedItemDto> UpdateColorAsync(Guid id, SaveCodedItemRequest r, CancellationToken ct) => UpdateAsync(db.Colors, id, r, "رنگ", ct);
    public Task<CodedItemDto> UpdateSoleAsync(Guid id, SaveCodedItemRequest r, CancellationToken ct) => UpdateAsync(db.Soles, id, r, "زیره", ct);

    private static async Task<IReadOnlyList<CodedItemDto>> ListAsync<T>(DbSet<T> set, CancellationToken ct) where T : CodedItem =>
        await set.AsNoTracking().OrderBy(x => x.Code).Select(x => new CodedItemDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct);

    private async Task<CodedItemDto> CreateAsync<T>(DbSet<T> set, SaveCodedItemRequest r, Func<T> create, string what, CancellationToken ct)
        where T : CodedItem
    {
        var code = r.Code.Trim().ToUpperInvariant();
        if (await set.AnyAsync(x => x.Code == code, ct)) throw new AppException($"کد {what} «{code}» قبلاً تعریف شده است.", 409);
        var item = create();
        item.SetActive(r.IsActive);
        set.Add(item);
        await db.SaveChangesAsync(ct);
        return new CodedItemDto(item.Id, item.Code, item.Name, item.IsActive);
    }

    private async Task<CodedItemDto> UpdateAsync<T>(DbSet<T> set, Guid id, SaveCodedItemRequest r, string what, CancellationToken ct)
        where T : CodedItem
    {
        var item = await set.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound(what);
        item.Rename(r.Name);
        item.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return new CodedItemDto(item.Id, item.Code, item.Name, item.IsActive);
    }

    // ---------- مسیر مراحل (فعلاً فقط خواندنی) ----------

    public async Task<IReadOnlyList<RouteDto>> ListRoutesAsync(CancellationToken ct)
    {
        var routes = await db.Routes.AsNoTracking().Include(r => r.Steps).ToListAsync(ct);
        var stages = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return routes.OrderBy(r => r.Name)
            .Select(r => new RouteDto(r.Id, r.Name, r.Steps.Select(s => stages[s.StageId]).ToList()))
            .ToList();
    }
}
