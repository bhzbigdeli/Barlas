using Barlas.Domain.Identity;

namespace Barlas.Application.Admin;

public sealed record PermissionInfo(Permission Permission, string Name, string Description);

/// <summary>نام و توضیح فارسی هر مجوز برای صفحه تعریف سطوح دسترسی.</summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionInfo> All =
    [
        new(Permission.ViewAllUnits, "دیدن همه واحدها", "سفارش‌ها و آمار همه واحدها را می‌بیند؛ بدون آن فقط واحد خود کاربر."),
        new(Permission.ManageUsers, "تعریف کاربران و سطوح دسترسی", "ساخت و ویرایش کاربران، رمز، و مجوزهای هر سطح."),
        new(Permission.ManageCatalog, "تعریف محصول و اطلاعات پایه", "محصول، سری سایز، رنگ و زیره."),
        new(Permission.ImportOrders, "ورود سفارش‌های تهران", "ورود فایل سفارش از سیستم فروش."),
        new(Permission.ManagePlans, "برنامه‌ریزی تولید", "تقسیم سفارش به زیربرنامه و صدور دسته‌ها."),
        new(Permission.ApproveSurplus, "تأیید مازاد", "تأیید زیربرنامه‌ای که مازادش از آستانه واحد بیشتر است."),
        new(Permission.ScanStages, "اسکن مراحل سالن", "ثبت پایان مراحل با اسکن کاردکس."),
        new(Permission.ManageWarehouse, "انبار محصول", "ورود دسته به انبار و ارسال به تهران."),
        new(Permission.ViewReports, "گزارش‌ها", "دیدن گزارش‌های روزانه و داشبورد."),
    ];
}
