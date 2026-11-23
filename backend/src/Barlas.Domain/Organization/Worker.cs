using Barlas.Domain.Common;

namespace Barlas.Domain.Organization;

/// <summary>کارگر سالن. کارکرد او در هر مرحله با اسکن ثبت می‌شود (پایه دستمزد کارمزدی فاز ۲).</summary>
public class Worker : AuditableEntity, IUnitScoped
{
    private Worker() { }

    public Worker(Guid unitId, string personnelCode, string fullName)
    {
        if (string.IsNullOrWhiteSpace(personnelCode)) throw new DomainException("کد پرسنلی لازم است.");
        UnitId = unitId;
        PersonnelCode = personnelCode.Trim();
        Rename(fullName);
    }

    public Guid UnitId { get; private set; }
    public string PersonnelCode { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("نام کارگر لازم است.");
        FullName = fullName.Trim();
    }

    public void SetActive(bool active) => IsActive = active;
    public void Deactivate() => IsActive = false;
}
