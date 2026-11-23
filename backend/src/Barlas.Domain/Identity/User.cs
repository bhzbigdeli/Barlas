using Barlas.Domain.Common;

namespace Barlas.Domain.Identity;

/// <summary>
/// مجوزهای قابل‌تخصیص به سطح دسترسی. فهرست در کد ثابت است؛ اینکه هر سطح کدام‌ها را دارد در برنامه تعریف می‌شود.
/// شماره‌ها در دیتابیس ذخیره می‌شوند؛ تغییرشان ندهید.
/// </summary>
public enum Permission
{
    /// <summary>دیدن و کار با همه واحدها؛ بدون آن کاربر به یک واحد محدود است.</summary>
    ViewAllUnits = 1,
    ManageUsers = 2,
    ManageCatalog = 3,
    ImportOrders = 4,
    ManagePlans = 5,
    ApproveSurplus = 6,
    ScanStages = 7,
    ManageWarehouse = 8,
    ViewReports = 9,
}

/// <summary>سطح دسترسی (نقش): نام و مجموعه مجوزها. سطح «مالک» سیستمی است و همه مجوزها را دارد.</summary>
public class Role : AuditableEntity
{
    private List<Permission> _permissions = [];
    private Role() { }

    public Role(string name, IEnumerable<Permission> permissions, bool isSystem = false)
    {
        Rename(name);
        IsSystem = isSystem;
        _permissions = isSystem ? [.. Enum.GetValues<Permission>()] : Normalize(permissions);
    }

    public string Name { get; private set; } = default!;
    public bool IsSystem { get; private set; }
    public IReadOnlyList<Permission> Permissions => _permissions;

    public bool Has(Permission p) => IsSystem || _permissions.Contains(p);
    public bool SeesAllUnits => Has(Permission.ViewAllUnits);

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("نام سطح دسترسی لازم است.");
        Name = name.Trim();
    }

    public void SetPermissions(IEnumerable<Permission> permissions)
    {
        if (IsSystem) throw new DomainException("مجوزهای سطح «مالک» قابل تغییر نیست.");
        _permissions = Normalize(permissions);
    }

    private static List<Permission> Normalize(IEnumerable<Permission> permissions) =>
        permissions.Where(Enum.IsDefined).Distinct().Order().ToList();
}

public class User : AuditableEntity
{
    private User() { }

    public User(string userName, string fullName, Role role, Guid? unitId)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new DomainException("نام کاربری لازم است.");
        UserName = NormalizeUserName(userName);
        Update(fullName, role, unitId);
    }

    public string UserName { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public Guid RoleId { get; private set; }

    /// <summary>واحد کاربر؛ برای سطحی که «دیدن همه واحدها» دارد خالی است.</summary>
    public Guid? UnitId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(string fullName, Role role, Guid? unitId)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("نام و نام خانوادگی لازم است.");
        if (!role.SeesAllUnits && unitId is null)
            throw new DomainException("برای این سطح دسترسی باید واحد مشخص شود.");

        FullName = fullName.Trim();
        RoleId = role.Id;
        UnitId = role.SeesAllUnits ? null : unitId;
    }

    public void SetPasswordHash(string hash) => PasswordHash = hash;
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public static string NormalizeUserName(string userName) => userName.Trim().ToLowerInvariant();

    /// <summary>حداقل قواعد رمز: ۸ کاراکتر، حرف و عدد.</summary>
    public static void ValidatePassword(string password)
    {
        if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new DomainException("رمز عبور باید حداقل ۸ کاراکتر و شامل حرف و عدد باشد.");
    }
}
