# Provider Setup

Providers are registered with keyed dependency injection.

## SQL Server

```csharp
builder.Services.AddVectorTileHub(builder.Configuration);
builder.Services.AddVectorTileHubSqlServerProvider();
```

Layer provider config:

```json
{
  "type": "SqlServer",
  "connectionStringName": "Default",
  "tableName": "[UALSDb].[ualsdataview].[LayerData_82]",
  "idColumn": "Id",
  "geometryColumn": "Geom",
  "sourceSrid": 3857
}
```

## Oracle

```csharp
builder.Services.AddVectorTileHub(builder.Configuration);
builder.Services.AddVectorTileHubOracleProvider();
```

Provider implementations must keep SQL parameterized and must apply scope filters server-side.
