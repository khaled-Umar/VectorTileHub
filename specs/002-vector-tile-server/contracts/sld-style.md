# Contract: SLD → OpenLayers Style (Sample Project Only)

**Branch**: `002-vector-tile-server`
**Scope**: Sample project only. The library emits **only PBF** and is not
involved in styling/rendering.

## Goal

Generate, from `tmp/layerStyle.sld`, a render style that the sample's
OpenLayers page applies so served PBF tiles render with the **same symbology**
as the SLD. Output format: **Mapbox GL JSON**, applied to OpenLayers via
`ol-mapbox-style`.

## Source SLD shape (observed)

- 39 `se:Rule` elements; 38 `se:PolygonSymbolizer` rules keyed on the
  `Type_t` attribute (some via `ogc:Or` of multiple `ogc:Literal` values).
- Each polygon rule: `se:Fill` (`fill`), `se:Stroke` (`stroke`,
  `stroke-width`, `stroke-linejoin`).
- 1 `se:TextSymbolizer` rule labelling `PARCELNUMBER` / `SERVICE_NAME`,
  gated by `se:MaxScaleDenominator` = 2500, font Open Sans 13, fill `#323232`.

## Mapping rules

| SLD construct | Mapbox GL output |
|---------------|------------------|
| Rule with `PropertyIsEqualTo(Type_t = X)` | one `fill` layer, `filter: ["==", ["get","Type_t"], "X"]` |
| Rule with `ogc:Or` of equals | `filter: ["in", ["get","Type_t"], ["literal", [v1, v2, ...]]]` |
| `se:Fill` `fill` | `paint.fill-color` |
| `se:Stroke` `stroke` / `stroke-width` | companion `line` layer: `paint.line-color`, `paint.line-width` |
| `se:Stroke` `stroke-linejoin` | `layout.line-join` (e.g. `bevel`) |
| `se:TextSymbolizer` label + `MaxScaleDenominator` | `symbol` layer; `text-field` from properties; `minzoom` from scale→zoom |
| `se:Font` family/size | `layout.text-font`, `text-size` |
| `se:Fill` (label) | `paint.text-color` |

**Scale → zoom**: convert `MaxScaleDenominator` to a Web-Mercator zoom
threshold (label visible only when zoomed in past it), e.g. scale 2500 ≈
high zoom; emit as the symbol layer's `minzoom`.

## Generated style skeleton (`wwwroot/ol-style.json`)

```json
{
  "version": 8,
  "sources": {
    "vth": { "type": "vector", "tiles": ["/vector-tile-hub/tiles/82/{z}/{x}/{y}.pbf"], "minzoom": 0, "maxzoom": 21 }
  },
  "layers": [
    { "id": "villa-fill", "type": "fill", "source": "vth", "source-layer": "layer_data_82",
      "filter": ["==", ["get", "Type_t"], "Villa"],
      "paint": { "fill-color": "#ffff00" } },
    { "id": "villa-line", "type": "line", "source": "vth", "source-layer": "layer_data_82",
      "filter": ["==", ["get", "Type_t"], "Villa"],
      "layout": { "line-join": "bevel" },
      "paint": { "line-color": "#9c9c9c", "line-width": 0.05 } }
    /* ... one fill + line pair per SLD rule (38 rules) ... */,
    { "id": "parcel-label", "type": "symbol", "source": "vth", "source-layer": "layer_data_82",
      "minzoom": 17,
      "layout": { "text-field": ["coalesce", ["get","PARCELNUMBER"], ["get","SERVICE_NAME"]],
                  "text-font": ["Open Sans"], "text-size": 13 },
      "paint": { "text-color": "#323232" } }
  ]
}
```

## Acceptance (maps to spec US7 / FR-039 / FR-040 / SC-010)

- Every SLD rule has a corresponding GL layer (fill + line) with matching
  fill color, stroke color, stroke width, and line-join.
- `ogc:Or` rules collapse to a single `in` filter covering all literals.
- The parcel label appears only within the equivalent scale (via `minzoom`).
- The sample's OpenLayers page loads `/vector-tile-hub/tiles/...` and renders
  using this style.

## Converter

`src/K1Soft.IT.VectorTileHub.Sample/Tools/SldToStyleConverter.cs` parses the
SLD (XML) and writes `wwwroot/ol-style.json`. It is a sample utility (build
step or one-shot), not part of the shipped library packages. `source-layer`
matches the layer's `LayerKey`.
