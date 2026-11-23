using Barlas.Application.Abstractions;
using Barlas.Domain.Catalog;
using Barlas.Domain.Identity;
using Barlas.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Barlas.Infrastructure.Persistence;

/// <summary>
/// داده‌های اولیه. اجرای آزمایشی اول در «واحد اصلی» است و بعد «سالن صندل»؛
/// کدهای تامین‌کننده موقت‌اند و باید با کدهای واقعی سیستم فروش تهران جایگزین شوند.
/// سطوح دسترسی پیش‌فرض فقط بار اول ساخته می‌شوند و بعد از آن در برنامه قابل تغییرند.
/// </summary>
public class DbSeeder(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DbSeeder> logger)
{
    public const string OwnerRoleName = "مالک";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Units.AnyAsync(ct))
        {
            db.Units.AddRange(
                new ProductionUnit("MAIN", "واحد اصلی", config["Seed:MainSupplierCode"] ?? "SUP-MAIN"),
                new ProductionUnit("SANDAL", "سالن صندل", config["Seed:SandalSupplierCode"] ?? "SUP-SANDAL"));
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

        await SeedCatalogAsync(ct);
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

    /// <summary>
    /// اطلاعات پایه واقعی (مراحل، مسیرها، زیره‌ها، محصولات و نرخ دستمزد هر مرحله) از فایل
    /// <c>Seed:CatalogFile</c> که از «آنالیز ۱۴۰۵» ساخته شده؛ فقط وقتی هنوز محصولی ثبت نشده.
    /// فایل در مخزن نیست (اطلاعات محرمانه کارخانه).
    /// </summary>
    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        if (await db.ShoeModels.AnyAsync(ct)) return;

        var path = config["Seed:CatalogFile"] ?? "SeedData/catalog.json";
        if (!Path.IsPathRooted(path)) path = Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(path))
        {
            logger.LogWarning("فایل اطلاعات پایه پیدا نشد ({Path}); محصولی ساخته نشد.", path);
            return;
        }

        var seed = JsonSerializer.Deserialize<CatalogSeed>(await File.ReadAllTextAsync(path, ct), JsonOptions)
            ?? throw new InvalidOperationException("فایل اطلاعات پایه خالی است.");

        var stages = (await db.Stages.ToListAsync(ct)).ToDictionary(s => s.Code);
        foreach (var s in seed.Stages.Where(s => !stages.ContainsKey(s.Code)))
        {
            stages[s.Code] = new ProductionStage(s.Code, s.Name);
            db.Stages.Add(stages[s.Code]);
        }

        var routes = (await db.Routes.Include(r => r.Steps).ToListAsync(ct)).ToDictionary(r => r.Name);
        foreach (var r in seed.Routes.Where(r => !routes.ContainsKey(r.Name)))
        {
            var route = new ProductionRoute(r.Name);
            foreach (var code in r.Stages) route.AddStep(stages[code].Id);
            routes[r.Name] = route;
            db.Routes.Add(route);
        }

        var soles = (await db.Soles.ToListAsync(ct)).ToDictionary(s => s.Code);
        foreach (var s in seed.Soles.Where(s => !soles.ContainsKey(s.Code)))
        {
            soles[s.Code] = new Sole(s.Code, s.Name);
            db.Soles.Add(soles[s.Code]);
        }

        foreach (var m in seed.Models)
        {
            // سری سایز در آنالیز نیست؛ محصول با سری خالی ثبت می‌شود تا از صفحه محصول تکمیل شود.
            var model = new ShoeModel(m.Code, m.Name, routes[m.Route].Id, m.Sole is null ? null : soles[m.Sole].Id,
                new Dictionary<int, int>(), m.Group);
            db.ShoeModels.Add(model);
            foreach (var st in m.Stages)
                db.PieceRates.Add(new PieceRate(stages[st.Stage].Id, model.Id, st.Rate, m.AnalysisDate));
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("اطلاعات پایه از {Source}: {Models} محصول، {Routes} مسیر.", seed.Source, seed.Models.Count, seed.Routes.Count);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record CatalogSeed(string Source, List<CodeName> Stages, List<CodeName> Soles, List<RouteSeed> Routes, List<ModelSeed> Models);
    private sealed record CodeName(string Code, string Name);
    private sealed record RouteSeed(string Name, List<string> Stages);
    private sealed record ModelSeed(string Code, string Name, string Group, DateOnly AnalysisDate, string? Sole, string Route, List<StageRate> Stages);
    private sealed record StageRate(string Stage, decimal Rate);
}
