using Barlas.Domain.Common;

namespace Barlas.Domain.Planning;

public enum PlanStatus
{
    Draft = 1,
    /// <summary>مازاد بیش از آستانه است و منتظر تأیید مدیر واحد.</summary>
    AwaitingApproval = 2,
    Approved = 3,
    /// <summary>دسته‌ها صادر و کاردکس‌ها چاپ شده‌اند.</summary>
    Released = 4,
    Completed = 5,
    Cancelled = 9,
}

/// <summary>
/// زیربرنامه تولید. مدیر فنی باقیمانده سفارش را به یک یا چند زیربرنامه تقسیم می‌کند
/// و می‌تواند مازاد هم برنامه‌ریزی کند؛ مازاد به موجودی آزاد می‌رود.
/// </summary>
public class ProductionPlan : AuditableEntity, IUnitScoped
{
    private readonly List<PlanLine> _lines = [];
    private ProductionPlan() { }

    public ProductionPlan(Guid unitId, string number, Guid? salesOrderId)
    {
        UnitId = unitId;
        Number = number;
        SalesOrderId = salesOrderId;
    }

    public Guid UnitId { get; private set; }
    public string Number { get; private set; } = default!;

    /// <summary>سفارش مبنا؛ خالی یعنی تولید برای انبار.</summary>
    public Guid? SalesOrderId { get; private set; }
    public PlanStatus Status { get; private set; } = PlanStatus.Draft;
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public IReadOnlyList<PlanLine> Lines => _lines;

    public int OrderPairs => _lines.Sum(l => l.OrderQuantity);
    public int SurplusPairs => _lines.Sum(l => l.SurplusQuantity);
    public int TotalPairs => OrderPairs + SurplusPairs;

    /// <summary>نسبت مازاد به سهم سفارش. برای برنامه بدون سفارش، بی‌نهایت در نظر گرفته می‌شود.</summary>
    public decimal SurplusRatio => OrderPairs == 0
        ? (SurplusPairs > 0 ? decimal.MaxValue : 0)
        : (decimal)SurplusPairs / OrderPairs;

    public void SetLine(SkuKey sku, Guid? salesOrderLineId, int orderQuantity, int surplusQuantity)
    {
        EnsureEditable();
        if (orderQuantity < 0 || surplusQuantity < 0 || orderQuantity + surplusQuantity == 0)
            throw new DomainException("تعداد ردیف برنامه نامعتبر است.");

        _lines.RemoveAll(l => l.Sku == sku);
        _lines.Add(new PlanLine(Id, sku, salesOrderLineId, orderQuantity, surplusQuantity));
    }

    /// <summary>
    /// ثبت نهایی توسط مدیر فنی. اگر مازاد از آستانه واحد بیشتر باشد، به انتظار تأیید مدیر واحد می‌رود.
    /// </summary>
    public void Submit(decimal surplusApprovalThreshold)
    {
        EnsureEditable();
        if (_lines.Count == 0) throw new DomainException("زیربرنامه خالی است.");
        Status = SurplusRatio > surplusApprovalThreshold ? PlanStatus.AwaitingApproval : PlanStatus.Approved;
    }

    /// <summary>تأیید مازاد بیش از آستانه. مجوز «تأیید مازاد» در لایه Application بررسی می‌شود.</summary>
    public void Approve(Guid userId, DateTime nowUtc)
    {
        if (Status != PlanStatus.AwaitingApproval)
            throw new DomainException("این زیربرنامه منتظر تأیید نیست.");

        Status = PlanStatus.Approved;
        ApprovedBy = userId;
        ApprovedAtUtc = nowUtc;
    }

    public void MarkReleased()
    {
        if (Status != PlanStatus.Approved) throw new DomainException("فقط زیربرنامه تأییدشده صادر می‌شود.");
        Status = PlanStatus.Released;
    }

    public void MarkCompleted() => Status = PlanStatus.Completed;

    private void EnsureEditable()
    {
        if (Status is not PlanStatus.Draft)
            throw new DomainException("زیربرنامه ثبت‌شده قابل ویرایش نیست.");
    }
}

public class PlanLine : Entity
{
    private PlanLine() { }
    internal PlanLine(Guid planId, SkuKey sku, Guid? salesOrderLineId, int orderQuantity, int surplusQuantity)
    {
        ProductionPlanId = planId;
        ShoeModelId = sku.ShoeModelId;
        ColorId = sku.ColorId;
        Size = sku.Size;
        SalesOrderLineId = salesOrderLineId;
        OrderQuantity = orderQuantity;
        SurplusQuantity = surplusQuantity;
    }

    public Guid ProductionPlanId { get; private set; }
    public Guid ShoeModelId { get; private set; }
    public Guid ColorId { get; private set; }
    public int Size { get; private set; }
    public Guid? SalesOrderLineId { get; private set; }

    /// <summary>سهم سفارش.</summary>
    public int OrderQuantity { get; private set; }

    /// <summary>مازاد برنامه‌ریزی‌شده (به موجودی آزاد می‌رود).</summary>
    public int SurplusQuantity { get; private set; }

    public SkuKey Sku => new(ShoeModelId, ColorId, Size);
}
