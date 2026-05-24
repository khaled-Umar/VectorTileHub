\# VectorTileHub Specification Seed



\## Product Name



\*\*K1-Soft VectorTileHub\*\*



\## Technical Namespace



```csharp

K1Soft.IT.VectorTileHub



Objective



Build a reusable ASP.NET Core C# library that adds Mapbox Vector Tile / MVT / PBF tile-server capabilities to any ASP.NET Core project.



The library should expose configurable API endpoints that serve vector tiles using:

GET /vector-tile-hub/tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf

The server must support dynamic tile generation, role/scope-aware cache, disk cache, background cache generation, cache deletion, cache invalidation, and extensible data providers.



The library must be designed as a reusable infrastructure package, not as a standalone hardcoded application.

