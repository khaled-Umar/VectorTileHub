# Admin API Contracts

All admin endpoints require authorization. The host configures which
roles have access via `VectorTileHubOptions.Hangfire.RequiredRoles`
or a dedicated admin policy.

Base path: `{RoutePrefix}/admin`

## Cache Generation

```
POST {RoutePrefix}/admin/layers/{layerId:int}/cache/generate
```

**Request body** (optional):
```json
{
  "minZoom": 12,
  "maxZoom": 18,
  "scopes": ["admin", "engineer", "public"]
}
```
If omitted, generates for the layer's full zoom range and all
configured scopes.

**Response** (202 Accepted):
```json
{
  "jobId": "abc-123",
  "layerId": 97,
  "status": "Enqueued",
  "message": "Cache generation job enqueued"
}
```

**Response** (404): Layer not found.
**Response** (401/403): Unauthorized.
**Response** (409): Generation already running for this layer.

## Cache Deletion

```
POST {RoutePrefix}/admin/layers/{layerId:int}/cache/delete
```

**Request body** (optional):
```json
{
  "cacheVersion": "v3",
  "deleteAllVersions": false
}
```

**Response** (202 Accepted):
```json
{
  "jobId": "def-456",
  "layerId": 97,
  "status": "Enqueued",
  "message": "Cache deletion job enqueued"
}
```

## Cache Invalidation

```
POST {RoutePrefix}/admin/layers/{layerId:int}/cache/invalidate
```

**Request body**:
```json
{
  "boundingBox": {
    "minX": 4150000.0,
    "minY": 2350000.0,
    "maxX": 4160000.0,
    "maxY": 2360000.0,
    "srid": 3857
  },
  "scopes": ["admin", "engineer"]
}
```
The system computes affected tile coordinates across all configured
zoom levels and invalidates those cache entries.

If `scopes` is omitted, invalidates for all scopes.

**Response** (200):
```json
{
  "layerId": 97,
  "tilesInvalidated": 156,
  "zoomLevelsAffected": [14, 15, 16, 17, 18]
}
```

## Data Change Notification

```
POST {RoutePrefix}/admin/layers/{layerId:int}/cache/notify-change
```

**Request body**:
```json
{
  "boundingBox": {
    "minX": 4150000.0,
    "minY": 2350000.0,
    "maxX": 4160000.0,
    "maxY": 2360000.0,
    "srid": 3857
  },
  "changeType": "Update",
  "regenerate": true
}
```

`changeType`: "Insert", "Update", "Delete", "ScopeChange"

If `regenerate` is true, affected tiles are invalidated AND
a background regeneration job is enqueued. Otherwise, tiles are
only invalidated and will be regenerated on next request.

**Response** (202 Accepted):
```json
{
  "layerId": 97,
  "tilesAffected": 42,
  "jobId": "ghi-789",
  "message": "Tiles invalidated and regeneration enqueued"
}
```

## Cache Version Swap

```
POST {RoutePrefix}/admin/layers/{layerId:int}/cache/swap
```

**Request body** (optional):
```json
{
  "newVersion": "v4",
  "regenerateAfterSwap": true,
  "deleteOldVersion": true
}
```

If `newVersion` is omitted, an auto-generated version name is used
(e.g., timestamp-based).

**Execution sequence**:
1. Create new cache folder for the new version
2. Atomically update `ActiveCacheVersion` in runtime settings
3. Begin serving through the new (initially empty) cache
4. If `regenerateAfterSwap`, enqueue background generation
5. If `deleteOldVersion`, enqueue background deletion of old folder

**Response** (202 Accepted):
```json
{
  "layerId": 97,
  "previousVersion": "v3",
  "newVersion": "v4",
  "status": "Swapped",
  "generationJobId": "jkl-012",
  "deletionJobId": "mno-345"
}
```

**Response** (409): Swap already in progress for this layer.

## Cache Status

```
GET {RoutePrefix}/admin/layers/{layerId:int}/cache/status
```

**Response** (200):
```json
{
  "layerId": 97,
  "activeCacheVersion": "v3",
  "generationStatus": "Idle",
  "generationJobId": null,
  "lastGenerationStartedAt": "2026-05-20T10:30:00Z",
  "lastGenerationCompletedAt": "2026-05-20T11:15:00Z",
  "lastInvalidatedAt": "2026-05-22T08:00:00Z",
  "diskUsageBytes": 1048576000
}
```

## Layer Configuration Reload

```
POST {RoutePrefix}/admin/layers/reload
```

Reloads all layer configurations from the `LayerConfigFolder`
without restarting the application.

**Response** (200):
```json
{
  "layersLoaded": 3,
  "layersEnabled": 2,
  "layersDisabled": 1,
  "errors": []
}
```

If individual layer files have errors:
```json
{
  "layersLoaded": 2,
  "layersEnabled": 2,
  "layersDisabled": 0,
  "errors": [
    {
      "file": "99-roads.json",
      "error": "Invalid JSON: unexpected token at line 12"
    }
  ]
}
```
