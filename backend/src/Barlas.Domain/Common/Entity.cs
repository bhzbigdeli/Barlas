namespace Barlas.Domain.Common;

/// <summary>پایه همه موجودیت‌ها. شناسه Guid ترتیبی است (EF Core روی SQL Server تولیدش می‌کند).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>موجودیتی که زمان و کاربر ایجاد/ویرایش آن ثبت می‌شود (توسط DbContext پر می‌شود).</summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// داده‌ای که مال یک واحد است. هر کاربرِ وابسته به واحد فقط داده‌های واحد خودش را می‌بیند
/// (فیلتر سراسری در DbContext)؛ مالک و مدیر کارخانه همه واحدها را.
/// </summary>
public interface IUnitScoped
{
    Guid UnitId { get; }
}
