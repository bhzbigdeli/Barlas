using Barlas.Domain.Catalog;
using Barlas.Domain.Common;
using Barlas.Domain.Identity;

namespace Barlas.Domain.Tests;

public class IdentityTests
{
    private static readonly Role Owner = new("مالک", [], isSystem: true);
    private static readonly Role Planner = new("برنامه‌ریز", [Permission.ImportOrders, Permission.ManagePlans]);

    [Fact]
    public void Owner_role_has_every_permission_and_cannot_be_changed()
    {
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(Owner.Has(p)));
        Assert.Throws<DomainException>(() => Owner.SetPermissions([Permission.ScanStages]));
    }

    [Fact]
    public void Custom_role_has_only_its_permissions()
    {
        Assert.True(Planner.Has(Permission.ImportOrders));
        Assert.False(Planner.Has(Permission.ManageUsers));
        Assert.False(Planner.SeesAllUnits);
    }

    [Fact]
    public void Unit_bound_role_requires_a_unit_and_all_units_role_drops_it()
    {
        Assert.Throws<DomainException>(() => new User("ali", "علی", Planner, null));

        var owner = new User("Admin ", "مدیر", Owner, Guid.NewGuid());
        Assert.Null(owner.UnitId);
        Assert.Equal("admin", owner.UserName);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Weak_passwords_are_rejected(string password) =>
        Assert.Throws<DomainException>(() => User.ValidatePassword(password));
}

public class ProductSeriesTests
{
    private static ShoeModel Product(Dictionary<int, int> series) => new("m-1", "محصول", Guid.NewGuid(), null, series);

    [Fact]
    public void Each_product_has_its_own_size_range_and_counts()
    {
        var men = Product(new() { [40] = 1, [41] = 2, [42] = 2, [43] = 1, [44] = 1, [45] = 1 });
        var women = Product(new() { [36] = 1, [37] = 2, [38] = 2, [39] = 1, [40] = 1 });

        Assert.Equal((40, 45, 8), (men.MinSeriesSize, men.MaxSeriesSize, men.PairsPerSeries));
        Assert.Equal((36, 40, 7), (women.MinSeriesSize, women.MaxSeriesSize, women.PairsPerSeries));
        Assert.Equal("M-1", men.Code);
    }

    [Fact]
    public void Series_count_expands_by_the_products_own_series()
    {
        var p = Product(new() { [37] = 1, [38] = 2, [42] = 1 });
        Assert.Equal(new Dictionary<int, int> { [37] = 5, [38] = 10, [42] = 5 }, p.Expand(5));
    }

    [Fact]
    public void Editing_series_adds_updates_and_removes_sizes()
    {
        var p = Product(new() { [40] = 1, [41] = 1 });
        p.SetSeries(new Dictionary<int, int> { [41] = 3, [42] = 2, [40] = 0 });
        Assert.Equal([(41, 3), (42, 2)], p.Series.Select(s => (s.Size, s.Quantity)));
    }

    [Theory]
    [InlineData(0, 1)]   // سایز خارج از بازه
    [InlineData(40, -1)] // تعداد منفی
    public void Invalid_series_is_rejected(int size, int qty) =>
        Assert.Throws<DomainException>(() => Product(new() { [size] = qty }));

    [Fact]
    public void Empty_series_means_not_defined_yet()
    {
        var model = Product(new() { [40] = 0 });
        Assert.Empty(model.Series);
        Assert.Throws<DomainException>(() => model.Expand(1));
    }
}
