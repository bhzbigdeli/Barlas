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
    private readonly List<PlanStageTarget> _stageTargets = [];
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

    // ---- تاریخ‌های واقعی هر قدم ----
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    // ---- تاریخ‌های پیش‌بینی‌شده ----
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }

    /// <summary>تاریخ پیش‌بینی‌شده پایان هر مرحله برای کل زیربرنامه.</summary>
    public IReadOnlyList<PlanStageTarget> StageTargets => _stageTargets;

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
    public void Submit(decimal surplusApprovalThreshold, DateTime nowUtc)
    {
        EnsureEditable();
        if (_lines.Count == 0) throw new DomainException("زیربرنامه خالی است.");
        Status = SurplusRatio > surplusApprovalThreshold ? PlanStatus.AwaitingApproval : PlanStatus.Approved;
        SubmittedAtUtc = nowUtc;
    }

    /// <summary>
    /// زمان‌بندی پیش‌بینی‌شده: شروع، پایان و تاریخ پایان هر مرحله. بار اول مراحل ساخته می‌شوند؛
    /// بعد از آن فقط تاریخ مراحل موجود عوض می‌شود.
    /// </summary>
    public void SetSchedule(DateOnly start, DateOnly end, IReadOnlyList<(Guid StageId, DateOnly Date)> stageDates)
    {
        if (Status is PlanStatus.Completed or PlanStatus.Cancelled)
            throw new DomainException("زمان‌بندی زیربرنامه بسته‌شده قابل تغییر نیست.");
        if (end < start) throw new DomainException("تاریخ پایان پیش‌بینی‌شده نباید قبل از شروع باشد.");
        if (stageDates.Any(d => d.Date < start || d.Date > end))
            throw new DomainException("تاریخ هر مرحله باید بین شروع و پایان زیربرنامه باشد.");
        if (stageDates.GroupBy(d => d.StageId).Any(g => g.Count() > 1))
            throw new DomainException("یک مرحله دوبار در زمان‌بندی آمده است.");

        PlannedStartDate = start;
        PlannedEndDate = end;
        if (_stageTargets.Count == 0)
        {
            foreach (var (stageId, date) in stageDates) _stageTargets.Add(new PlanStageTarget(Id, stageId, date));
            return;
        }

        foreach (var (stageId, date) in stageDates)
        {
            var target = _stageTargets.FirstOrDefault(t => t.StageId == stageId)
                ?? throw new DomainException("این مرحله در مسیر زیربرنامه نیست.");
            target.Reschedule(date);
        }
    }

    /// <summary>پخش یکنواخت تاریخ پایان مراحل (به ترتیب مسیر) بین شروع و پایان.</summary>
    public static IReadOnlyList<(Guid StageId, DateOnly Date)> DistributeStageDates(DateOnly start, DateOnly end, IReadOnlyList<Guid> orderedStageIds)
    {
        var days = end.DayNumber - start.DayNumber;
        var n = orderedStageIds.Count;
        return orderedStageIds.Select((id, i) => (id, start.AddDays((int)Math.Round((double)days * (i + 1) / n)))).ToList();
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

    public void MarkReleased(DateTime nowUtc)
    {
        if (Status != PlanStatus.Approved) throw new DomainException("فقط زیربرنامه تأییدشده صادر می‌شود.");
        Status = PlanStatus.Released;
        ReleasedAtUtc = nowUtc;
    }

    public void MarkCompleted(DateTime nowUtc)
    {
        Status = PlanStatus.Completed;
        CompletedAtUtc = nowUtc;
    }

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

/// <summary>تاریخ پیش‌بینی‌شده پایان یک مرحله در یک زیربرنامه.</summary>
public class PlanStageTarget : Entity
{
    private PlanStageTarget() { }
    internal PlanStageTarget(Guid planId, Guid stageId, DateOnly plannedDate)
    {
        ProductionPlanId = planId;
        StageId = stageId;
        PlannedDate = plannedDate;
    }

    public Guid ProductionPlanId { get; private set; }
    public Guid StageId { get; private set; }
    public DateOnly PlannedDate { get; private set; }

    internal void Reschedule(DateOnly date) => PlannedDate = date;
}
