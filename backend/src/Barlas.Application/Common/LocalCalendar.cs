using Barlas.Domain.Catalog;

namespace Barlas.Application.Common;

/// <summary>
/// روز کاری کارخانه به وقت تهران. زمان‌ها UTC ذخیره می‌شوند؛ مقایسه با تاریخ‌های پیش‌بینی‌شده (DateOnly)
/// باید روی تاریخ محلی انجام شود.
/// </summary>
public static class LocalCalendar
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static DateOnly Date(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

    public static DateOnly? Date(DateTime? utc) => utc is { } u ? Date(u) : null;

    /// <summary>ابتدای روز محلی به UTC (برای فیلتر بازه).</summary>
    public static DateTime StartUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), Zone);

    /// <summary>
    /// دیرکرد به روز: اگر انجام شده، فاصله تاریخ واقعی تا پیش‌بینی (منفی یعنی زودتر)؛
    /// اگر انجام نشده و موعد گذشته، فاصله امروز تا موعد؛ وگرنه null.
    /// </summary>
    public static int? DelayDays(DateOnly? planned, DateOnly? actual, DateOnly today)
    {
        if (planned is not { } p) return null;
        if (actual is { } a) return a.DayNumber - p.DayNumber;
        return today > p ? today.DayNumber - p.DayNumber : null;
    }

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Asia/Tehran", "Iran Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        return TimeZoneInfo.CreateCustomTimeZone("Tehran", TimeSpan.FromMinutes(210), "Tehran", "Tehran");
    }
}

/// <summary>نرخ دستمزد کارمزدی معتبر در یک تاریخ: نرخ خاص مدل، وگرنه نرخ عمومی مرحله.</summary>
public sealed class WageTable(IEnumerable<PieceRate> rates)
{
    private readonly ILookup<(Guid? Model, Guid Stage), PieceRate> _rates =
        rates.OrderByDescending(r => r.EffectiveFrom).ToLookup(r => (r.ShoeModelId, r.StageId));

    public decimal Rate(Guid modelId, Guid stageId, DateOnly date) =>
        Pick(_rates[(modelId, stageId)], date) ?? Pick(_rates[(null, stageId)], date) ?? 0;

    /// <summary>نرخ معتبر در تاریخ؛ اگر نرخی قبل از آن تاریخ نیست، قدیمی‌ترین نرخ.</summary>
    private static decimal? Pick(IEnumerable<PieceRate> newestFirst, DateOnly date)
    {
        var list = newestFirst.ToList();
        return (list.FirstOrDefault(r => r.EffectiveFrom <= date) ?? list.LastOrDefault())?.RatePerPair;
    }
}
