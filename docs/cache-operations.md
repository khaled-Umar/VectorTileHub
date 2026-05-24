# Cache Operations

VectorTileHub uses cache keys with:

```text
layerId + z + x + y + scopeKey + cacheVersion
```

Disk cache paths follow:

```text
{CacheRoot}/{LayerId}/{ScopeKey}/{CacheVersion}/{z}/{x}/{y}.pbf
```

## Admin Endpoints

```http
POST /vector-tile-hub/admin/layers/{layerId}/cache/generate
POST /vector-tile-hub/admin/layers/{layerId}/cache/delete
POST /vector-tile-hub/admin/layers/{layerId}/cache/invalidate
POST /vector-tile-hub/admin/layers/{layerId}/cache/notify-change
POST /vector-tile-hub/admin/layers/{layerId}/cache/swap
GET  /vector-tile-hub/admin/layers/{layerId}/cache/status
```

Admin endpoints require authorization.

## Cache Swap Policy

The swap job creates a new cache version and switches runtime settings immediately. The server then serves through the new cache while background generation fills it. Old cache deletion runs later so deleting many small files does not block the switch.
