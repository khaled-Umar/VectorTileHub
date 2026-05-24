# Providers

VectorTileHub currently includes SQL Server and Oracle provider projects.

Provider rules:

- use parameterized SQL
- return normalized `VectorTileFeature` objects
- apply security scope filters in the database query
- return only whitelisted attributes
- avoid EF Core tracking on tile query hot paths

Add future providers by implementing `IVectorTileFeatureProvider` and registering the implementation as a keyed service.
