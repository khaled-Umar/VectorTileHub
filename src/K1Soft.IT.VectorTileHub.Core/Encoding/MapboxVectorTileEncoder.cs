using System.Globalization;
using System.Text;
using NetTopologySuite.Geometries;

namespace K1Soft.IT.VectorTileHub;

public sealed class MapboxVectorTileEncoder : IVectorTileEncoder
{
    public byte[] Encode(string mvtLayerName, IReadOnlyList<VectorTileFeature> features, VectorTileEncodingContext context)
    {
        var layer = new MvtLayerBuilder(mvtLayerName, context.Extent);

        foreach (var feature in features)
        {
            var geometry = EncodeGeometry(feature.Geometry, context);
            if (geometry.Count == 0)
            {
                continue;
            }

            var type = GetGeometryType(feature.Geometry);
            if (type == 0)
            {
                continue;
            }

            layer.AddFeature(feature.Id, type, feature.Attributes, geometry);
        }

        return WriteTile(layer);
    }

    public byte[] EncodeEmpty(string mvtLayerName, VectorTileEncodingContext context)
    {
        return WriteTile(new MvtLayerBuilder(mvtLayerName, context.Extent));
    }

    private static byte[] WriteTile(MvtLayerBuilder layer)
    {
        using var stream = new MemoryStream();
        var layerBytes = layer.ToBytes();
        WriteTag(stream, 3, 2);
        WriteVarint(stream, (ulong)layerBytes.Length);
        stream.Write(layerBytes);
        return stream.ToArray();
    }

    private static int GetGeometryType(Geometry geometry)
    {
        return geometry switch
        {
            Point or MultiPoint => 1,
            LineString or MultiLineString => 2,
            Polygon or MultiPolygon => 3,
            GeometryCollection collection when collection.NumGeometries > 0 => GetGeometryType(collection.GetGeometryN(0)),
            _ => 0
        };
    }

    private static List<uint> EncodeGeometry(Geometry geometry, VectorTileEncodingContext context)
    {
        var commands = new List<uint>();
        var cursor = new TilePoint(0, 0);

        switch (geometry)
        {
            case Point point:
                EncodePoint(point, context, commands, ref cursor);
                break;
            case MultiPoint multiPoint:
                for (var i = 0; i < multiPoint.NumGeometries; i++)
                {
                    EncodePoint((Point)multiPoint.GetGeometryN(i), context, commands, ref cursor);
                }
                break;
            case LineString line:
                EncodeLine(line, context, commands, ref cursor);
                break;
            case MultiLineString multiLine:
                for (var i = 0; i < multiLine.NumGeometries; i++)
                {
                    EncodeLine((LineString)multiLine.GetGeometryN(i), context, commands, ref cursor);
                }
                break;
            case Polygon polygon:
                EncodePolygon(polygon, context, commands, ref cursor);
                break;
            case MultiPolygon multiPolygon:
                for (var i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    EncodePolygon((Polygon)multiPolygon.GetGeometryN(i), context, commands, ref cursor);
                }
                break;
            case GeometryCollection collection:
                for (var i = 0; i < collection.NumGeometries; i++)
                {
                    commands.AddRange(EncodeGeometry(collection.GetGeometryN(i), context));
                }
                break;
        }

        return commands;
    }

    private static void EncodePoint(Point point, VectorTileEncodingContext context, List<uint> commands, ref TilePoint cursor)
    {
        if (point.IsEmpty)
        {
            return;
        }

        var tilePoint = ToTilePoint(point.Coordinate, context);
        commands.Add(Command(1, 1));
        WriteDelta(commands, tilePoint, ref cursor);
    }

    private static void EncodeLine(LineString line, VectorTileEncodingContext context, List<uint> commands, ref TilePoint cursor)
    {
        var points = line.Coordinates.Select(c => ToTilePoint(c, context)).DistinctConsecutive().ToArray();
        if (points.Length < 2)
        {
            return;
        }

        commands.Add(Command(1, 1));
        WriteDelta(commands, points[0], ref cursor);
        commands.Add(Command(2, (uint)(points.Length - 1)));
        for (var i = 1; i < points.Length; i++)
        {
            WriteDelta(commands, points[i], ref cursor);
        }
    }

    private static void EncodePolygon(Polygon polygon, VectorTileEncodingContext context, List<uint> commands, ref TilePoint cursor)
    {
        EncodeRing(polygon.ExteriorRing, context, commands, ref cursor);
        for (var i = 0; i < polygon.NumInteriorRings; i++)
        {
            EncodeRing(polygon.GetInteriorRingN(i), context, commands, ref cursor);
        }
    }

    private static void EncodeRing(LineString ring, VectorTileEncodingContext context, List<uint> commands, ref TilePoint cursor)
    {
        var points = ring.Coordinates.Select(c => ToTilePoint(c, context)).DistinctConsecutive().ToList();
        if (points.Count > 1 && points[0] == points[^1])
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3)
        {
            return;
        }

        commands.Add(Command(1, 1));
        WriteDelta(commands, points[0], ref cursor);
        commands.Add(Command(2, (uint)(points.Count - 1)));
        for (var i = 1; i < points.Count; i++)
        {
            WriteDelta(commands, points[i], ref cursor);
        }

        commands.Add(Command(7, 1));
    }

    private static TilePoint ToTilePoint(Coordinate coordinate, VectorTileEncodingContext context)
    {
        var env = context.TileEnvelope;
        var x = (int)Math.Round((coordinate.X - env.MinX) / env.Width * context.Extent);
        var y = (int)Math.Round((env.MaxY - coordinate.Y) / env.Height * context.Extent);
        return new TilePoint(x, y);
    }

    private static uint Command(uint id, uint count) => (count << 3) | id;

    private static void WriteDelta(List<uint> commands, TilePoint point, ref TilePoint cursor)
    {
        commands.Add(ZigZag(point.X - cursor.X));
        commands.Add(ZigZag(point.Y - cursor.Y));
        cursor = point;
    }

    private static uint ZigZag(int value) => (uint)((value << 1) ^ (value >> 31));

    private readonly record struct TilePoint(int X, int Y);

    private sealed class MvtLayerBuilder
    {
        private readonly string _name;
        private readonly int _extent;
        private readonly List<byte[]> _features = [];
        private readonly List<string> _keys = [];
        private readonly List<MvtValue> _values = [];
        private readonly Dictionary<string, int> _keyIndex = new(StringComparer.Ordinal);
        private readonly Dictionary<MvtValue, int> _valueIndex = new();

        public MvtLayerBuilder(string name, int extent)
        {
            _name = name;
            _extent = extent;
        }

        public void AddFeature(object id, int type, IReadOnlyDictionary<string, object?> attributes, IReadOnlyList<uint> geometry)
        {
            using var stream = new MemoryStream();

            if (TryConvertId(id, out var featureId))
            {
                WriteVarintField(stream, 1, featureId);
            }

            var tags = BuildTags(attributes);
            if (tags.Count > 0)
            {
                WritePackedVarints(stream, 2, tags);
            }

            WriteVarintField(stream, 3, (ulong)type);
            WritePackedVarints(stream, 4, geometry);
            _features.Add(stream.ToArray());
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            WriteString(stream, 1, _name);

            foreach (var feature in _features)
            {
                WriteTag(stream, 2, 2);
                WriteVarint(stream, (ulong)feature.Length);
                stream.Write(feature);
            }

            foreach (var key in _keys)
            {
                WriteString(stream, 3, key);
            }

            foreach (var value in _values)
            {
                var bytes = value.ToBytes();
                WriteTag(stream, 4, 2);
                WriteVarint(stream, (ulong)bytes.Length);
                stream.Write(bytes);
            }

            WriteVarintField(stream, 5, (ulong)_extent);
            WriteVarintField(stream, 15, 2);
            return stream.ToArray();
        }

        private List<uint> BuildTags(IReadOnlyDictionary<string, object?> attributes)
        {
            var tags = new List<uint>();
            foreach (var (key, value) in attributes)
            {
                if (value is null or DBNull)
                {
                    continue;
                }

                tags.Add((uint)GetKeyIndex(key));
                tags.Add((uint)GetValueIndex(MvtValue.From(value)));
            }

            return tags;
        }

        private int GetKeyIndex(string key)
        {
            if (_keyIndex.TryGetValue(key, out var index))
            {
                return index;
            }

            index = _keys.Count;
            _keys.Add(key);
            _keyIndex[key] = index;
            return index;
        }

        private int GetValueIndex(MvtValue value)
        {
            if (_valueIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _values.Count;
            _values.Add(value);
            _valueIndex[value] = index;
            return index;
        }

        private static bool TryConvertId(object id, out ulong value)
        {
            try
            {
                value = Convert.ToUInt64(id, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }
    }

    private readonly record struct MvtValue(object Value, MvtValueKind Kind)
    {
        public static MvtValue From(object value)
        {
            return value switch
            {
                bool b => new MvtValue(b, MvtValueKind.Bool),
                byte or sbyte or short or int or long => new MvtValue(Convert.ToInt64(value, CultureInfo.InvariantCulture), MvtValueKind.Int),
                ushort or uint or ulong => new MvtValue(Convert.ToUInt64(value, CultureInfo.InvariantCulture), MvtValueKind.UInt),
                float or double or decimal => new MvtValue(Convert.ToDouble(value, CultureInfo.InvariantCulture), MvtValueKind.Double),
                DateTime dt => new MvtValue(dt.ToString("O", CultureInfo.InvariantCulture), MvtValueKind.String),
                DateTimeOffset dto => new MvtValue(dto.ToString("O", CultureInfo.InvariantCulture), MvtValueKind.String),
                _ => new MvtValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", MvtValueKind.String)
            };
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            switch (Kind)
            {
                case MvtValueKind.String:
                    WriteString(stream, 1, (string)Value);
                    break;
                case MvtValueKind.Double:
                    WriteTag(stream, 3, 1);
                    var bytes = BitConverter.GetBytes((double)Value);
                    stream.Write(bytes);
                    break;
                case MvtValueKind.Int:
                    WriteVarintField(stream, 4, (ulong)(long)Value);
                    break;
                case MvtValueKind.UInt:
                    WriteVarintField(stream, 5, (ulong)Value);
                    break;
                case MvtValueKind.Bool:
                    WriteVarintField(stream, 7, (bool)Value ? 1UL : 0UL);
                    break;
            }

            return stream.ToArray();
        }
    }

    private enum MvtValueKind
    {
        String,
        Double,
        Int,
        UInt,
        Bool
    }

    private static void WritePackedVarints(Stream stream, int field, IEnumerable<uint> values)
    {
        using var packed = new MemoryStream();
        foreach (var value in values)
        {
            WriteVarint(packed, value);
        }

        var bytes = packed.ToArray();
        WriteTag(stream, field, 2);
        WriteVarint(stream, (ulong)bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteString(Stream stream, int field, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteTag(stream, field, 2);
        WriteVarint(stream, (ulong)bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarintField(Stream stream, int field, ulong value)
    {
        WriteTag(stream, field, 0);
        WriteVarint(stream, value);
    }

    private static void WriteTag(Stream stream, int field, int wireType) => WriteVarint(stream, (ulong)((field << 3) | wireType));

    private static void WriteVarint(Stream stream, uint value) => WriteVarint(stream, (ulong)value);

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value > 127)
        {
            stream.WriteByte((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}

file static class TilePointEnumerableExtensions
{
    public static IEnumerable<T> DistinctConsecutive<T>(this IEnumerable<T> source)
        where T : IEquatable<T>
    {
        var hasPrevious = false;
        var previous = default(T);
        foreach (var item in source)
        {
            if (hasPrevious && item.Equals(previous))
            {
                continue;
            }

            yield return item;
            previous = item;
            hasPrevious = true;
        }
    }
}
