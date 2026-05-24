# Sample Map

The sample application includes an OpenLayers map viewer in:

```text
src/K1Soft.IT.VectorTileHub.Sample/wwwroot/
```

## Run

```powershell
dotnet run --project src/K1Soft.IT.VectorTileHub.Sample/K1Soft.IT.VectorTileHub.Sample.csproj
```

Open the root URL shown by ASP.NET Core.

## Behavior

- Loads layer metadata from `/vector-tile-hub/layers`.
- Uses the first available layer, preferring layer `82`.
- Renders vector tiles from the metadata `tileUrlTemplate`.
- Displays links to metadata, health, and Hangfire jobs.

## External Assets

The sample page uses OpenLayers from a CDN and OpenStreetMap raster tiles as a base map. For offline environments, vendor these assets locally and update `index.html`.
