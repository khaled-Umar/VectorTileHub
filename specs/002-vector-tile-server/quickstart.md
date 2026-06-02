# Quickstart: VectorTileHub (002 — host-agnostic)

Get a host serving PBF tiles and rendering them in OpenLayers.

## 1. Add library references

In your ASP.NET Core (.NET 10) host:

- `K1Soft.IT.VectorTileHub.AspNetCore` (main integration package)
- `K1Soft.IT.VectorTileHub.Providers.SqlServer` and/or `...Providers.Oracle`

## 2. Register services (`Program.cs`)

```csharp
builder.Services.AddVectorTileHub(builder.Configuration);
builder.Services.AddVectorTileHubSqlServerProvider();
// builder.Services.AddVectorTileHubOracleProvider();

var app = builder.Build();

app.MapVectorTileHubEndpoints();          // host owns exposure + security
app.UseVectorTileHubHangfireDashboard(o =>
{
    // Host supplies dashboard authorization — library enforces none.
    o.Authorization = new[] { new MyDashboardAuthFilter() };
});
```

> The library does **no** authentication/authorization. Put your own
> auth/proxy in front of these endpoints. To serve role-scoped data, map the
> user's role to a **variant key** and pass it on the tile request.

## 3. Global config (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Server=127.0.0.1;Database=UALSDb;User ID=sa;Password=***;TrustServerCertificate=True"
  },
  "VectorTileHub": {
    "Enabled": true,
    "RoutePrefix": "/vector-tile-hub",
    "LayerConfigPaths": [ "VectorTileHub/Layers/82-layer-data.json" ],
    "DefaultCacheRootFolder": "VectorTileHub/Cache",
    "UseMemoryCache": true,
    "UseDiskCache": true,
    "InternalSettingsStore": { "Provider": "Sqlite" },
    "Hangfire": { "Enabled": true, "DashboardPath": "/vector-tile-hub/jobs" }
  }
}
```
Omit `InternalSettingsStore.ConnectionString` to auto-create SQLite.

## 4. Layer config with variants (`82-layer-data.json`)

```json
{
  "id": 82,
  "layerKey": "layer_data_82",
  "layerName": "Local Plan NE",
  "enabled": true,
  "provider": {
    "type": "SqlServer",
    "connectionStringName": "Default",
    "tableName": "[UALSDb].[ualsdataview].[LayerData_82]",
    "idColumn": "OBJECTID",
    "geometryColumn": "Geom",
    "sourceSrid": 3857
  },
  "tile": { "minZoom": 0, "maxZoom": 21 },
  "attributes": { "include": ["Type_t", "PARCELNUMBER", "SERVICE_NAME"] },
  "cacheRules": [
    { "variantKey": "default", "isDefault": true },
    { "variantKey": "public",
      "filter": { "column": "Type_t", "operator": "In", "values": ["Villa", "invest"] } }
  ],
  "cache": { "enabled": true, "refreshPeriodMinutes": 1440 }
}
```

## 5. Request tiles

```
GET /vector-tile-hub/tiles/82/14/9123/6234.pbf            → default variant
GET /vector-tile-hub/tiles/82/14/9123/6234.pbf?variant=public
```
- Outside zoom range or empty area → empty tile (not an error).
- Stale tile (older than `refreshPeriodMinutes`) → served immediately,
  refreshed in the background.

## 6. Manage cache (host secures these)

```
POST   /vector-tile-hub/admin/layers/82/cache/generate
POST   /vector-tile-hub/admin/layers/82/cache/swap          → blue/green
POST   /vector-tile-hub/admin/layers/82/cache/notify        → bbox refresh
DELETE /vector-tile-hub/admin/layers/82/cache
POST   /vector-tile-hub/admin/config/reload                 → explicit reload
```

## 7. Render in OpenLayers (sample)

1. Generate the style once from the SLD:
   `dotnet run --project src/K1Soft.IT.VectorTileHub.Sample -- gen-style tmp/layerStyle.sld wwwroot/ol-style.json`
2. Open the sample's `index.html` — it loads `ol-style.json` via
   `ol-mapbox-style`, points the vector source at the tile endpoint, and
   renders with the SLD-equivalent symbology (see `contracts/sld-style.md`).
3. Swagger UI (sample) documents and exercises every endpoint.

## 8. Solution

The solution uses the `.slnx` format (`VectorTileHub.slnx`) on .NET 10.
