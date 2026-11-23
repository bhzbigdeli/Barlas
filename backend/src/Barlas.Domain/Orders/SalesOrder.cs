using Barlas.Domain.Common;

namespace Barlas.Domain.Orders;

public enum SalesOrderStatus
{
    Received = 1,
    Planned = 2,
    InProduction = 3,
    ReadyToShip = 4,
    Shipped = 5,
    Cancelled = 9,
}

/// <summary>سفارشی که از سیستم فروش تهران دریافت و با کد تامین‌کننده به یک واحد داده شده است.</summary>
public class SalesOrder : AuditableEntity, IUnitScoped
{
    private readonly List<SalesOrderLine> _lines = [];
    private SalesOrder() { }

    public SalesOrder(Guid unitId, string externalNumber, DateOnly orderDate, DateOnly? dueDate, string? customerName)
    {
        UnitId = unitId;
        ExternalNumber = externalNumber;
        OrderDate = orderDate;
        DueDate = dueDate;
        CustomerName = customerName;
    }

    public Guid UnitId { get; private set; }

    /// <summary>شماره سفارش در سیستم فروش تهران (یکتا در هر واحد).</summary>
    public string ExternalNumber { get; private set; } = default!;
    public DateOnly OrderDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string? CustomerName { get; private set; }
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Received;
    public IReadOnlyList<SalesOrderLine> Lines => _lines;

    public int TotalPairs => _lines.Sum(l => l.TotalPairs);
    public int FulfilledPairs => _lines.Sum(l => l.Sizes.Sum(s => s.FromStock + s.FromProduction));

    public SalesOrderLine AddLine(Guid shoeModelId, Guid colorId, int? seriesCount, IReadOnlyDictionary<int, int> quantitiesBySize)
    {
        if (Status != SalesOrderStatus.Received)
            throw new DomainException("بعد از برنامه‌ریزی، ردیف سفارش قابل تغییر نیست.");
        var line = new SalesOrderLine(Id, _lines.Count + 1, shoeModelId, colorId, seriesCount, quantitiesBySize);
        _lines.Add(line);
        return line;
    }

    public void MarkPlanned() => Transition(SalesOrderStatus.Received, SalesOrderStatus.Planned);
    public void MarkInProduction() => Transition(SalesOrderStatus.Planned, SalesOrderStatus.InProduction);

    public void MarkShipped() => Transition(SalesOrderStatus.ReadyToShip, SalesOrderStatus.Shipped);

    /// <summary>اگر همه سایزها کامل شده باشند، سفارش «آماده ارسال» می‌شود.</summary>
    public void RefreshReadiness()
    {
        if (Status is SalesOrderStatus.Shipped or SalesOrderStatus.Cancelled) return;
        if (_lines.All(l => l.Sizes.All(s => s.Outstanding == 0)))
            Status = SalesOrderStatus.ReadyToShip;
    }

    public void Cancel()
    {
        if (Status == SalesOrderStatus.Shipped) throw new DomainException("سفارش ارسال‌شده قابل لغو نیست.");
        Status = SalesOrderStatus.Cancelled;
    }

    private void Transition(SalesOrderStatus from, SalesOrderStatus to)
    {
        if (Status != from) throw new DomainException($"تغییر وضعیت سفارش از {Status} به {to} مجاز نیست.");
        Status = to;
    }
}

/// <summary>ردیف سفارش: یک مدل و رنگ با تعداد هر سایز.</summary>
public class SalesOrderLine : Entity
{
    private readonly List<SalesOrderLineSize> _sizes = [];
    private SalesOrderLine() { }

    internal SalesOrderLine(Guid salesOrderId, int lineNumber, Guid shoeModelId, Guid colorId, int? seriesCount, IReadOnlyDictionary<int, int> quantitiesBySize)
    {
        if (quantitiesBySize.Count == 0 || quantitiesBySize.Values.Any(q => q < 0) || quantitiesBySize.Values.Sum() == 0)
            throw new DomainException("ردیف سفارش باید حداقل یک سایز با تعداد مثبت داشته باشد.");

        SalesOrderId = salesOrderId;
        LineNumber = lineNumber;
        ShoeModelId = shoeModelId;
        ColorId = colorId;
        SeriesCount = seriesCount;
        foreach (var (size, qty) in quantitiesBySize.Where(kv => kv.Value > 0))
            _sizes.Add(new SalesOrderLineSize(Id, size, qty));
    }

    public Guid SalesOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ShoeModelId { get; private set; }
    public Guid ColorId { get; private set; }
    /// <summary>اگر سفارش به‌صورت «تعداد سری» آمده باشد (طبق سری همان محصول).</summary>
    public int? SeriesCount { get; private set; }
    public IReadOnlyList<SalesOrderLineSize> Sizes => _sizes;
    public int TotalPairs => _sizes.Sum(s => s.Quantity);

    public SalesOrderLineSize Size(int size) =>
        _sizes.FirstOrDefault(s => s.Size == size) ?? throw new DomainException($"سایز {size} در این ردیف نیست.");
}

/// <summary>تعداد یک سایز در ردیف سفارش و اینکه چقدرش از موجودی آزاد و چقدر از تولید تأمین شده.</summary>
public class SalesOrderLineSize : Entity
{
    private SalesOrderLineSize() { }
    internal SalesOrderLineSize(Guid lineId, int size, int quantity)
    {
        SalesOrderLineId = lineId;
        Size = size;
        Quantity = quantity;
    }

    public Guid SalesOrderLineId { get; private set; }
    public int Size { get; private set; }
    public int Quantity { get; private set; }

    /// <summary>تعداد رزروشده از موجودی آزاد انبار.</summary>
    public int FromStock { get; private set; }

    /// <summary>تعداد تحویل‌شده از تولید به انبار برای این سفارش.</summary>
    public int FromProduction { get; private set; }

    public int Outstanding => Quantity - FromStock - FromProduction;

    internal void AddFromStock(int qty) { Guard(qty); FromStock += qty; }
    internal void AddFromProduction(int qty) { Guard(qty); FromProduction += qty; }

    private void Guard(int qty)
    {
        if (qty <= 0 || qty > Outstanding)
            throw new DomainException($"تعداد {qty} برای سایز {Size} بیش از باقیمانده سفارش ({Outstanding}) است.");
    }
}
