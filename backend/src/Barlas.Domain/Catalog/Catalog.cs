using Barlas.Domain.Common;

namespace Barlas.Domain.Catalog;

// اطلاعات پایه: بین همه واحدها مشترک است (IUnitScoped نیستند).

/// <summary>مرحله تولید، مثل برش، لیزر، پستاسازی، کفاشی، بسته‌بندی.</summary>
public class ProductionStage : AuditableEntity
{
    private ProductionStage() { }
    public ProductionStage(string code, string name) { Code = code; Name = name; }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
}

/// <summary>مسیر مراحل: ترتیب مراحلی که یک دسته باید طی کند.</summary>
public class ProductionRoute : AuditableEntity
{
    private readonly List<RouteStep> _steps = [];
    private ProductionRoute() { }
    public ProductionRoute(string name) => Name = name;

    public string Name { get; private set; } = default!;
    public IReadOnlyList<RouteStep> Steps => _steps.OrderBy(s => s.Sequence).ToList();

    public void AddStep(Guid stageId)
    {
        if (_steps.Any(s => s.StageId == stageId))
            throw new DomainException("این مرحله قبلاً در مسیر آمده است.");
        _steps.Add(new RouteStep(Id, _steps.Count + 1, stageId));
    }
}

public class RouteStep : Entity
{
    private RouteStep() { }
    internal RouteStep(Guid routeId, int sequence, Guid stageId)
    {
        RouteId = routeId;
        Sequence = sequence;
        StageId = stageId;
    }

    public Guid RouteId { get; private set; }
    public int Sequence { get; private set; }
    public Guid StageId { get; private set; }
}

/// <summary>پایه اطلاعات ساده کد + نام (رنگ، زیره).</summary>
public abstract class CodedItem : AuditableEntity
{
    protected CodedItem() { }
    protected CodedItem(string code, string name) { Code = CatalogRules.Code(code); Rename(name); }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public void Rename(string name) => Name = CatalogRules.Name(name);
    public void SetActive(bool active) => IsActive = active;
}

public class Color : CodedItem
{
    private Color() { }
    public Color(string code, string name) : base(code, name) { }
}

/// <summary>زیره.</summary>
public class Sole : CodedItem
{
    private Sole() { }
    public Sole(string code, string name) : base(code, name) { }
}

/// <summary>
/// محصول (مدل کفش). هر محصول سری خودش را دارد: بازه سایز و تعداد هر سایز در یک سری
/// با محصول دیگر فرق می‌کند (مثلاً ۳۷ تا ۴۲ با دو جفت ۴۰، یا ۴۰ تا ۴۵).
/// </summary>
public class ShoeModel : AuditableEntity
{
    public const int MinSize = 15;
    public const int MaxSize = 50;

    private readonly List<SeriesSize> _series = [];
    private ShoeModel() { }

    public ShoeModel(string code, string name, Guid routeId, Guid? soleId, IReadOnlyDictionary<int, int> series)
    {
        Code = CatalogRules.Code(code);
        Update(name, routeId, soleId, series);
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public Guid RouteId { get; private set; }
    public Guid? SoleId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>ترکیب یک سری: سایز → تعداد جفت.</summary>
    public IReadOnlyList<SeriesSize> Series => _series.OrderBy(s => s.Size).ToList();
    public int PairsPerSeries => _series.Sum(s => s.Quantity);
    public int? MinSeriesSize => _series.Count == 0 ? null : _series.Min(s => s.Size);
    public int? MaxSeriesSize => _series.Count == 0 ? null : _series.Max(s => s.Size);

    public void Update(string name, Guid routeId, Guid? soleId, IReadOnlyDictionary<int, int> series)
    {
        Name = CatalogRules.Name(name);
        RouteId = routeId;
        SoleId = soleId;
        SetSeries(series);
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>سایزهای با تعداد صفر حذف می‌شوند؛ حداقل یک سایز لازم است.</summary>
    public void SetSeries(IReadOnlyDictionary<int, int> series)
    {
        if (series.Values.Any(q => q < 0)) throw new DomainException("تعداد سایز در سری نمی‌تواند منفی باشد.");
        var sizes = series.Where(kv => kv.Value > 0).ToList();
        if (sizes.Count == 0) throw new DomainException("سری محصول باید حداقل یک سایز داشته باشد.");
        if (sizes.Any(kv => kv.Key is < MinSize or > MaxSize))
            throw new DomainException($"سایز باید بین {MinSize} و {MaxSize} باشد.");

        foreach (var existing in _series.ToList())
            if (!series.TryGetValue(existing.Size, out var q) || q <= 0) _series.Remove(existing);

        foreach (var (size, qty) in sizes)
        {
            var existing = _series.FirstOrDefault(s => s.Size == size);
            if (existing is null) _series.Add(new SeriesSize(Id, size, qty));
            else existing.Quantity = qty;
        }
    }

    /// <summary>تعداد جفت هر سایز برای n سری.</summary>
    public Dictionary<int, int> Expand(int seriesCount)
    {
        if (seriesCount <= 0) throw new DomainException("تعداد سری باید مثبت باشد.");
        return _series.ToDictionary(s => s.Size, s => s.Quantity * seriesCount);
    }
}

/// <summary>یک سایز در سری محصول و تعداد جفتش.</summary>
public class SeriesSize : Entity
{
    private SeriesSize() { }
    internal SeriesSize(Guid shoeModelId, int size, int quantity)
    {
        ShoeModelId = shoeModelId;
        Size = size;
        Quantity = quantity;
    }

    public Guid ShoeModelId { get; private set; }
    public int Size { get; private set; }
    public int Quantity { get; internal set; }
}

/// <summary>نرخ دستمزد کارمزدی هر مرحله (اختیاری برای یک مدل خاص). محاسبه دستمزد در فاز ۲.</summary>
public class PieceRate : AuditableEntity
{
    private PieceRate() { }
    public PieceRate(Guid stageId, Guid? shoeModelId, decimal ratePerPair, DateOnly effectiveFrom)
    {
        StageId = stageId;
        ShoeModelId = shoeModelId;
        RatePerPair = ratePerPair;
        EffectiveFrom = effectiveFrom;
    }

    public Guid StageId { get; private set; }
    public Guid? ShoeModelId { get; private set; }
    /// <summary>ریال به ازای هر جفت.</summary>
    public decimal RatePerPair { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
}

internal static class CatalogRules
{
    public static string Code(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("کد لازم است.");
        return code.Trim().ToUpperInvariant();
    }

    public static string Name(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("نام لازم است.");
        return name.Trim();
    }
}
