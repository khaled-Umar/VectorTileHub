# Public API Contracts

## Tile Endpoint

```
GET {RoutePrefix}/tiles/{layerId:int}/{z:int}/{x:int}/{y:int}.pbf
```

**Query parameters**:

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| scope | string | no | Optional scope override (advanced scenarios only) |

**Responses**:

| Status | Content-Type | Body | Condition |
|--------|-------------|------|-----------|
| 200 | application/x-protobuf | MVT/PBF bytes | Tile generated or served from cache |
| 200 | application/x-protobuf | Empty MVT bytes | Layer exists but no features in tile/scope |
| 400 | application/json | `{"error": "..."}` | Invalid tile coordinates or zoom out of valid range |
| 401 | — | — | Authentication required but not provided |
| 403 | — | — | Authenticated but scope does not permit access |
| 404 | application/json | `{"error": "..."}` | Layer not found or disabled |
| 503 | application/json | `{"error": "..."}` | Data provider unreachable |

**Headers** (on 200):
- `Content-Type: application/x-protobuf`
- `Content-Encoding: gzip` (if compression enabled)
- `Cache-Control: public, max-age=3600` (configurable)
- `ETag: "{cacheVersion}-{z}-{x}-{y}"`

## Layer Metadata Endpoints

### List Layers

```
GET {RoutePrefix}/layers
```

**Response** (200):
```json
{
  "layers": [
    {
      "id": 97,
      "layerKey": "buildings",
      "layerName": "Jeddah Buildings",
      "minZoom": 12,
      "maxZoom": 21,
      "tileUrlTemplate": "/vector-tile-hub/tiles/97/{z}/{x}/{y}.pbf"
    }
  ]
}
```

### Get Layer

```
GET {RoutePrefix}/layers/{layerId:int}
```

**Response** (200): Single layer object (same shape as list item).

**Response** (404): `{"error": "Layer not found"}`

**Security note**: Metadata responses MUST NOT include connection
strings, provider secrets, security policy details, or internal
configuration.

## Health Check Endpoint

```
GET {HealthCheckPath}
```

**Response** (200 — healthy):
```json
{
  "status": "Healthy",
  "checks": {
    "settingsStore": "Healthy",
    "cacheFolder": "Healthy",
    "layerConfigFolder": "Healthy"
  }
}
```

**Response** (503 — unhealthy):
```json
{
  "status": "Unhealthy",
  "checks": {
    "settingsStore": "Unhealthy",
    "cacheFolder": "Healthy",
    "layerConfigFolder": "Healthy"
  }
}
```
