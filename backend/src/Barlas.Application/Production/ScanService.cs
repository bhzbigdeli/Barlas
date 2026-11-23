using Barlas.Application.Abstractions;
using Barlas.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Barlas.Application.Production;

public sealed record ScanRequest(string QrToken, string WorkerCode);

public sealed record ReworkRequest(string QrToken, string WorkerCode, Guid ToStageId, string? Note);

public sealed record BundleState(Guid BundleId, string Number, string ModelName, string ColorName, int Pairs,
    string Status, string? LastStage, string? NextStage);

/// <summary>اسکن کاردکس QR در سالن با گوشی: ثبت پایان مرحله بعدی دسته به نام یک کارگر.</summary>
public class ScanService(IAppDbContext db, ICurrentUser user, IClock clock)
{
    public async Task<BundleState> LookupAsync(string qrToken, CancellationToken ct)
    {
        var bundle = await db.Bundles.AsNoTracking().Include(b => b.Sizes).FirstOrDefaultAsync(b => b.QrToken == qrToken, ct)
            ?? throw AppException.NotFound("دسته");
        return await DescribeAsync(bundle, ct);
    }

    public async Task<BundleState> CompleteNextStageAsync(ScanRequest request, CancellationToken ct)
    {
        var (bundle, worker) = await LoadAsync(request.QrToken, request.WorkerCode, ct);
        var stageId = bundle.NextStageId ?? throw new AppException("همه مراحل این دسته تمام شده است.");

        var scan = bundle.CompleteStage(stageId, worker.Id, user.UserId!.Value, clock.UtcNow);
        db.StageScans.Add(scan);
        await db.SaveChangesAsync(ct);
        return await DescribeAsync(bundle, ct);
    }

    public async Task<BundleState> ReworkAsync(ReworkRequest request, CancellationToken ct)
    {
        var (bundle, worker) = await LoadAsync(request.QrToken, request.WorkerCode, ct);
        var scan = bundle.SendBackForRework(request.ToStageId, worker.Id, user.UserId!.Value, clock.UtcNow, request.Note);
        db.StageScans.Add(scan);
        await db.SaveChangesAsync(ct);
        return await DescribeAsync(bundle, ct);
    }

    private async Task<(Domain.Production.Bundle, Domain.Organization.Worker)> LoadAsync(string qrToken, string workerCode, CancellationToken ct)
    {
        var bundle = await db.Bundles.Include(b => b.Sizes).FirstOrDefaultAsync(b => b.QrToken == qrToken, ct)
            ?? throw AppException.NotFound("دسته");
        var worker = await db.Workers.FirstOrDefaultAsync(
            w => w.UnitId == bundle.UnitId && w.PersonnelCode == workerCode && w.IsActive, ct)
            ?? throw AppException.NotFound("کارگر این واحد");
        return (bundle, worker);
    }

    private async Task<BundleState> DescribeAsync(Domain.Production.Bundle b, CancellationToken ct)
    {
        var stages = await db.Stages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var model = await db.ShoeModels.AsNoTracking().Where(m => m.Id == b.ShoeModelId).Select(m => m.Name).FirstAsync(ct);
        var color = await db.Colors.AsNoTracking().Where(c => c.Id == b.ColorId).Select(c => c.Name).FirstAsync(ct);
        return new BundleState(b.Id, b.Number, model, color, b.TotalPairs, b.Status.ToString(),
            b.LastCompletedStageId is { } last ? stages[last] : null,
            b.NextStageId is { } next ? stages[next] : null);
    }
}
