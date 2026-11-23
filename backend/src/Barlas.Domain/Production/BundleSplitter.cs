using Barlas.Domain.Common;
using Barlas.Domain.Planning;

namespace Barlas.Domain.Production;

/// <summary>
/// صدور دسته‌ها از زیربرنامه: برای هر مدل+رنگ، جفت‌ها (سهم سفارش + مازاد) به ترتیب سایز
/// در دسته‌هایی با حداکثر <c>maxPairsPerBundle</c> جفت تقسیم می‌شوند.
/// </summary>
public static class BundleSplitter
{
    public static IReadOnlyList<Bundle> Split(ProductionPlan plan, int maxPairsPerBundle,
        Func<Guid, IReadOnlyList<Guid>> routeStagesForModel)
    {
        if (maxPairsPerBundle <= 0) throw new DomainException("تعداد جفت هر دسته باید مثبت باشد.");

        plan.MarkReleased();
        var bundles = new List<Bundle>();
        var groups = plan.Lines
            .GroupBy(l => (l.ShoeModelId, l.ColorId))
            .OrderBy(g => g.Key.ShoeModelId).ThenBy(g => g.Key.ColorId);

        foreach (var group in groups)
        {
            var route = routeStagesForModel(group.Key.ShoeModelId);
            var current = new Dictionary<int, int>();
            var count = 0;

            void Flush()
            {
                if (count == 0) return;
                var number = $"{plan.Number}-{bundles.Count + 1:00}";
                bundles.Add(new Bundle(plan.UnitId, plan.Id, number, group.Key.ShoeModelId, group.Key.ColorId, current, route));
                current = new Dictionary<int, int>();
                count = 0;
            }

            foreach (var line in group.OrderBy(l => l.Size))
            {
                var remaining = line.OrderQuantity + line.SurplusQuantity;
                while (remaining > 0)
                {
                    var take = Math.Min(remaining, maxPairsPerBundle - count);
                    current[line.Size] = current.GetValueOrDefault(line.Size) + take;
                    count += take;
                    remaining -= take;
                    if (count == maxPairsPerBundle) Flush();
                }
            }
            Flush();
        }

        return bundles;
    }
}
