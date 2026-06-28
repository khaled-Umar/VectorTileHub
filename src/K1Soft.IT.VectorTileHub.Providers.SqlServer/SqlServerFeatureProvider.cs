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
    private readonly ICoordinateReprojector _reprojector;

    public SqlServerFeatureProvider(IConfiguration configuration, ICoordinateReprojector reprojector)
    {
        _configuration = configuration;
        _reprojector = reprojector;
    }

    public string ProviderType => "SqlServer";

    public async Task<VectorTileFeatureBatch> GetFeaturesAsync(VectorTileFeatureQuery query, CancellationToken cancellationToken)
    {
        var layer = query.LayerConfig;
        var sourceCrs = CoordinateReferenceSystem.FromProvider(layer.Provider);
        var connectionString = ResolveConnectionString(layer.Provider);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = BuildSql(query, out var filterValues);
        await using var command = new SqlCommand(sql, connection);
        if (layer.Provider.CommandTimeoutSeconds is { } commandTimeout)
        {
            command.CommandTimeout = commandTimeout;
        }

        // The tile envelope arrives in Web Mercator; reproject it into the stored CRS for the spatial filter.
        var filterEnvelope = _reprojector.Reproject(query.Envelope, CoordinateReferenceSystem.WebMercator, sourceCrs);
        command.Parameters.Add("@envelope", SqlDbType.VarBinary).Value = BuildEnvelopeWkb(filterEnvelope);
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
                Geometry = _reprojector.Reproject(reader.Read(wkb), sourceCrs, CoordinateReferenceSystem.WebMercator),
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

    private static byte[] BuildEnvelopeWkb(Envelope envelope)
    {
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
