using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Admin;

public sealed record RoleDto(Guid Id, string Name, bool IsSystem, IReadOnlyList<Permission> Permissions, int UserCount);

public sealed record SaveRoleRequest(string Name, IReadOnlyList<Permission> Permissions);

/// <summary>تعریف سطوح دسترسی.</summary>
public class RoleService(IAppDbContext db)
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToListAsync(ct);
        var counts = await db.Users.GroupBy(u => u.RoleId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return roles.Select(r => ToDto(r, counts.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<RoleDto> CreateAsync(SaveRoleRequest request, CancellationToken ct)
    {
        await EnsureUniqueNameAsync(request.Name, null, ct);
        var role = new Role(request.Name, request.Permissions);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return ToDto(role, 0);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, SaveRoleRequest request, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw AppException.NotFound("سطح دسترسی");
        await EnsureUniqueNameAsync(request.Name, id, ct);
        role.Rename(request.Name);
        if (!role.IsSystem) role.SetPermissions(request.Permissions);

        // کاربری که واحد ندارد نباید سطحش «دیدن همه واحدها» را از دست بدهد.
        if (!role.SeesAllUnits && await db.Users.AnyAsync(u => u.RoleId == id && u.UnitId == null, ct))
            throw new AppException("کاربرانی با این سطح واحد مشخصی ندارند؛ اول برایشان واحد تعیین کنید یا «دیدن همه واحدها» را نگه دارید.");

        await db.SaveChangesAsync(ct);
        return ToDto(role, await db.Users.CountAsync(u => u.RoleId == id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw AppException.NotFound("سطح دسترسی");
        if (role.IsSystem) throw new AppException("سطح «مالک» قابل حذف نیست.");
        if (await db.Users.AnyAsync(u => u.RoleId == id, ct))
            throw new AppException("این سطح دسترسی کاربر دارد؛ اول کاربرانش را به سطح دیگری ببرید.");
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == trimmed && r.Id != exceptId, ct))
            throw new AppException($"سطح دسترسی «{trimmed}» قبلاً تعریف شده است.", 409);
    }

    private static RoleDto ToDto(Role r, int users) =>
        new(r.Id, r.Name, r.IsSystem, Enum.GetValues<Permission>().Where(r.Has).ToList(), users);
}
