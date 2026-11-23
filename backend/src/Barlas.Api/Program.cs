using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Barlas.Api.Infrastructure;
using Barlas.Api.Security;
using Barlas.Application;
using Barlas.Application.Abstractions;
using Barlas.Infrastructure;
using Barlas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- احراز هویت و نقش‌ها ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserState>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.Section);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey باید حداقل ۳۲ کاراکتر باشد.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.Key(),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization(PermissionPolicies.Add);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// --- API ---
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// پشت CDN آروان یا پراکسی دیگر: IP واقعی کاربر از X-Forwarded-For (برای محدودیت تلاش ورود).
// فقط وقتی روشن شود که سرور مستقیم از اینترنت در دسترس نیست، وگرنه سربرگ قابل جعل است.
var trustForwarded = builder.Configuration.GetValue("ReverseProxy:TrustForwardedHeaders", false);
if (trustForwarded)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (trustForwarded) app.UseForwardedHeaders();
app.UseExceptionHandler();

// صفحه‌های وب (خروجی build فرانت در wwwroot) از همین برنامه سرو می‌شوند: یک سایت در IIS.
var serveWeb = File.Exists(Path.Combine(app.Environment.WebRootPath ?? "", "index.html"));
if (serveWeb)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<LoadCurrentUserMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (serveWeb)
{
    // آدرس ناشناخته زیر /api خطای ۴۰۴ بماند؛ بقیه مسیرهای SPA به index.html.
    app.Map("/api/{**rest}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

// مهاجرت دیتابیس و داده‌های اولیه (اطلاعات پایه واقعی از SeedData).
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.Run();

public partial class Program;
