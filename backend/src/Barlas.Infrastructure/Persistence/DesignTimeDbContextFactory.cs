using Barlas.Application.Abstractions;
using Barlas.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Barlas.Infrastructure.Persistence;

/// <summary>فقط برای ابزار dotnet ef (ساخت migration)؛ به دیتابیس وصل نمی‌شود.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=Barlas;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options, new DesignTimeUser(), new DesignTimeClock());
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public Guid? UserId => null;
        public Guid? UnitId => null;
        public bool IsAuthenticated => false;
        public bool SeesAllUnits => true;
        public IReadOnlySet<Permission> Permissions { get; } = new HashSet<Permission>();
        public bool Has(Permission permission) => false;
    }

    private sealed class DesignTimeClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
