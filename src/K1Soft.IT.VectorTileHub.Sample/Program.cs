using K1Soft.IT.VectorTileHub;
using K1Soft.IT.VectorTileHub.AspNetCore;
using K1Soft.IT.VectorTileHub.Jobs;
using K1Soft.IT.VectorTileHub.Providers.SqlServer;
using K1Soft.IT.VectorTileHub.Sample;
using K1Soft.IT.VectorTileHub.Sample.Tools;
using K1Soft.IT.VectorTileHub.Storage;
using Microsoft.AspNetCore.DataProtection;

// ----------------------------------------------------------------------------
// CLI: generate the OpenLayers (Mapbox GL) style from the supplied SLD, e.g.
//   dotnet run --project src/K1Soft.IT.VectorTileHub.Sample -- gen-style tmp/layerStyle.sld src/K1Soft.IT.VectorTileHub.Sample/wwwroot/ol-style.json
// ----------------------------------------------------------------------------
if (args.Length >= 1 && string.Equals(args[0], "gen-style", StringComparison.OrdinalIgnoreCase))
{
    var sld = args.Length >= 2 ? args[1] : "tmp/layerStyle.sld";
    var outPath = args.Length >= 3 ? args[2] : "wwwroot/ol-style.json";
    // maxZoom = the layer's top tile zoom. The viewer over-zooms beyond this (renders these tiles
    // scaled) rather than requesting tiles above the layer's maxZoom.
    var json = SldToStyleConverter.Convert(sld, sourceLayer: "parcels", tileUrlTemplate: "/vector-tile-hub/tiles/82/{z}/{x}/{y}.pbf", minZoom: 12, maxZoom: 17);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllText(outPath, json);
    Console.WriteLine($"Wrote {outPath} from {sld}.");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var tempRoot = new DirectoryInfo(@"D:\Temp\VectorTileHub");
tempRoot.Create();

builder.Services.AddDataProtection()
    .SetApplicationName("VectorTileHub.Sample")
    .UseEphemeralDataProtectionProvider();

// The host owns authentication/authorization. This sample uses a demo scheme; a real
// host would plug in its own auth and (optionally) restrict the admin/dashboard routes.
builder.Services.AddAuthentication("Demo").AddScheme<DemoAuthenticationOptions, DemoAuthenticationHandler>("Demo", _ => { });
builder.Services.AddAuthorization();

// The host owns MVC now — the library exposes no controllers. AddControllers() registers this
// host's controllers (the tile/layer/admin exposers) and the API explorer that SwaggerGen consumes.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// One facade call wires the whole server stack (core + storage + jobs); the database
// provider is added separately (the facade is intentionally provider-agnostic).
builder.Services.AddVectorTileHubServer(builder.Configuration);
builder.Services.AddVectorTileHubSqlServerProvider();

var app = builder.Build();

await app.Services.EnsureVectorTileHubStorageAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
// The host owns the HTTP surface. Map this host's controllers (TilesController, LayersController,
// CacheAdminController, ConfigAdminController, SampleExtentController), the health check, and the
// Hangfire dashboard — the dashboard secured by the host's own authorization filter.
app.MapControllers();
app.MapHealthChecks("/vector-tile-hub/health");
app.UseVectorTileHubHangfireDashboard(new SampleDashboardAuthorizationFilter());

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
