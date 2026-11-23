using Barlas.Application.Abstractions;
using Barlas.Application.Orders;
using Barlas.Infrastructure.Orders;
using Barlas.Infrastructure.Persistence;
using Barlas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Barlas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Barlas")
            ?? throw new InvalidOperationException("ConnectionStrings:Barlas تنظیم نشده است.");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString,
            sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IOrderSource, ExcelOrderSource>();
        services.AddScoped<DbSeeder>();
        return services;
    }
}
