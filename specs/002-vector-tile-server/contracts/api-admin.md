# Contract: Admin Endpoints (Cache Lifecycle + Config)

**Branch**: `002-vector-tile-server`

> These mutate cache/config and enqueue background jobs. The library applies
> **no built-in authorization** — the host MUST secure these (and the
> Hangfire dashboard) with its own policy. Paths are relative to `RoutePrefix`.

## POST `{prefix}/admin/layers/{layerId}/cache/generate`

Start (re)generating a layer's cache as a background job.

**Body** (optional)
```json
{ "variant": "public", "minZoom": 10, "maxZoom": 16 }
```
- `variant` omitted → all configured variants (or default).
- zoom range omitted → the layer's full configured range.

**202 Accepted**
```json
{ "jobId": "hangfire:job:123", "layerId": 82, "variant": "public", "status": "Running" }
```

## POST `{prefix}/admin/layers/{layerId}/cache/swap`

Blue/green replacement. Enqueues **two** jobs: (A) build a fresh cache into a
new empty version folder and flip the active version; (B) delete the previous
version folder afterward. Serving is never interrupted; partial tiles never
served.

**202 Accepted**
```json
{ "buildJobId": "...", "deleteJobId": "...", "newVersion": "v20260601T1200", "previousVersion": "v20260531T0900" }
```

## DELETE `{prefix}/admin/layers/{layerId}/cache`

Delete a layer's cache (optionally a single `variant` via query). Runs as a
background deletion job.

**202 Accepted** `{ "jobId": "...", "layerId": 82, "variant": "public" }`

## POST `{prefix}/admin/layers/{layerId}/cache/notify`

Refresh tiles intersecting a bounding box. The system computes affected tiles
across the layer's zoom range and refreshes them as a background job.

**Body**
```json
{ "variant": "default", "bbox": [minX, minY, maxX, maxY], "srid": 3857,
  "minZoom": 0, "maxZoom": 21 }
```
**202 Accepted** `{ "jobId": "...", "affectedTileEstimate": 1284 }`

## GET `{prefix}/admin/layers/{layerId}/cache/status`

Per-(layer, variant) runtime state.

**200**
```json
[
  { "variant": "default", "status": "Idle", "activeCacheVersion": "v...",
    "lastGenerationCompletedAt": "2026-06-01T10:00:00Z",
    "lastInvalidatedAt": null }
]
```

## POST `{prefix}/admin/config/reload`

Explicitly reload layer configuration files (no file watching). Reports
loaded/changed/failed layers.

**200**
```json
{ "loaded": [82, 91], "added": [91], "removed": [], "failed": [
  { "path": "C:/cfg/bad.json", "error": "Missing Provider.ConnectionString" } ] }
```

## Background Job Dashboard

Mounted at `HangfireOptions.DashboardPath` (default `/vector-tile-hub/jobs`).
Authorization is **host-supplied** at mount time
(`IDashboardAuthorizationFilter` / delegate). Jobs are tagged per
layer+variant and expose Running/Succeeded/Failed state; failed cache jobs
retain already-written tiles and support retry.

## Error Shape (all admin + public errors)

RFC 7807 `application/problem+json`:
```json
{ "type": "about:blank", "title": "Layer not found", "status": 404,
  "detail": "No layer with id 999", "instance": "/vector-tile-hub/tiles/999/..." }
```
