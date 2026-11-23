using Barlas.Domain.Common;

namespace Barlas.Domain.Orders;

public sealed record ShipmentQuantity(Guid SalesOrderLineId, int Size, int Quantity);

/// <summary>یک ارسال به تهران (کامل یا جزئی) برای یک سفارش.</summary>
public class Shipment : AuditableEntity, IUnitScoped
{
    private readonly List<ShipmentLine> _lines = [];
    private Shipment() { }

    internal Shipment(Guid unitId, Guid salesOrderId, string number, DateTime shippedAtUtc)
    {
        UnitId = unitId;
        SalesOrderId = salesOrderId;
        Number = number;
        ShippedAtUtc = shippedAtUtc;
    }

    public Guid UnitId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public string Number { get; private set; } = default!;
    public DateTime ShippedAtUtc { get; private set; }
    public IReadOnlyList<ShipmentLine> Lines => _lines;
    public int TotalPairs => _lines.Sum(l => l.Quantity);

    internal void AddLine(Guid salesOrderLineId, int size, int quantity) =>
        _lines.Add(new ShipmentLine(Id, salesOrderLineId, size, quantity));
}

public class ShipmentLine : Entity
{
    private ShipmentLine() { }
    internal ShipmentLine(Guid shipmentId, Guid salesOrderLineId, int size, int quantity)
    {
        ShipmentId = shipmentId;
        SalesOrderLineId = salesOrderLineId;
        Size = size;
        Quantity = quantity;
    }

    public Guid ShipmentId { get; private set; }
    public Guid SalesOrderLineId { get; private set; }
    public int Size { get; private set; }
    public int Quantity { get; private set; }
}
