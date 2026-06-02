using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace K1Soft.IT.VectorTileHub.Providers.SqlServer;

public sealed class SqlServerFeatureProvider : IVectorTileFeatureProvider
{
    private readonly IConfiguration _configuration;

    public SqlServerFeatureProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ProviderType => "SqlServer";

    public async Task<VectorTileFeatureBatch> GetFeaturesAsync(VectorTileFeatureQuery query, CancellationToken cancellationToken)
    {
        var layer = query.LayerConfig;
        var connectionString = ResolveConnectionString(layer.Provider);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = BuildSql(query, out var filterValues);
        await using var command = new SqlCommand(sql, connection);
        if (layer.Provider.CommandTimeoutSeconds is { } commandTimeout)
        {
            command.CommandTimeout = commandTimeout;
        }

        command.Parameters.Add("@envelope", SqlDbType.VarBinary).Value = BuildEnvelopeWkb(query.Envelope, layer.Provider.SourceSrid);
        command.Parameters.Add("@srid", SqlDbType.Int).Value = layer.Provider.SourceSrid;

        for (var i = 0; i < filterValues.Length; i++)
        {
            command.Parameters.Add($"@v{i}", SqlDbType.NVarChar).Value = filterValues[i];
        }

        var features = new List<VectorTileFeature>();
        var reader = new WKBReader();
        await using var dataReader = await command.ExecuteReaderAsync(cancellationToken);
        while (await dataReader.ReadAsync(cancellationToken))
        {
            if (dataReader["GeomWkb"] is not byte[] wkb || wkb.Length == 0)
            {
                continue;
            }

            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in layer.Attributes.Include)
            {
                attributes[attribute] = dataReader[attribute] is DBNull ? null : dataReader[attribute];
            }

            features.Add(new VectorTileFeature
            {
                Id = dataReader[layer.Provider.IdColumn],
                Geometry = ToServingGeometry(reader.Read(wkb), layer.Provider.SourceSrid),
                Attributes = attributes
            });
        }

        return new VectorTileFeatureBatch
        {
            Features = features,
            TotalCount = features.Count
        };
    }

    private string ResolveConnectionString(ProviderConfig provider)
    {
        if (!string.IsNullOrWhiteSpace(provider.ConnectionString))
        {
            return provider.ConnectionString;
        }

        if (!string.IsNullOrWhiteSpace(provider.ConnectionStringName))
        {
            return _configuration.GetConnectionString(provider.ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{provider.ConnectionStringName}' was not found.");
        }

        throw new InvalidOperationException("SQL Server provider requires a connection string or connection string name.");
    }

    private static string BuildSql(VectorTileFeatureQuery query, out string[] filterValues)
    {
        var layer = query.LayerConfig;
        var attributes = layer.Attributes.Include.Select(QuoteIdentifier).ToArray();
        var projection = attributes.Length == 0 ? "" : ", " + string.Join(", ", attributes);
        var table = ValidateTableName(layer.Provider.TableName);
        var idColumn = QuoteIdentifier(layer.Provider.IdColumn);
        var geometryColumn = QuoteIdentifier(layer.Provider.GeometryColumn);
        var sql = new StringBuilder();
        sql.Append($"SELECT {idColumn}, {geometryColumn}.STAsBinary() AS GeomWkb{projection} FROM {table} ");
        sql.Append($"WHERE {geometryColumn}.STIntersects(geometry::STGeomFromWKB(@envelope, @srid)) = 1");

        var predicate = VariantFilterSql.Build(query.Variant.Filter, QuoteIdentifier, i => $"@v{i}", out filterValues);
        if (predicate is not null)
        {
            sql.Append(" AND ");
            sql.Append(predicate);
        }

        return sql.ToString();
    }

    private static byte[] BuildEnvelopeWkb(Envelope envelope, int srid)
    {
        if (srid == 4326)
        {
            envelope = ToGeographicEnvelope(envelope);
        }

        var geometryFactory = new GeometryFactory();
        var coordinates = new[]
        {
            new Coordinate(envelope.MinX, envelope.MinY),
            new Coordinate(envelope.MaxX, envelope.MinY),
            new Coordinate(envelope.MaxX, envelope.MaxY),
            new Coordinate(envelope.MinX, envelope.MaxY),
            new Coordinate(envelope.MinX, envelope.MinY)
        };
        var polygon = geometryFactory.CreatePolygon(coordinates);
        return new WKBWriter().Write(polygon);
    }

    private static Envelope ToGeographicEnvelope(Envelope mercator)
    {
        var min = WebMercatorToLonLat(mercator.MinX, mercator.MinY);
        var max = WebMercatorToLonLat(mercator.MaxX, mercator.MaxY);
        return new Envelope(min.lon, max.lon, min.lat, max.lat);
    }

    private static (double lon, double lat) WebMercatorToLonLat(double x, double y)
    {
        const double originShift = 20037508.342789244;
        var lon = x / originShift * 180.0;
        var lat = y / originShift * 180.0;
        lat = 180.0 / Math.PI * (2.0 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
        return (lon, lat);
    }

    private static Geometry ToServingGeometry(Geometry geometry, int sourceSrid)
    {
        if (sourceSrid != 4326)
        {
            return geometry;
        }

        var copy = geometry.Copy();
        copy.Apply(new WebMercatorCoordinateFilter());
        copy.GeometryChanged();
        copy.SRID = 3857;
        return copy;
    }

    private sealed class WebMercatorCoordinateFilter : ICoordinateFilter
    {
        public void Filter(Coordinate coord)
        {
            const double originShift = 20037508.342789244;
            coord.X = coord.X * originShift / 180.0;
            var y = Math.Log(Math.Tan((90.0 + coord.Y) * Math.PI / 360.0)) / (Math.PI / 180.0);
            coord.Y = y * originShift / 180.0;
        }
    }

    private static string QuoteIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
        {
            throw new InvalidOperationException($"Unsafe SQL identifier: {value}");
        }

        return $"[{value}]";
    }

    private static string ValidateTableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Table name is required.");
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '_' and not '.' and not '[' and not ']')
            {
                throw new InvalidOperationException($"Unsafe table name: {value}");
            }
        }

        return value;
    }
}
