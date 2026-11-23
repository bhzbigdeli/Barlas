using Barlas.Domain.Common;

namespace Barlas.Domain.Organization;

/// <summary>کارگر سالن. کارکرد او در هر مرحله با اسکن ثبت می‌شود (پایه دستمزد کارمزدی فاز ۲).</summary>
public class Worker : AuditableEntity, IUnitScoped
{
    private Worker() { }

    public Worker(Guid unitId, string personnelCode, string fullName)
    {
        UnitId = unitId;
        PersonnelCode = personnelCode;
        FullName = fullName;
    }

    public Guid UnitId { get; private set; }
    public string PersonnelCode { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}
