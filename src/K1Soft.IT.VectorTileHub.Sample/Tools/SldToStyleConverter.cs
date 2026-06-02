using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace K1Soft.IT.VectorTileHub.Sample.Tools;

/// <summary>
/// Converts an OGC SLD/SE style document into a Mapbox GL style JSON (consumed by
/// OpenLayers via ol-mapbox-style). One fill + line layer is emitted per polygon rule
/// (keyed on the rule's property filter); text rules become a symbol layer with a
/// minzoom derived from the rule's MaxScaleDenominator. This lives in the sample only —
/// the VectorTileHub library performs no rendering.
/// </summary>
public static class SldToStyleConverter
{
    private static readonly XNamespace Se = "http://www.opengis.net/se";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";

    public static string Convert(string sldPath, string sourceLayer, string tileUrlTemplate, int minZoom = 0, int maxZoom = 21)
    {
        var doc = XDocument.Load(sldPath);

        var source = new JsonObject
        {
            ["type"] = "vector",
            ["tiles"] = new JsonArray(tileUrlTemplate),
            ["minzoom"] = minZoom,
            ["maxzoom"] = maxZoom
        };

        var layers = new JsonArray();
        var index = 0;

        foreach (var rule in doc.Descendants(Se + "Rule"))
        {
            index++;
            var property = ExtractFilterProperty(rule, out var values);
            var filter = BuildFilter(property, values);

            foreach (var polygon in rule.Elements(Se + "PolygonSymbolizer"))
            {
                var fill = SvgParam(polygon.Element(Se + "Fill"), "fill");
                var stroke = SvgParam(polygon.Element(Se + "Stroke"), "stroke");
                var strokeWidth = SvgParam(polygon.Element(Se + "Stroke"), "stroke-width");
                var strokeJoin = SvgParam(polygon.Element(Se + "Stroke"), "stroke-linejoin");

                if (fill is not null)
                {
                    var fillLayer = new JsonObject
                    {
                        ["id"] = $"rule-{index}-fill",
                        ["type"] = "fill",
                        ["source"] = "vth",
                        ["source-layer"] = sourceLayer,
                        ["paint"] = new JsonObject { ["fill-color"] = fill }
                    };
                    if (filter is not null) fillLayer["filter"] = filter.DeepClone();
                    layers.Add(fillLayer);
                }

                if (stroke is not null)
                {
                    var lineLayout = new JsonObject();
                    if (strokeJoin is not null) lineLayout["line-join"] = strokeJoin;
                    var linePaint = new JsonObject { ["line-color"] = stroke };
                    if (strokeWidth is not null && double.TryParse(strokeWidth, NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                    {
                        linePaint["line-width"] = w;
                    }

                    var lineLayer = new JsonObject
                    {
                        ["id"] = $"rule-{index}-line",
                        ["type"] = "line",
                        ["source"] = "vth",
                        ["source-layer"] = sourceLayer,
                        ["layout"] = lineLayout,
                        ["paint"] = linePaint
                    };
                    if (filter is not null) lineLayer["filter"] = filter.DeepClone();
                    layers.Add(lineLayer);
                }
            }

            foreach (var text in rule.Elements(Se + "TextSymbolizer"))
            {
                var labelProps = text.Element(Se + "Label")?.Elements(Ogc + "PropertyName").Select(p => p.Value.Trim()).ToArray() ?? [];
                var fontFamily = SvgParam(text.Element(Se + "Font"), "font-family") ?? "Open Sans";
                var fontSizeStr = SvgParam(text.Element(Se + "Font"), "font-size");
                var textColor = SvgParam(text.Element(Se + "Fill"), "fill") ?? "#323232";
                var maxScale = (double?)rule.Element(Se + "MaxScaleDenominator");

                var layout = new JsonObject
                {
                    ["text-field"] = BuildTextField(labelProps),
                    ["text-font"] = new JsonArray(fontFamily)
                };
                if (fontSizeStr is not null && double.TryParse(fontSizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs))
                {
                    layout["text-size"] = fs;
                }

                var symbol = new JsonObject
                {
                    ["id"] = $"rule-{index}-label",
                    ["type"] = "symbol",
                    ["source"] = "vth",
                    ["source-layer"] = sourceLayer,
                    ["layout"] = layout,
                    ["paint"] = new JsonObject { ["text-color"] = textColor }
                };
                if (maxScale is > 0)
                {
                    symbol["minzoom"] = ScaleToZoom(maxScale.Value);
                }
                if (filter is not null) symbol["filter"] = filter.DeepClone();
                layers.Add(symbol);
            }
        }

        var style = new JsonObject
        {
            ["version"] = 8,
            ["name"] = "VectorTileHub SLD-derived style",
            ["sources"] = new JsonObject { ["vth"] = source },
            ["layers"] = layers
        };

        return style.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? ExtractFilterProperty(XElement rule, out string[] values)
    {
        values = [];
        var filter = rule.Element(Ogc + "Filter");
        if (filter is null)
        {
            return null;
        }

        var equals = filter.Descendants(Ogc + "PropertyIsEqualTo").ToArray();
        if (equals.Length == 0)
        {
            return null;
        }

        var property = equals[0].Element(Ogc + "PropertyName")?.Value.Trim();
        values = equals
            .Select(e => e.Element(Ogc + "Literal")?.Value ?? "")
            .Where(v => v.Length > 0)
            .ToArray();
        return property;
    }

    private static JsonArray? BuildFilter(string? property, string[] values)
    {
        if (string.IsNullOrWhiteSpace(property) || values.Length == 0)
        {
            return null;
        }

        if (values.Length == 1)
        {
            return new JsonArray("==", new JsonArray("get", property), values[0]);
        }

        var literal = new JsonArray();
        foreach (var v in values)
        {
            literal.Add(v);
        }

        return new JsonArray("in", new JsonArray("get", property), new JsonArray("literal", literal));
    }

    private static JsonArray BuildTextField(string[] properties)
    {
        // ["coalesce", ["get", prop1], ["get", prop2], ...]
        var expr = new JsonArray("coalesce");
        if (properties.Length == 0)
        {
            expr.Add("");
            return expr;
        }

        foreach (var p in properties)
        {
            expr.Add(new JsonArray("get", p));
        }

        return expr;
    }

    private static string? SvgParam(XElement? container, string name)
    {
        return container?
            .Elements(Se + "SvgParameter")
            .FirstOrDefault(e => (string?)e.Attribute("name") == name)?
            .Value.Trim();
    }

    private static int ScaleToZoom(double scaleDenominator)
    {
        // Web-Mercator: zoom ≈ log2(559082264.029 / scaleDenominator). The label is
        // visible at scales finer than MaxScaleDenominator, i.e. at/above this zoom.
        const double z0Scale = 559082264.029;
        var zoom = Math.Log2(z0Scale / scaleDenominator);
        return Math.Clamp((int)Math.Floor(zoom), 0, 24);
    }
}
