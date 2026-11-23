using System.Linq.Expressions;
using Barlas.Application.Abstractions;
using Barlas.Domain.Catalog;
using Barlas.Domain.Common;
using Barlas.Domain.Identity;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;
using Barlas.Domain.Organization;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, IClock clock)
    : DbContext(options), IAppDbContext
{
    public DbSet<ProductionUnit> Units => Set<ProductionUnit>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ProductionStage> Stages => Set<ProductionStage>();
    public DbSet<ProductionRoute> Routes => Set<ProductionRoute>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Sole> Soles => Set<Sole>();
    public DbSet<ShoeModel> ShoeModels => Set<ShoeModel>();
    public DbSet<PieceRate> PieceRates => Set<PieceRate>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<FreeStockItem> FreeStock => Set<FreeStockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProductionPlan> Plans => Set<ProductionPlan>();
    public DbSet<Bundle> Bundles => Set<Bundle>();
    public DbSet<StageScan> StageScans => Set<StageScan>();

    // این دو عضو در فیلتر سراسری استفاده می‌شوند؛ EF مقدارشان را در هر درخواست دوباره می‌خواند.
    private bool SeesAllUnits => currentUser.SeesAllUnits;
    private Guid? CurrentUnitId => currentUser.UnitId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // موجودیت‌ها مجموعه‌هایشان را پشت فیلد خصوصی نگه می‌دارند.
        modelBuilder.UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;

            // جدول موجودیت‌های فرزند (بدون DbSet) هم جمع نام‌گذاری شود: SalesOrderLine → SalesOrderLines
            if (entityType.GetTableName() == clr.Name)
                entityType.SetTableName(clr.Name + "s");

            // شناسه در دامنه ساخته می‌شود؛ EF نباید آن را تولید کند (موجودیت جدید در گراف = Added).
            if (typeof(Entity).IsAssignableFrom(clr))
                modelBuilder.Entity(clr).Property(nameof(Entity.Id)).ValueGeneratedNever();

            if (typeof(IUnitScoped).IsAssignableFrom(clr))
                modelBuilder.Entity(clr).HasQueryFilter(BuildUnitFilter(clr));
        }
    }

    /// <summary>e => SeesAllUnits || e.UnitId == CurrentUnitId</summary>
    private LambdaExpression BuildUnitFilter(Type clr)
    {
        var e = Expression.Parameter(clr, "e");
        var ctx = Expression.Constant(this);
        var all = Expression.Property(ctx, nameof(SeesAllUnits));
        var unitId = Expression.Convert(Expression.Property(e, nameof(IUnitScoped.UnitId)), typeof(Guid?));
        var current = Expression.Property(ctx, nameof(CurrentUnitId));
        return Expression.Lambda(Expression.OrElse(all, Expression.Equal(unitId, current)), e);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = currentUser.UserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = currentUser.UserId;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
