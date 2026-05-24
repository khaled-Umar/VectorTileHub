# Getting Started

## Build

```powershell
dotnet build VectorTileHub.sln
```

## Run Sample

```powershell
dotnet run --project src/K1Soft.IT.VectorTileHub.Sample/K1Soft.IT.VectorTileHub.Sample.csproj
```

Open the root URL printed by ASP.NET Core.

## Verify

```http
GET /vector-tile-hub/layers
GET /vector-tile-hub/health
GET /vector-tile-hub/tiles/82/14/9978/7171.pbf
```

The tile endpoint needs the sample SQL Server database to be available.
