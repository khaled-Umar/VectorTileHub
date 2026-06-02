using K1Soft.IT.VectorTileHub;
using K1Soft.IT.VectorTileHub.AspNetCore;
using K1Soft.IT.VectorTileHub.Jobs;
using K1Soft.IT.VectorTileHub.Providers.SqlServer;
using K1Soft.IT.VectorTileHub.Sample.Tools;
using K1Soft.IT.VectorTileHub.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.IO;

// ----------------------------------------------------------------------------
// CLI: generate the OpenLayers (Mapbox GL) style from the supplied SLD, e.g.
//   dotnet run --project src/K1Soft.IT.VectorTileHub.Sample -- gen-style tmp/layerStyle.sld src/K1Soft.IT.VectorTileHub.Sample/wwwroot/ol-style.json
// ----------------------------------------------------------------------------
if (args.Length >= 1 && string.Equals(args[0], "gen-style", StringComparison.OrdinalIgnoreCase))
{
    var sld = args.Length >= 2 ? args[1] : "tmp/layerStyle.sld";
    var outPath = args.Length >= 3 ? args[2] : "wwwroot/ol-style.json";
    var json = SldToStyleConverter.Convert(sld, sourceLayer: "parcels", tileUrlTemplate: "/vector-tile-hub/tiles/82/{z}/{x}/{y}.pbf", minZoom: 12, maxZoom: 21);
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddVectorTileHub(builder.Configuration);
builder.Services.AddVectorTileHubSqlServerProvider();
builder.Services.AddVectorTileHubJobs(builder.Configuration);

var app = builder.Build();

await app.Services.EnsureVectorTileHubStorageAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapVectorTileHubEndpoints();

// Sample-only helper: returns the layer's data bounding box (EPSG:3857) so the
// OpenLayers page can zoom straight to the data instead of opening at world view.
app.MapGet("/sample/layers/{id:int}/extent", async (int id, IVectorTileLayerConfigProvider layers, IConfiguration config, CancellationToken ct) =>
{
    var layer = layers.GetLayer(id);
    if (layer is null)
    {
        return Results.NotFound(new { error = "Layer not found" });
    }

    var connectionString = !string.IsNullOrWhiteSpace(layer.Provider.ConnectionString)
        ? layer.Provider.ConnectionString
        : config.GetConnectionString(layer.Provider.ConnectionStringName ?? "");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("No connection string configured for the layer.");
    }

    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var sql = $"SELECT geometry::EnvelopeAggregate(t.g).STAsText() FROM (SELECT TOP 5000 [{layer.Provider.GeometryColumn}] AS g FROM {layer.Provider.TableName} WHERE [{layer.Provider.GeometryColumn}] IS NOT NULL) t";
        await using var cmd = new SqlCommand(sql, conn);
        if (await cmd.ExecuteScalarAsync(ct) is not string wkt || string.IsNullOrWhiteSpace(wkt))
        {
            return Results.NoContent();
        }

        var env = new WKTReader().Read(wkt).EnvelopeInternal;
        // Source SRID 4326 is stored as planar lon/lat; project corners to Web Mercator for the OL view.
        var (minX, minY) = layer.Provider.SourceSrid == 4326 ? LonLatToMercator(env.MinX, env.MinY) : (env.MinX, env.MinY);
        var (maxX, maxY) = layer.Provider.SourceSrid == 4326 ? LonLatToMercator(env.MaxX, env.MaxY) : (env.MaxX, env.MaxY);
        return Results.Ok(new { minX, minY, maxX, maxY, srid = 3857 });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Extent query failed: {ex.Message}");
    }
});

// Host supplies dashboard authorization (none here = Hangfire's local-only default).
app.UseVectorTileHubHangfireDashboard();

app.Run();

static (double x, double y) LonLatToMercator(double lon, double lat)
{
    const double originShift = 20037508.342789244;
    var x = lon * originShift / 180.0;
    var y = Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0) * originShift / 180.0;
    return (x, y);
}

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
