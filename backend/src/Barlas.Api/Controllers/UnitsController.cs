using Barlas.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Api.Controllers;

public sealed record UnitDto(Guid Id, string Code, string Name);

[ApiController]
[Authorize]
[Route("api/units")]
public class UnitsController(IAppDbContext db, ICurrentUser user) : ControllerBase
{
    /// <summary>واحدهایی که کاربر می‌تواند ببیند.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<UnitDto>> List(CancellationToken ct) =>
        await db.Units.AsNoTracking()
            .Where(u => u.IsActive && (user.SeesAllUnits || u.Id == user.UnitId))
            .OrderBy(u => u.Code)
            .Select(u => new UnitDto(u.Id, u.Code, u.Name))
            .ToListAsync(ct);
}
