# Configuration

VectorTileHub is configured through `appsettings.json` and per-layer JSON files.

## Global Configuration

```json
{
  "VectorTileHub": {
    "Enabled": true,
    "RoutePrefix": "/vector-tile-hub",
    "LayerConfigFolder": "VectorTileHub/Layers",
    "DefaultCacheRootFolder": "VectorTileHub/Cache",
    "UseMemoryCache": true,
    "UseDiskCache": true,
    "DefaultAuthenticationRequired": false,
    "InternalSettingsStore": {
      "Provider": "Sqlite",
      "ConnectionString": "Data Source=VectorTileHub/vector_tile_hub.db"
    },
    "Hangfire": {
      "Enabled": true,
      "DashboardPath": "/vector-tile-hub/jobs",
      "RequiredRoles": [ "Admin", "GISAdmin" ]
    }
  }
}
```

## Layer Configuration

Layer files live under the configured `LayerConfigFolder`.

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
    "sourceSrid": 3857
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

Do not include audit columns, concurrency fields, soft-delete metadata, or large unrelated fields in the tile whitelist.
