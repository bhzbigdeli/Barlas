using Barlas.Application.Abstractions;
using Barlas.Domain.Catalog;
using Barlas.Domain.Identity;
using Barlas.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Barlas.Infrastructure.Persistence;

/// <summary>
/// داده‌های اولیه. اجرای آزمایشی اول در «واحد اصلی» است و بعد «سالن صندل»؛
/// کدهای تامین‌کننده موقت‌اند و باید با کدهای واقعی سیستم فروش تهران جایگزین شوند.
/// سطوح دسترسی پیش‌فرض فقط بار اول ساخته می‌شوند و بعد از آن در برنامه قابل تغییرند.
/// </summary>
public class DbSeeder(AppDbContext db, IPasswordHasher hasher, IConfiguration config)
{
    public const string OwnerRoleName = "مالک";

    public async Task SeedAsync(bool includeSampleData, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Units.AnyAsync(ct))
        {
            db.Units.AddRange(
                new ProductionUnit("MAIN", "واحد اصلی", config["Seed:MainSupplierCode"] ?? "SUP-MAIN"),
                new ProductionUnit("SANDAL", "سالن صندل", config["Seed:SandalSupplierCode"] ?? "SUP-SANDAL"));
        }

        if (!await db.Stages.AnyAsync(ct))
        {
            var stages = new[]
            {
                new ProductionStage("CUT", "برش"),
                new ProductionStage("LASER", "لیزر"),
                new ProductionStage("UPPER", "پستاسازی"),
                new ProductionStage("LAST", "کفاشی"),
                new ProductionStage("PACK", "بسته‌بندی"),
            };
            db.Stages.AddRange(stages);

            var route = new ProductionRoute("مسیر استاندارد");
            foreach (var s in stages) route.AddStep(s.Id);
            db.Routes.Add(route);
        }

        if (!await db.Roles.AnyAsync(ct)) db.Roles.AddRange(DefaultRoles());

        await db.SaveChangesAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            var password = config["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Seed:AdminPassword تنظیم نشده است (برای ساخت کاربر مالک).");

            var owner = await db.Roles.FirstAsync(r => r.IsSystem, ct);
            var admin = new User("admin", "مدیر سیستم", owner, null);
            admin.SetPasswordHash(hasher.Hash(admin, password));
            db.Users.Add(admin);
            await db.SaveChangesAsync(ct);
        }

        if (includeSampleData) await SeedSamplesAsync(ct);
    }

    private static IEnumerable<Role> DefaultRoles()
    {
        yield return new Role(OwnerRoleName, [], isSystem: true);
        yield return new Role("مدیر کارخانه", Enum.GetValues<Permission>());
        yield return new Role("مدیر واحد",
            [Permission.ImportOrders, Permission.ManagePlans, Permission.ApproveSurplus, Permission.ScanStages,
             Permission.ManageWarehouse, Permission.ViewReports]);
        yield return new Role("مدیر فنی", [Permission.ManagePlans, Permission.ManageCatalog, Permission.ViewReports]);
        yield return new Role("برنامه‌ریز", [Permission.ImportOrders, Permission.ManagePlans, Permission.ViewReports]);
        yield return new Role("سرپرست سالن", [Permission.ScanStages, Permission.ViewReports]);
        yield return new Role("انباردار", [Permission.ManageWarehouse]);
        yield return new Role("اپراتور اسکن", [Permission.ScanStages]);
    }

    /// <summary>نمونه برای توسعه: سه محصول با سری‌های متفاوت، رنگ، زیره و دو کارگر در واحد اصلی.</summary>
    private async Task SeedSamplesAsync(CancellationToken ct)
    {
        if (await db.ShoeModels.AnyAsync(ct)) return;

        var main = await db.Units.IgnoreQueryFilters().FirstAsync(u => u.Code == "MAIN", ct);
        var route = await db.Routes.FirstAsync(ct);

        db.Colors.AddRange(new Color("BLK", "مشکی"), new Color("BRN", "قهوه‌ای"), new Color("WHT", "سفید"));
        var sole = new Sole("PU-1", "زیره پی‌یو ۱");
        db.Soles.Add(sole);

        // سری‌ها عمداً متفاوت‌اند: بازه سایز و تعداد هر سایز محصول به محصول فرق دارد.
        db.ShoeModels.AddRange(
            new ShoeModel("M-100", "مردانه کلاسیک", route.Id, sole.Id,
                new Dictionary<int, int> { [40] = 1, [41] = 2, [42] = 2, [43] = 2, [44] = 1, [45] = 1 }),
            new ShoeModel("W-200", "زنانه روزمره", route.Id, null,
                new Dictionary<int, int> { [36] = 1, [37] = 2, [38] = 2, [39] = 2, [40] = 1 }),
            new ShoeModel("S-300", "صندل اسپرت", route.Id, null,
                new Dictionary<int, int> { [37] = 1, [38] = 1, [39] = 2, [40] = 2, [41] = 2, [42] = 1 }));

        db.Workers.AddRange(new Worker(main.Id, "1001", "کارگر نمونه ۱"), new Worker(main.Id, "1002", "کارگر نمونه ۲"));
        await db.SaveChangesAsync(ct);
    }
}
