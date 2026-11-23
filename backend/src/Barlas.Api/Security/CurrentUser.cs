using System.Security.Claims;
using Barlas.Application.Abstractions;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Api.Security;

public static class BarlasClaims
{
    public const string Permission = "perm";
}

/// <summary>کاربر و مجوزهای بارگذاری‌شده برای این درخواست (یک‌بار در هر درخواست).</summary>
public sealed class CurrentUserState
{
    public bool Loaded { get; set; }
    public Guid? UserId { get; set; }
    public Guid? UnitId { get; set; }
    public HashSet<Permission> Permissions { get; } = [];
}

/// <summary>
/// کاربر جاری. بیرون از درخواست HTTP (seed، کار پس‌زمینه) «سیستم» است و همه واحدها را می‌بیند؛
/// درخواست HTTP بدون ورود هیچ داده واحدی را نمی‌بیند.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor, CurrentUserState state) : ICurrentUser
{
    private bool IsSystem => accessor.HttpContext is null;

    public bool IsAuthenticated => state.Loaded;
    public Guid? UserId => state.UserId;
    public Guid? UnitId => state.UnitId;
    public IReadOnlySet<Permission> Permissions => state.Permissions;
    public bool Has(Permission permission) => state.Permissions.Contains(permission);
    public bool SeesAllUnits => IsSystem || Has(Permission.ViewAllUnits);
}

/// <summary>
/// بعد از احراز هویت JWT، کاربر و سطح دسترسی‌اش را از دیتابیس می‌خواند.
/// کاربر غیرفعال یا حذف‌شده فوراً بیرون می‌افتد و تغییر مجوزها بدون ورود دوباره اثر دارد.
/// </summary>
public sealed class LoadCurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, CurrentUserState state, IAppDbContext db)
    {
        var principal = http.User;
        if (principal.Identity?.IsAuthenticated == true &&
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var row = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId && u.IsActive)
                .Join(db.Roles.AsNoTracking(), u => u.RoleId, r => r.Id, (u, r) => new { u.UnitId, Role = r })
                .FirstOrDefaultAsync(http.RequestAborted);

            if (row is null)
            {
                http.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else
            {
                state.Loaded = true;
                state.UserId = userId;
                state.UnitId = row.UnitId;
                foreach (var p in Enum.GetValues<Permission>().Where(row.Role.Has)) state.Permissions.Add(p);

                var identity = (ClaimsIdentity)principal.Identity;
                foreach (var p in state.Permissions) identity.AddClaim(new Claim(BarlasClaims.Permission, p.ToString()));
            }
        }

        await next(http);
    }
}

/// <summary>هر مجوز یک policy هم‌نام دارد: [Authorize(Policy = nameof(Permission.ManageUsers))].</summary>
public static class PermissionPolicies
{
    public static void Add(AuthorizationOptions o)
    {
        foreach (var p in Enum.GetValues<Permission>())
            o.AddPolicy(p.ToString(), b => b.RequireAuthenticatedUser().RequireClaim(BarlasClaims.Permission, p.ToString()));
    }
}
