# Contract: Public Endpoints (Tile + Layer Metadata)

**Branch**: `002-vector-tile-server`

> The library provides these endpoint handlers. The **host** mounts and
> secures them (e.g., behind its own proxy/authorization). The library
> performs no authentication or authorization. All paths are relative to
> the configured `RoutePrefix` (default `/vector-tile-hub`).

## GET `{prefix}/tiles/{layerId}/{z}/{x}/{y}.pbf`

Return an MVT/PBF tile for a layer.

**Path params**
| Name | Type | Description |
|------|------|-------------|
| layerId | int | Configured layer id |
| z, x, y | int | XYZ tile coordinates |

**Query params**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| variant | string | no | Variant/filter key; omitted → layer default variant |

**Responses**
| Status | Content-Type | Body | When |
|--------|--------------|------|------|
| 200 | application/x-protobuf | MVT/PBF bytes | Tile served (cache, on-demand, or stale-while-revalidate) |
| 204 | — | empty | Empty tile (no features, or outside zoom range when configured as empty) |
| 404 | application/json | problem | Unknown `layerId`, or unknown `variant` ("variant not found") |

**Headers** (informational, set by library; host may strip/override)
- `X-VTH-From-Cache: true|false`
- `X-VTH-Stale: true|false` (stale tile served; background refresh enqueued)

**Behavior**
- Outside `[MinZoom, MaxZoom]`: empty tile (200 empty or 204), never an error.
- Cache miss with `AllowOnDemandGeneration=true`: generate, return, persist.
- Cache hit older than `RefreshPeriodMinutes`: serve stale + enqueue one
  background refresh (de-duplicated).
- Only whitelisted attributes are present in the tile.

## GET `{prefix}/layers`

List configured, enabled layers (metadata only — no connection strings).

**200** `application/json`:
```json
[
  { "id": 82, "layerKey": "layer_data_82", "name": "Local Plan NE",
    "minZoom": 0, "maxZoom": 21,
    "variants": ["default", "public", "internal"] }
]
```

## GET `{prefix}/layers/{layerId}`

Metadata for one layer (zoom range, layer key, available variant keys,
attribute whitelist). **404** if unknown.

## GET `{prefix}/health` (health indicator)

**200** when settings store reachable, cache root writable, layer-config
readable; otherwise **503** with the failing check. Database/provider
connectivity is the host's concern.
