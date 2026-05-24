# Configuration

VectorTileHub uses two levels of configuration:

- Global host settings in `appsettings.json`
- One JSON file per layer under `VectorTileHub/Layers`

Important settings:

- `RoutePrefix`: endpoint prefix
- `LayerConfigFolder`: layer JSON folder
- `DefaultCacheRootFolder`: disk cache root
- `InternalSettingsStore`: SQLite runtime settings database
- `Hangfire`: background job dashboard configuration

Layer files define provider settings, tile rules, attributes, security, and cache behavior.
