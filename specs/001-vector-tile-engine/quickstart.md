# Quickstart: VectorTileHub

## 1. Add Library References

Add the VectorTileHub packages to your ASP.NET Core host application:

- `K1Soft.IT.VectorTileHub.AspNetCore` (main integration package)
- `K1Soft.IT.VectorTileHub.Providers.SqlServer` (for SQL Server)
- `K1Soft.IT.VectorTileHub.Providers.Oracle` (for Oracle)

## 2. Configure Services

In `Program.cs`:

```csharp
builder.Services.AddVectorTileHub(builder.Configuration);
builder.Services.AddVectorTileHubSqlServerProvider();
// builder.Services.AddVectorTileHubOracleProvider();

var app = builder.Build();

app.MapVectorTileHubEndpoints();
app.UseVectorTileHubHangfireDashboard();
```

## 3. Add Configuration

In `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=127.0.0.1;Database=UALSDb;User ID=sa;Password=***;TrustServerCertificate=True"
  },
  "VectorTileHub": {
    "Enabled": true,
    "RoutePrefix": "/vector-tile-hub",
    "LayerConfigFolder": "VectorTileHub/Layers",
    "DefaultCacheRootFolder": "VectorTileHub/Cache",
    "UseMemoryCache": true,
    "UseDiskCache": true,
    "DefaultAuthenticationRequired": true,
    "InternalSettingsStore": {
      "Provider": "Sqlite",
      "ConnectionString": "Data Source=VectorTileHub/vector_tile_hub.db"
    },
    "Hangfire": {
      "Enabled": true,
      "DashboardPath": "/vector-tile-hub/jobs",
      "RequiredRoles": ["Admin", "GISAdmin"]
    }
  }
}
```

## 4. Create a Layer Configuration

Create `VectorTileHub/Layers/82-layer-data.json`:

```json
{
  "id": 82,
  "layerKey": "parcels",
  "layerName": "Parcels Layer",
  "enabled": true,
  "provider": {
    "type": "SqlServer",
    "connectionStringName": "Default",
    "tableName": "[UALSDb].[ualsdataview].[LayerData_82]",
    "idColumn": "Id",
    "geometryColumn": "Geom",
    "sourceSrid": 4326
  },
  "tile": {
    "minZoom": 12,
    "maxZoom": 21,
    "extent": 4096,
    "buffer": 64,
    "clipGeometry": true,
    "returnEmptyTileOutsideZoomRange": true
  },
  "attributes": {
    "include": [
      "Id",
      "LayerId",
      "DISTRICT",
      "SUBMUNICIPALITY",
      "SUBDISTRICT_NAME",
      "PARCELNUMBER",
      "PARCEL_LANDUSE",
      "LAND_USES",
      "PLAN_NUMBER",
      "BlockNumber"
    ]
  }
}
```

## 5. Request a Tile

```
GET /vector-tile-hub/tiles/82/14/9978/7171.pbf
```

Returns MVT/PBF binary containing parcel geometries with the
whitelisted attributes for the requested tile area.

## 6. Check Layer Metadata

```
GET /vector-tile-hub/layers
```

Returns a JSON list of enabled layers with their IDs, names, zoom
ranges, and tile URL templates.

## 7. Admin Operations

Trigger cache generation (requires admin role):

```
POST /vector-tile-hub/admin/layers/82/cache/generate
```

Check cache status:

```
GET /vector-tile-hub/admin/layers/82/cache/status
```

View background jobs:

```
GET /vector-tile-hub/jobs
```

## 8. Health Check

```
GET /vector-tile-hub/health
```

Returns operational status of the settings store, cache folder,
and layer config folder.

## Verification Checklist

- [ ] Application starts without errors
- [ ] `GET /vector-tile-hub/layers` returns layer metadata
- [ ] `GET /vector-tile-hub/tiles/82/14/{x}/{y}.pbf` returns MVT bytes
- [ ] Tile response contains only whitelisted attributes
- [ ] `GET /vector-tile-hub/health` returns "Healthy"
- [ ] Unauthenticated requests to admin endpoints return 401
- [ ] Background job dashboard is accessible at `/vector-tile-hub/jobs`
