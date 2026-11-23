using Barlas.Domain.Catalog;
using Barlas.Domain.Identity;
using Barlas.Domain.Inventory;
using Barlas.Domain.Orders;
using Barlas.Domain.Organization;
using Barlas.Domain.Planning;
using Barlas.Domain.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Barlas.Infrastructure.Persistence;

internal sealed class ProductionUnitConfig : IEntityTypeConfiguration<ProductionUnit>
{
    public void Configure(EntityTypeBuilder<ProductionUnit> b)
    {
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.SupplierCode).HasMaxLength(50);
        b.Property(x => x.SurplusApprovalThreshold).HasPrecision(5, 4);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.SupplierCode).IsUnique();
    }
}

internal sealed class WorkerConfig : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> b)
    {
        b.Property(x => x.PersonnelCode).HasMaxLength(20);
        b.Property(x => x.FullName).HasMaxLength(100);
        b.HasIndex(x => new { x.UnitId, x.PersonnelCode }).IsUnique();
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.UserName).HasMaxLength(50);
        b.Property(x => x.FullName).HasMaxLength(100);
        b.Property(x => x.PasswordHash).HasMaxLength(500);
        b.HasIndex(x => x.UserName).IsUnique();
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Name).IsUnique();
        b.Ignore(x => x.Permissions);
        b.PrimitiveCollection<List<Permission>>("_permissions").HasColumnName("Permissions");
    }
}

internal sealed class StageConfig : IEntityTypeConfiguration<ProductionStage>
{
    public void Configure(EntityTypeBuilder<ProductionStage> b)
    {
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class RouteConfig : IEntityTypeConfiguration<ProductionRoute>
{
    public void Configure(EntityTypeBuilder<ProductionRoute> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasMany(x => x.Steps).WithOne().HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Steps).HasField("_steps");
    }
}

internal sealed class RouteStepConfig : IEntityTypeConfiguration<RouteStep>
{
    public void Configure(EntityTypeBuilder<RouteStep> b)
    {
        b.HasIndex(x => new { x.RouteId, x.Sequence }).IsUnique();
        b.HasOne<ProductionStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ColorConfig : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> b)
    {
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class SoleConfig : IEntityTypeConfiguration<Sole>
{
    public void Configure(EntityTypeBuilder<Sole> b)
    {
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class ShoeModelConfig : IEntityTypeConfiguration<ShoeModel>
{
    public void Configure(EntityTypeBuilder<ShoeModel> b)
    {
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Group).HasMaxLength(60);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<ProductionRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Sole>().WithMany().HasForeignKey(x => x.SoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Series).WithOne().HasForeignKey(s => s.ShoeModelId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Series).HasField("_series");
    }
}

internal sealed class SeriesSizeConfig : IEntityTypeConfiguration<SeriesSize>
{
    public void Configure(EntityTypeBuilder<SeriesSize> b) =>
        b.HasIndex(x => new { x.ShoeModelId, x.Size }).IsUnique();
}

internal sealed class ProductImageConfig : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.Property(x => x.ContentType).HasMaxLength(30);
        b.HasIndex(x => x.ShoeModelId).IsUnique();
        b.HasOne<ShoeModel>().WithMany().HasForeignKey(x => x.ShoeModelId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PieceRateConfig : IEntityTypeConfiguration<PieceRate>
{
    public void Configure(EntityTypeBuilder<PieceRate> b)
    {
        b.Property(x => x.RatePerPair).HasPrecision(18, 0);
        b.HasOne<ProductionStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShoeModel>().WithMany().HasForeignKey(x => x.ShoeModelId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalesOrderConfig : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> b)
    {
        b.Property(x => x.ExternalNumber).HasMaxLength(50);
        b.Property(x => x.CustomerName).HasMaxLength(200);
        b.HasIndex(x => new { x.UnitId, x.ExternalNumber }).IsUnique();
        b.HasIndex(x => new { x.UnitId, x.Status });
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SalesOrderLineConfig : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> b)
    {
        b.HasMany(x => x.Sizes).WithOne().HasForeignKey(s => s.SalesOrderLineId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ShoeModel>().WithMany().HasForeignKey(x => x.ShoeModelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Color>().WithMany().HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalesOrderLineSizeConfig : IEntityTypeConfiguration<SalesOrderLineSize>
{
    public void Configure(EntityTypeBuilder<SalesOrderLineSize> b) =>
        b.HasIndex(x => new { x.SalesOrderLineId, x.Size }).IsUnique();
}

internal sealed class ShipmentConfig : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.Property(x => x.Number).HasMaxLength(60);
        b.HasIndex(x => new { x.UnitId, x.Number }).IsUnique();
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ShipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ShipmentLineConfig : IEntityTypeConfiguration<ShipmentLine>
{
    public void Configure(EntityTypeBuilder<ShipmentLine> b) =>
        b.HasOne<SalesOrderLine>().WithMany().HasForeignKey(x => x.SalesOrderLineId).OnDelete(DeleteBehavior.Restrict);
}

internal sealed class FreeStockConfig : IEntityTypeConfiguration<FreeStockItem>
{
    public void Configure(EntityTypeBuilder<FreeStockItem> b)
    {
        b.HasIndex(x => new { x.UnitId, x.ShoeModelId, x.ColorId, x.Size }).IsUnique();
        // جلوگیری از رزرو هم‌زمان یک موجودی برای دو سفارش.
        b.Property<byte[]>("RowVersion").IsRowVersion();
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShoeModel>().WithMany().HasForeignKey(x => x.ShoeModelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Color>().WithMany().HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfig : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.HasIndex(x => new { x.UnitId, x.ShoeModelId, x.ColorId, x.Size });
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanConfig : IEntityTypeConfiguration<ProductionPlan>
{
    public void Configure(EntityTypeBuilder<ProductionPlan> b)
    {
        b.Property(x => x.Number).HasMaxLength(30);
        b.HasIndex(x => new { x.UnitId, x.Number }).IsUnique();
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ProductionPlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.StageTargets).WithOne().HasForeignKey(t => t.ProductionPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PlanStageTargetConfig : IEntityTypeConfiguration<PlanStageTarget>
{
    public void Configure(EntityTypeBuilder<PlanStageTarget> b)
    {
        b.HasIndex(x => new { x.ProductionPlanId, x.StageId }).IsUnique();
        b.HasOne<ProductionStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanLineConfig : IEntityTypeConfiguration<PlanLine>
{
    public void Configure(EntityTypeBuilder<PlanLine> b) =>
        b.HasIndex(x => new { x.ProductionPlanId, x.ShoeModelId, x.ColorId, x.Size }).IsUnique();
}

internal sealed class BundleConfig : IEntityTypeConfiguration<Bundle>
{
    public void Configure(EntityTypeBuilder<Bundle> b)
    {
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.QrToken).HasMaxLength(64);
        b.HasIndex(x => x.QrToken).IsUnique();
        b.HasIndex(x => new { x.UnitId, x.Number }).IsUnique();
        b.Ignore(x => x.RouteStageIds);
        b.PrimitiveCollection<List<Guid>>("_route").HasColumnName("RouteStageIds");
        // دو اسکن هم‌زمان روی یک دسته نباید هر دو ثبت شوند.
        b.Property<byte[]>("RowVersion").IsRowVersion();
        b.HasOne<ProductionUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProductionPlan>().WithMany().HasForeignKey(x => x.ProductionPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Sizes).WithOne().HasForeignKey(s => s.BundleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Scans).WithOne().HasForeignKey(s => s.BundleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StageScanConfig : IEntityTypeConfiguration<StageScan>
{
    public void Configure(EntityTypeBuilder<StageScan> b)
    {
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => x.ScannedAtUtc);
        b.HasIndex(x => new { x.WorkerId, x.ScannedAtUtc });
        b.HasOne<Worker>().WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProductionStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
    }
}
