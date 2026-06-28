using System.Text;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.IO;
using Oracle.ManagedDataAccess.Client;

namespace K1Soft.IT.VectorTileHub.Providers.Oracle;

public sealed class OracleFeatureProvider : IVectorTileFeatureProvider
{
    private readonly IConfiguration _configuration;
    private readonly ICoordinateReprojector _reprojector;

    public OracleFeatureProvider(IConfiguration configuration, ICoordinateReprojector reprojector)
    {
        _configuration = configuration;
        _reprojector = reprojector;
    }

    public string ProviderType => "Oracle";

    public async Task<VectorTileFeatureBatch> GetFeaturesAsync(VectorTileFeatureQuery query, CancellationToken cancellationToken)
    {
        var layer = query.LayerConfig;
        var sourceCrs = CoordinateReferenceSystem.FromProvider(layer.Provider);
        await using var connection = new OracleConnection(ResolveConnectionString(layer.Provider));
        await connection.OpenAsync(cancellationToken);

        var sql = BuildSql(query, out var filterValues);
        await using var command = new OracleCommand(sql, connection) { BindByName = true };
        if (layer.Provider.CommandTimeoutSeconds is { } commandTimeout)
        {
            command.CommandTimeout = commandTimeout;
        }

        command.Parameters.Add(":srid", OracleDbType.Int32).Value = layer.Provider.SourceSrid;
        // The tile envelope arrives in Web Mercator; reproject it into the stored CRS for the spatial filter.
        var envelope = _reprojector.Reproject(query.Envelope, CoordinateReferenceSystem.WebMercator, sourceCrs);
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
                Geometry = _reprojector.Reproject(reader.Read(wkb), sourceCrs, CoordinateReferenceSystem.WebMercator),
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
}
