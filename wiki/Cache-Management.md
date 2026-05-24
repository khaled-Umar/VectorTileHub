# Cache Management

Cache entries are isolated by:

- layer
- zoom
- x/y tile coordinate
- security scope
- active cache version

The cache swap operation switches to a new cache version immediately. The old cache is deleted later so filesystem deletion does not block serving.

Use the admin endpoints under:

```text
/vector-tile-hub/admin/layers/{layerId}/cache
```
