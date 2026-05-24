# Troubleshooting

## Layer list is empty

Check `VectorTileHub:LayerConfigFolder` and confirm the JSON files are copied to the sample app output.

## Tile endpoint returns 503

The provider may not be registered or the configured database may be unavailable.

## Tile endpoint returns empty bytes

The current encoder implementation supports valid empty MVT output. Full feature geometry encoding is tracked as the remaining encoder task.

## Hangfire dashboard is inaccessible

Check `VectorTileHub:Hangfire:RequiredRoles` and the host authentication setup.
