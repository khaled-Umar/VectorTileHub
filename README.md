# VectorTileHub

`VectorTileHub` is a reusable ASP.NET Core library for serving secure, cache-aware Mapbox Vector Tiles from configurable GIS data sources.

The solution targets `.NET 10` and uses the namespace:

```csharp
K1Soft.IT.VectorTileHub
```

## Projects

- `K1Soft.IT.VectorTileHub.Abstractions`: public contracts and shared models.
- `K1Soft.IT.VectorTileHub.Core`: orchestration, tile math, cache services, security scope resolution, and MVT encoding entry points.
- `K1Soft.IT.VectorTileHub.AspNetCore`: endpoint and host integration.
- `K1Soft.IT.VectorTileHub.Providers.SqlServer`: SQL Server spatial provider.
- `K1Soft.IT.VectorTileHub.Providers.Oracle`: Oracle spatial provider.
- `K1Soft.IT.VectorTileHub.Providers.Postgis`: PostgreSQL/PostGIS spatial provider.
- `K1Soft.IT.VectorTileHub.Storage`: SQLite runtime settings store.
- `K1Soft.IT.VectorTileHub.Jobs`: Hangfire cache jobs + cache-admin service.
- `K1Soft.IT.VectorTileHub.Server`: provider-agnostic facade — one reference registers the whole stack.
- `K1Soft.IT.VectorTileHub.Sample`: sample ASP.NET Core host with a map viewer.

> **Architecture note:** the library exposes **no HTTP endpoints** — it registers services
> (`IVectorTileService`, `IVectorTileLayerConfigProvider`, `IVectorTileCacheAdmin`) that the host
> calls from its own controllers. See the [Developer Tutorial](docs/developer-tutorial.md).

## Quick Start

```powershell
dotnet build VectorTileHub.slnx
dotnet run --project src/K1Soft.IT.VectorTileHub.Sample/K1Soft.IT.VectorTileHub.Sample.csproj
```

Open:

```text
http://localhost:5000/
```

The sample map loads layer metadata from `/vector-tile-hub/layers` and renders vector tiles from:

```text
/vector-tile-hub/tiles/82/{z}/{x}/{y}.pbf
```

## Sample Database

The sample app is configured for:

```text
Server=127.0.0.1;Database=UALSDb;User ID=sa;Password=asdAAA123;TrustServerCertificate=True;Connection Timeout=3600
```

Layer `82` reads from:

```sql
[UALSDb].[ualsdataview].[LayerData_82]
```

Only a curated set of attributes is emitted in tiles.

## Documentation

- **[Developer Tutorial](docs/developer-tutorial.md)** — full end-to-end guide (start here)
- [Architecture](docs/architecture.md)
- [Configuration](docs/configuration.md)
- [Sample Map](docs/sample-map.md)
- [Cache Operations](docs/cache-operations.md)
- [Provider Setup](docs/provider-setup.md)
- [Wiki Home](wiki/Home.md)

## Current Implementation Notes

- The solution builds successfully on `.NET 10`.
- The sample map shell and metadata endpoint work without a live SQL Server.
- Tile requests require the configured SQL Server database to be reachable.
- The current encoder returns valid empty MVT tiles; full feature geometry encoding remains the next implementation task.
