using Barlas.Domain.Catalog;
using Barlas.Domain.Identity;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;
using Barlas.Domain.Organization;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Abstractions;

/// <summary>
/// دسترسی لایه Application به داده. پیاده‌سازی در Infrastructure است و
/// فیلتر واحد (هر کاربر فقط واحد خودش) را به‌صورت سراسری اعمال می‌کند.
/// </summary>
public interface IAppDbContext
{
    DbSet<ProductionUnit> Units { get; }
    DbSet<Worker> Workers { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }

    DbSet<ProductionStage> Stages { get; }
    DbSet<ProductionRoute> Routes { get; }
    DbSet<Color> Colors { get; }
    DbSet<Sole> Soles { get; }
    DbSet<ShoeModel> ShoeModels { get; }
    DbSet<PieceRate> PieceRates { get; }

    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<FreeStockItem> FreeStock { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<ProductionPlan> Plans { get; }
    DbSet<Bundle> Bundles { get; }
    DbSet<StageScan> StageScans { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>کاربر جاری درخواست. مجوزها در هر درخواست از دیتابیس خوانده می‌شوند (تغییر سطح دسترسی فوراً اثر دارد).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? UnitId { get; }
    bool IsAuthenticated { get; }

    /// <summary>دارای مجوز «دیدن همه واحدها»، یا فرایند سیستمی (مثل seed).</summary>
    bool SeesAllUnits { get; }

    IReadOnlySet<Permission> Permissions { get; }
    bool Has(Permission permission);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}
