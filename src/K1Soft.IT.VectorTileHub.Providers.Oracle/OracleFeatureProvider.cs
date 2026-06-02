using System.Text;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.IO;
using Oracle.ManagedDataAccess.Client;

namespace K1Soft.IT.VectorTileHub.Providers.Oracle;

public sealed class OracleFeatureProvider : IVectorTileFeatureProvider
{
    private readonly IConfiguration _configuration;

    public OracleFeatureProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ProviderType => "Oracle";

    public async Task<VectorTileFeatureBatch> GetFeaturesAsync(VectorTileFeatureQuery query, CancellationToken cancellationToken)
    {
        var layer = query.LayerConfig;
        await using var connection = new OracleConnection(ResolveConnectionString(layer.Provider));
        await connection.OpenAsync(cancellationToken);

        var sql = BuildSql(query, out var filterValues);
        await using var command = new OracleCommand(sql, connection) { BindByName = true };
        if (layer.Provider.CommandTimeoutSeconds is { } commandTimeout)
        {
            command.CommandTimeout = commandTimeout;
        }

        command.Parameters.Add(":srid", OracleDbType.Int32).Value = layer.Provider.SourceSrid;
        var envelope = layer.Provider.SourceSrid == 4326 ? ToGeographicEnvelope(query.Envelope) : query.Envelope;
        command.Parameters.Add(":minx", OracleDbType.Double).Value = envelope.MinX;
        command.Parameters.Add(":miny", OracleDbType.Double).Value = envelope.MinY;
        command.Parameters.Add(":maxx", OracleDbType.Double).Value = envelope.MaxX;
        command.Parameters.Add(":maxy", OracleDbType.Double).Value = envelope.MaxY;

        for (var i = 0; i < filterValues.Length; i++)
        {
            command.Parameters.Add($":v{i}", OracleDbType.Varchar2).Value = filterValues[i];
        }

        var features = new List<VectorTileFeature>();
        var reader = new WKBReader();
        await using var dataReader = await command.ExecuteReaderAsync(cancellationToken);
        while (await dataReader.ReadAsync(cancellationToken))
        {
            if (dataReader["GEOM_WKB"] is not byte[] wkb || wkb.Length == 0)
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

        return new VectorTileFeatureBatch { Features = features, TotalCount = features.Count };
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

        throw new InvalidOperationException("Oracle provider requires a connection string or connection string name.");
    }

    private static string BuildSql(VectorTileFeatureQuery query, out string[] filterValues)
    {
        var layer = query.LayerConfig;
        var table = ValidateTableName(layer.Provider.TableName);
        var idColumn = QuoteIdentifier(layer.Provider.IdColumn);
        var geometryColumn = QuoteIdentifier(layer.Provider.GeometryColumn);
        var attributes = layer.Attributes.Include.Select(QuoteIdentifier).ToArray();
        var projection = attributes.Length == 0 ? "" : ", " + string.Join(", ", attributes);

        var sql = new StringBuilder();
        sql.Append($"SELECT {idColumn}, SDO_UTIL.TO_WKBGEOMETRY({geometryColumn}) AS GEOM_WKB{projection} FROM {table} ");
        sql.Append($"WHERE SDO_RELATE({geometryColumn}, SDO_GEOMETRY(2003, :srid, NULL, SDO_ELEM_INFO_ARRAY(1, 1003, 3), SDO_ORDINATE_ARRAY(:minx, :miny, :maxx, :maxy)), 'mask=ANYINTERACT') = 'TRUE'");

        var predicate = VariantFilterSql.Build(query.Variant.Filter, QuoteIdentifier, i => $":v{i}", out filterValues);
        if (predicate is not null)
        {
            sql.Append(" AND ");
            sql.Append(predicate);
        }

        return sql.ToString();
    }

    private static string QuoteIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
        {
            throw new InvalidOperationException($"Unsafe SQL identifier: {value}");
        }

        return $"\"{value}\"";
    }

    private static string ValidateTableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Table name is required.");
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '_' and not '.' and not '"')
            {
                throw new InvalidOperationException($"Unsafe table name: {value}");
            }
        }

        return value;
    }

    private static NetTopologySuite.Geometries.Envelope ToGeographicEnvelope(NetTopologySuite.Geometries.Envelope mercator)
    {
        var min = WebMercatorToLonLat(mercator.MinX, mercator.MinY);
        var max = WebMercatorToLonLat(mercator.MaxX, mercator.MaxY);
        return new NetTopologySuite.Geometries.Envelope(min.lon, max.lon, min.lat, max.lat);
    }

    private static (double lon, double lat) WebMercatorToLonLat(double x, double y)
    {
        const double originShift = 20037508.342789244;
        var lon = x / originShift * 180.0;
        var lat = y / originShift * 180.0;
        lat = 180.0 / Math.PI * (2.0 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
        return (lon, lat);
    }

    private static NetTopologySuite.Geometries.Geometry ToServingGeometry(NetTopologySuite.Geometries.Geometry geometry, int sourceSrid)
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

    private sealed class WebMercatorCoordinateFilter : NetTopologySuite.Geometries.ICoordinateFilter
    {
        public void Filter(NetTopologySuite.Geometries.Coordinate coord)
        {
            const double originShift = 20037508.342789244;
            coord.X = coord.X * originShift / 180.0;
            var y = Math.Log(Math.Tan((90.0 + coord.Y) * Math.PI / 360.0)) / (Math.PI / 180.0);
            coord.Y = y * originShift / 180.0;
        }
    }
}
