using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Barlas.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Organization;

public sealed record WorkerDto(Guid Id, Guid UnitId, string UnitName, string PersonnelCode, string FullName, bool IsActive);

public sealed record CreateWorkerRequest(Guid? UnitId, string PersonnelCode, string FullName);

public sealed record UpdateWorkerRequest(string FullName, bool IsActive);

/// <summary>کارگران سالن؛ کد پرسنلی هنگام اسکن کاردکس وارد می‌شود. هر واحد فقط کارگران خودش را می‌بیند.</summary>
public class WorkerService(IAppDbContext db, ICurrentUser user)
{
    public async Task<IReadOnlyList<WorkerDto>> ListAsync(CancellationToken ct)
    {
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var workers = await db.Workers.AsNoTracking().OrderBy(w => w.UnitId).ThenBy(w => w.PersonnelCode).ToListAsync(ct);
        return workers.Select(w => ToDto(w, units)).ToList();
    }

    public async Task<WorkerDto> CreateAsync(CreateWorkerRequest request, CancellationToken ct)
    {
        var unitId = user.SeesAllUnits ? request.UnitId : user.UnitId;
        if (unitId is null) throw new AppException("واحد کارگر را انتخاب کنید.");
        if (!await db.Units.AnyAsync(u => u.Id == unitId, ct)) throw AppException.NotFound("واحد");

        var code = request.PersonnelCode.Trim();
        if (await db.Workers.AnyAsync(w => w.UnitId == unitId && w.PersonnelCode == code, ct))
            throw new AppException($"کد پرسنلی «{code}» در این واحد قبلاً ثبت شده است.", 409);

        var worker = new Worker(unitId.Value, code, request.FullName);
        db.Workers.Add(worker);
        await db.SaveChangesAsync(ct);
        return await GetAsync(worker.Id, ct);
    }

    public async Task<WorkerDto> UpdateAsync(Guid id, UpdateWorkerRequest request, CancellationToken ct)
    {
        var worker = await db.Workers.FirstOrDefaultAsync(w => w.Id == id, ct) ?? throw AppException.NotFound("کارگر");
        worker.Rename(request.FullName);
        worker.SetActive(request.IsActive);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<WorkerDto> GetAsync(Guid id, CancellationToken ct)
    {
        var w = await db.Workers.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return ToDto(w, units);
    }

    private static WorkerDto ToDto(Worker w, IReadOnlyDictionary<Guid, string> units) =>
        new(w.Id, w.UnitId, units.GetValueOrDefault(w.UnitId, "?"), w.PersonnelCode, w.FullName, w.IsActive);
}
