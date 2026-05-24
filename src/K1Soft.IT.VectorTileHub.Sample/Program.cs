using K1Soft.IT.VectorTileHub.AspNetCore;
using K1Soft.IT.VectorTileHub.Jobs;
using K1Soft.IT.VectorTileHub.Providers.SqlServer;
using K1Soft.IT.VectorTileHub.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var tempRoot = new DirectoryInfo(@"D:\Temp\VectorTileHub");
tempRoot.Create();

builder.Services.AddDataProtection()
    .SetApplicationName("VectorTileHub.Sample")
    .UseEphemeralDataProtectionProvider();

builder.Services.AddAuthentication("Demo").AddScheme<DemoAuthenticationOptions, DemoAuthenticationHandler>("Demo", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddVectorTileHub(builder.Configuration);
builder.Services.AddVectorTileHubSqlServerProvider();
builder.Services.AddVectorTileHubJobs(builder.Configuration);

var app = builder.Build();

await app.Services.EnsureVectorTileHubStorageAsync();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapVectorTileHubEndpoints();
K1Soft.IT.VectorTileHub.AspNetCore.VectorTileHubEndpointRouteBuilderExtensions.UseVectorTileHubHangfireDashboard(app);

app.Run();

public sealed class DemoAuthenticationOptions : Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;

public sealed class DemoAuthenticationHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<DemoAuthenticationOptions>
{
    public DemoAuthenticationHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<DemoAuthenticationOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "sample-user"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "GISAdmin")
        ], Scheme.Name);

        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(
            new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), Scheme.Name)));
    }
}
