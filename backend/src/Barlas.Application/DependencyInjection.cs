using Barlas.Application.Admin;
using Barlas.Application.Auth;
using Barlas.Application.Catalog;
using Barlas.Application.Orders;
using Barlas.Application.Organization;
using Barlas.Application.Planning;
using Barlas.Application.Production;
using Microsoft.Extensions.DependencyInjection;

namespace Barlas.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<OrderIntakeService>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<ScanService>();
        services.AddScoped<PlanningService>();
        services.AddScoped<WorkerService>();
        services.AddScoped<Barlas.Application.Reports.ReportService>();
        return services;
    }
}
