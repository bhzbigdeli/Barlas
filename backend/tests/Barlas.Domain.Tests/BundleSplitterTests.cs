using Barlas.Domain.Common;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;

namespace Barlas.Domain.Tests;

public class BundleSplitterTests
{
    private static readonly Guid Unit = Guid.NewGuid(), Model = Guid.NewGuid(), Color = Guid.NewGuid();
    private static readonly IReadOnlyList<Guid> Route = [Guid.NewGuid(), Guid.NewGuid()];
    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Splits_pairs_in_size_order_into_bundles_of_max_size()
    {
        var plan = new ProductionPlan(Unit, "MAIN-0001", null);
        plan.SetLine(new SkuKey(Model, Color, 40), null, 8, 0);
        plan.SetLine(new SkuKey(Model, Color, 41), null, 10, 2);
        plan.Submit(0.20m, Now);

        var bundles = BundleSplitter.Split(plan, 10, _ => Route, Now);

        Assert.Equal(PlanStatus.Released, plan.Status);
        Assert.Equal(Now, plan.ReleasedAtUtc);
        Assert.Equal(Now, plan.SubmittedAtUtc);
        Assert.Equal([10, 10], bundles.Select(b => b.TotalPairs));
        Assert.Equal("MAIN-0001-01", bundles[0].Number);
        Assert.Equal(8, bundles[0].Sizes.Single(s => s.Size == 40).Quantity);
        Assert.Equal(2, bundles[0].Sizes.Single(s => s.Size == 41).Quantity);
        Assert.Equal(10, bundles[1].Sizes.Single(s => s.Size == 41).Quantity);
    }

    [Fact]
    public void Unapproved_plan_cannot_be_released()
    {
        var plan = new ProductionPlan(Unit, "P", null);
        plan.SetLine(new SkuKey(Model, Color, 40), null, 10, 10);
        plan.Submit(0.20m, Now);
        Assert.Throws<DomainException>(() => BundleSplitter.Split(plan, 10, _ => Route, Now));
    }
}
