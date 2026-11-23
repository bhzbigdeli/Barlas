namespace Barlas.Domain.Common;

/// <summary>کلید یک کالای قابل‌شمارش: مدل + رنگ + سایز.</summary>
public readonly record struct SkuKey(Guid ShoeModelId, Guid ColorId, int Size);
