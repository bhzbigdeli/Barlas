using Barlas.Domain.Common;

namespace Barlas.Domain.Organization;

/// <summary>
/// واحد تولیدی (واحد اصلی، سالن صندل و در فاز ۲ واحد تزریق).
/// سفارش‌های سیستم فروش تهران با «کد تامین‌کننده» به واحد نسبت داده می‌شوند.
/// </summary>
public class ProductionUnit : AuditableEntity
{
    private ProductionUnit() { }

    public ProductionUnit(string code, string name, string supplierCode)
    {
        Code = code;
        Name = name;
        SupplierCode = supplierCode;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    /// <summary>کد تامین‌کننده این واحد در سیستم فروش تهران.</summary>
    public string SupplierCode { get; private set; } = default!;

    public bool IsActive { get; private set; } = true;

    /// <summary>حداکثر درصد مازاد تولید بدون نیاز به تأیید مدیر واحد (پیش‌فرض ۲۰٪).</summary>
    public decimal SurplusApprovalThreshold { get; private set; } = 0.20m;

    public void Rename(string name) => Name = name;
    public void Deactivate() => IsActive = false;
}
