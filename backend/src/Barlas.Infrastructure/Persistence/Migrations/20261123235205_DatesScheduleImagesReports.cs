using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Barlas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DatesScheduleImagesReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ShoeModels",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InProductionAtUtc",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedAtUtc",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyAtUtc",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAtUtc",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Plans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedEndDate",
                table: "Plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedStartDate",
                table: "Plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAtUtc",
                table: "Plans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "Plans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAtUtc",
                table: "Bundles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InWarehouseAtUtc",
                table: "Bundles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlanStageTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanStageTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanStageTargets_Plans_ProductionPlanId",
                        column: x => x.ProductionPlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanStageTargets_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShoeModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_ShoeModels_ShoeModelId",
                        column: x => x.ShoeModelId,
                        principalTable: "ShoeModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanStageTargets_ProductionPlanId_StageId",
                table: "PlanStageTargets",
                columns: new[] { "ProductionPlanId", "StageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanStageTargets_StageId",
                table: "PlanStageTargets",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ShoeModelId",
                table: "ProductImages",
                column: "ShoeModelId",
                unique: true);

            // پر کردن تقریبی تاریخ‌ها برای داده‌های قبل از این نسخه.
            migrationBuilder.Sql("""
                UPDATE Plans SET SubmittedAtUtc = CreatedAtUtc WHERE SubmittedAtUtc IS NULL;
                UPDATE p SET ReleasedAtUtc = (SELECT MIN(b.CreatedAtUtc) FROM Bundles b WHERE b.ProductionPlanId = p.Id)
                    FROM Plans p WHERE p.Status IN (4, 5) AND p.ReleasedAtUtc IS NULL;
                UPDATE b SET FinishedAtUtc = (SELECT MAX(s.ScannedAtUtc) FROM StageScans s WHERE s.BundleId = b.Id)
                    FROM Bundles b WHERE b.Status IN (3, 4);
                UPDATE Bundles SET InWarehouseAtUtc = COALESCE(UpdatedAtUtc, FinishedAtUtc) WHERE Status = 4;
                UPDATE p SET CompletedAtUtc = (SELECT MAX(b.InWarehouseAtUtc) FROM Bundles b WHERE b.ProductionPlanId = p.Id)
                    FROM Plans p WHERE p.Status = 5;
                UPDATE o SET PlannedAtUtc = (SELECT MIN(p.CreatedAtUtc) FROM Plans p WHERE p.SalesOrderId = o.Id)
                    FROM SalesOrders o WHERE o.Status NOT IN (1, 9);
                UPDATE o SET InProductionAtUtc = (SELECT MIN(p.ReleasedAtUtc) FROM Plans p WHERE p.SalesOrderId = o.Id)
                    FROM SalesOrders o WHERE o.Status NOT IN (1, 2, 9);
                UPDATE o SET ShippedAtUtc = (SELECT MAX(s.ShippedAtUtc) FROM Shipments s WHERE s.SalesOrderId = o.Id)
                    FROM SalesOrders o WHERE o.Status = 5;
                UPDATE SalesOrders SET ReadyAtUtc = COALESCE(UpdatedAtUtc, CreatedAtUtc) WHERE Status IN (4, 5) AND ReadyAtUtc IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanStageTargets");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ShoeModels");

            migrationBuilder.DropColumn(
                name: "InProductionAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "PlannedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ReadyAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ShippedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "PlannedEndDate",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "ReleasedAtUtc",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "FinishedAtUtc",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "InWarehouseAtUtc",
                table: "Bundles");
        }
    }
}
