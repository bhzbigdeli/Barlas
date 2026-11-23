using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Orders;

public sealed record OrderSummary(
    Guid Id, Guid UnitId, string UnitName, string ExternalNumber, DateOnly OrderDate, DateOnly? DueDate,
    string? CustomerName, SalesOrderStatus Status, int TotalPairs, int FulfilledPairs, int ShippedPairs);

public sealed record OrderLineDto(Guid Id, int LineNumber, string ModelCode, string ModelName, string ColorName,
    IReadOnlyList<OrderLineSizeDto> Sizes);

public sealed record OrderLineSizeDto(int Size, int Quantity, int FromStock, int FromProduction, int Outstanding,
    int Shipped, int ReadyToShip);

public sealed record OrderShipmentDto(string Number, DateTime ShippedAtUtc, int Pairs);

public sealed record OrderDetail(OrderSummary Summary, IReadOnlyList<OrderLineDto> Lines, IReadOnlyList<OrderShipmentDto> Shipments);

/// <summary>خواندن سفارش‌ها؛ فیلتر واحد به‌صورت سراسری در DbContext اعمال می‌شود.</summary>
public class OrderQueries(IAppDbContext db)
{
    public async Task<IReadOnlyList<OrderSummary>> ListAsync(SalesOrderStatus? status, CancellationToken ct)
    {
        var query = db.SalesOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Sizes).AsQueryable();
        if (status is not null) query = query.Where(o => o.Status == status);

        var orders = await query.OrderByDescending(o => o.OrderDate).Take(500).ToListAsync(ct);
        var unitNames = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return orders.Select(o => ToSummary(o, unitNames)).ToList();
    }

    public async Task<OrderDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await db.SalesOrders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Sizes)
            .FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw AppException.NotFound("سفارش");

        var unitNames = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var models = await db.ShoeModels.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
        var colors = await db.Colors.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var lines = order.Lines.OrderBy(l => l.LineNumber).Select(l => new OrderLineDto(
            l.Id, l.LineNumber, models[l.ShoeModelId].Code, models[l.ShoeModelId].Name, colors[l.ColorId],
            l.Sizes.OrderBy(s => s.Size)
                .Select(s => new OrderLineSizeDto(s.Size, s.Quantity, s.FromStock, s.FromProduction, s.Outstanding, s.Shipped, s.ReadyToShip))
                .ToList())).ToList();

        var shipments = (await db.Shipments.AsNoTracking().Include(x => x.Lines).Where(x => x.SalesOrderId == id)
                .OrderBy(x => x.ShippedAtUtc).ToListAsync(ct))
            .Select(x => new OrderShipmentDto(x.Number, x.ShippedAtUtc, x.TotalPairs)).ToList();

        return new OrderDetail(ToSummary(order, unitNames), lines, shipments);
    }

    private static OrderSummary ToSummary(SalesOrder o, IReadOnlyDictionary<Guid, string> unitNames) => new(
        o.Id, o.UnitId, unitNames.GetValueOrDefault(o.UnitId, "?"), o.ExternalNumber, o.OrderDate, o.DueDate,
        o.CustomerName, o.Status, o.TotalPairs, o.FulfilledPairs, o.ShippedPairs);
}
