using System.Security.Cryptography;
using Barlas.Domain.Common;

namespace Barlas.Domain.Production;

public enum BundleStatus
{
    /// <summary>صادر شده، کاردکس چاپ شده، هنوز مرحله‌ای ثبت نشده.</summary>
    Issued = 1,
    InProgress = 2,
    /// <summary>همه مراحل مسیر تمام شده؛ آماده ورود به انبار.</summary>
    Finished = 3,
    InWarehouse = 4,
}

/// <summary>
/// دسته کار: بخشی از زیربرنامه برای یک مدل و رنگ، با جدول سایز و کاردکس QR.
/// هر مرحله سالن با اسکن کد QR با گوشی ثبت می‌شود.
/// </summary>
public class Bundle : AuditableEntity, IUnitScoped
{
    private readonly List<BundleSize> _sizes = [];
    private readonly List<StageScan> _scans = [];
    private readonly List<Guid> _route = [];
    private Bundle() { }

    public Bundle(Guid unitId, Guid planId, string number, Guid shoeModelId, Guid colorId,
        IReadOnlyDictionary<int, int> quantitiesBySize, IReadOnlyList<Guid> routeStageIds)
    {
        if (quantitiesBySize.Values.Sum() <= 0) throw new DomainException("دسته خالی است.");
        if (routeStageIds.Count == 0) throw new DomainException("مسیر مراحل مدل تعریف نشده است.");

        UnitId = unitId;
        ProductionPlanId = planId;
        Number = number;
        ShoeModelId = shoeModelId;
        ColorId = colorId;
        QrToken = NewToken();
        _route.AddRange(routeStageIds);
        foreach (var (size, qty) in quantitiesBySize.Where(kv => kv.Value > 0))
            _sizes.Add(new BundleSize(Id, size, qty));
    }

    public Guid UnitId { get; private set; }
    public Guid ProductionPlanId { get; private set; }
    public string Number { get; private set; } = default!;
    public Guid ShoeModelId { get; private set; }
    public Guid ColorId { get; private set; }

    /// <summary>توکن تصادفی چاپ‌شده در QR کاردکس (حدس‌زدنی نیست).</summary>
    public string QrToken { get; private set; } = default!;
    public BundleStatus Status { get; private set; } = BundleStatus.Issued;

    /// <summary>کپی مسیر مراحل در لحظه صدور، تا تغییر بعدی مسیر روی دسته‌های در جریان اثر نگذارد.</summary>
    public IReadOnlyList<Guid> RouteStageIds => _route;
    public IReadOnlyList<BundleSize> Sizes => _sizes;
    public IReadOnlyList<StageScan> Scans => _scans;
    public int TotalPairs => _sizes.Sum(s => s.Quantity);

    /// <summary>آخرین مرحله تمام‌شده (null یعنی هیچ).</summary>
    public Guid? LastCompletedStageId { get; private set; }

    /// <summary>زمان پایان آخرین مرحله (با دوباره‌کاری پاک می‌شود).</summary>
    public DateTime? FinishedAtUtc { get; private set; }
    public DateTime? InWarehouseAtUtc { get; private set; }

    public Guid? NextStageId
    {
        get
        {
            if (LastCompletedStageId is null) return _route[0];
            var i = _route.IndexOf(LastCompletedStageId.Value);
            return i + 1 < _route.Count ? _route[i + 1] : null;
        }
    }

    /// <summary>ثبت پایان یک مرحله توسط کارگر. مراحل باید به ترتیب مسیر باشند.</summary>
    public StageScan CompleteStage(Guid stageId, Guid workerId, Guid scannedBy, DateTime nowUtc)
    {
        if (Status is BundleStatus.Finished or BundleStatus.InWarehouse)
            throw new DomainException("همه مراحل این دسته تمام شده است.");
        if (NextStageId != stageId)
            throw new DomainException("این مرحله، مرحله بعدی این دسته نیست.");

        var scan = new StageScan(Id, stageId, workerId, scannedBy, TotalPairs, ScanKind.Completed, nowUtc);
        _scans.Add(scan);
        LastCompletedStageId = stageId;
        Status = NextStageId is null ? BundleStatus.Finished : BundleStatus.InProgress;
        if (Status == BundleStatus.Finished) FinishedAtUtc = nowUtc;
        return scan;
    }

    /// <summary>دوباره‌کاری: دسته به یک مرحله قبلی برمی‌گردد و از آنجا دوباره ادامه می‌دهد.</summary>
    public StageScan SendBackForRework(Guid toStageId, Guid workerId, Guid scannedBy, DateTime nowUtc, string? note)
    {
        if (Status == BundleStatus.InWarehouse) throw new DomainException("دسته وارد انبار شده است.");
        var target = _route.IndexOf(toStageId);
        var current = LastCompletedStageId is null ? -1 : _route.IndexOf(LastCompletedStageId.Value);
        if (target < 0 || target > current)
            throw new DomainException("دوباره‌کاری فقط به مراحلی که انجام شده‌اند ممکن است.");

        var scan = new StageScan(Id, toStageId, workerId, scannedBy, TotalPairs, ScanKind.Rework, nowUtc) { Note = note };
        _scans.Add(scan);
        LastCompletedStageId = target == 0 ? null : _route[target - 1];
        Status = BundleStatus.InProgress;
        FinishedAtUtc = null;
        return scan;
    }

    public void MarkInWarehouse(DateTime nowUtc)
    {
        if (Status != BundleStatus.Finished) throw new DomainException("دسته هنوز همه مراحل را طی نکرده است.");
        Status = BundleStatus.InWarehouse;
        InWarehouseAtUtc = nowUtc;
    }

    private static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
}

public class BundleSize : Entity
{
    private BundleSize() { }
    internal BundleSize(Guid bundleId, int size, int quantity)
    {
        BundleId = bundleId;
        Size = size;
        Quantity = quantity;
    }

    public Guid BundleId { get; private set; }
    public int Size { get; private set; }
    public int Quantity { get; private set; }
}

public enum ScanKind
{
    Completed = 1,
    Rework = 2,
}

/// <summary>ثبت یک مرحله برای یک دسته توسط یک کارگر — جایگزین گزارش روزانه کاغذی سالن.</summary>
public class StageScan : Entity
{
    private StageScan() { }
    internal StageScan(Guid bundleId, Guid stageId, Guid workerId, Guid scannedBy, int pairs, ScanKind kind, DateTime scannedAtUtc)
    {
        BundleId = bundleId;
        StageId = stageId;
        WorkerId = workerId;
        ScannedBy = scannedBy;
        Pairs = pairs;
        Kind = kind;
        ScannedAtUtc = scannedAtUtc;
    }

    public Guid BundleId { get; private set; }
    public Guid StageId { get; private set; }
    public Guid WorkerId { get; private set; }
    /// <summary>کاربری که اسکن کرد (ممکن است سرپرست به جای کارگر اسکن کند).</summary>
    public Guid ScannedBy { get; private set; }
    public int Pairs { get; private set; }
    public ScanKind Kind { get; private set; }
    public DateTime ScannedAtUtc { get; private set; }
    public string? Note { get; internal set; }
}
