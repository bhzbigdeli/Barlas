using Barlas.Domain.Common;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;

namespace Barlas.Domain.Tests;

public class ScheduleTests
{
    private static readonly Guid Unit = Guid.NewGuid(), Model = Guid.NewGuid(), Color = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 12, 1);

    private static ProductionPlan Plan()
    {
        var plan = new ProductionPlan(Unit, "P-1", null);
        plan.SetLine(new SkuKey(Model, Color, 40), null, 10, 0);
        return plan;
    }

    [Fact]
    public void Stage_dates_are_spread_evenly_and_end_on_plan_end()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), d = Guid.NewGuid();
        var dates = ProductionPlan.DistributeStageDates(Start, Start.AddDays(8), [a, b, c, d]);
        Assert.Equal([Start.AddDays(2), Start.AddDays(4), Start.AddDays(6), Start.AddDays(8)], dates.Select(x => x.Date));
    }

    [Fact]
    public void Rescheduling_updates_existing_stages_only()
    {
        var stage = Guid.NewGuid();
        var plan = Plan();
        plan.SetSchedule(Start, Start.AddDays(10), [(stage, Start.AddDays(10))]);
        plan.SetSchedule(Start, Start.AddDays(12), [(stage, Start.AddDays(11))]);

        Assert.Equal(Start.AddDays(11), plan.StageTargets.Single().PlannedDate);
        Assert.Throws<DomainException>(() => plan.SetSchedule(Start, Start.AddDays(12), [(Guid.NewGuid(), Start)]));
        Assert.Throws<DomainException>(() => plan.SetSchedule(Start, Start.AddDays(-1), []));
        Assert.Throws<DomainException>(() => plan.SetSchedule(Start, Start.AddDays(5), [(stage, Start.AddDays(6))]));
    }

    [Fact]
    public void Bundle_records_finish_time_and_rework_clears_it()
    {
        var route = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var bundle = new Bundle(Unit, Guid.NewGuid(), "B-1", Model, Color, new Dictionary<int, int> { [40] = 5 }, route);
        var w = Guid.NewGuid();
        var t1 = new DateTime(2026, 12, 2, 8, 0, 0, DateTimeKind.Utc);

        bundle.CompleteStage(route[0], w, w, t1);
        Assert.Null(bundle.FinishedAtUtc);
        bundle.CompleteStage(route[1], w, w, t1.AddHours(3));
        Assert.Equal(t1.AddHours(3), bundle.FinishedAtUtc);

        bundle.SendBackForRework(route[1], w, w, t1.AddHours(4), null);
        Assert.Null(bundle.FinishedAtUtc);
    }
}
