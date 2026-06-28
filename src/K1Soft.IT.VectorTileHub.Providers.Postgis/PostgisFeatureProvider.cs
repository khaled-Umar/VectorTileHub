using System.Text;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;

namespace K1Soft.IT.VectorTileHub.Providers.Postgis;

/// <summary>
/// PostGIS feature provider. Queries a PostgreSQL/PostGIS table, intersecting the requested tile
/// envelope against the geometry column and returning each feature's geometry as OGC WKB plus the
/// configured attributes. Mirrors the SqlServer/Oracle providers: the envelope is reprojected to the
/// source SRID for the spatial filter and the served geometry is reprojected back to Web Mercator for
/// tiling, via <see cref="ICoordinateReprojector"/> (any source CRS, including custom WKT projections).
/// </summary>
public sealed class PostgisFeatureProvider : IVectorTileFeatureProvider
{
    private readonly IConfiguration _configuration;
    private readonly ICoordinateReprojector _reprojector;

    public PostgisFeatureProvider(IConfiguration configuration, ICoordinateReprojector reprojector)
    {
        _configuration = configuration;
        _reprojector = reprojector;
    }

    public string ProviderType => "Postgis";

    public async Task<VectorTileFeatureBatch> GetFeaturesAsync(VectorTileFeatureQuery query, CancellationToken cancellationToken)
    {
        var layer = query.LayerConfig;
        var sourceCrs = CoordinateReferenceSystem.FromProvider(layer.Provider);
        await using var connection = new NpgsqlConnection(ResolveConnectionString(layer.Provider));
        await connection.OpenAsync(cancellationToken);

        var sql = BuildSql(query, out var filterValues);
        await using var command = new NpgsqlCommand(sql, connection);
        if (layer.Provider.CommandTimeoutSeconds is { } commandTimeout)
        {
            command.CommandTimeout = commandTimeout;
        }

        // The stored geometry is in SourceSrid; reproject the Web Mercator tile envelope into that SRID.
        var envelope = _reprojector.Reproject(query.Envelope, CoordinateReferenceSystem.WebMercator, sourceCrs);
        command.Parameters.Add(new NpgsqlParameter("minx", NpgsqlDbType.Double) { Value = envelope.MinX });
        command.Parameters.Add(new NpgsqlParameter("miny", NpgsqlDbType.Double) { Value = envelope.MinY });
        command.Parameters.Add(new NpgsqlParameter("maxx", NpgsqlDbType.Double) { Value = envelope.MaxX });
        command.Parameters.Add(new NpgsqlParameter("maxy", NpgsqlDbType.Double) { Value = envelope.MaxY });
        command.Parameters.Add(new NpgsqlParameter("srid", NpgsqlDbType.Integer) { Value = layer.Provider.SourceSrid });

        for (var i = 0; i < filterValues.Length; i++)
        {
            command.Parameters.Add(new NpgsqlParameter($"v{i}", NpgsqlDbType.Text) { Value = filterValues[i] });
        }

        var features = new List<VectorTileFeature>();
        var reader = new WKBReader();
        await using var dataReader = await command.ExecuteReaderAsync(cancellationToken);
        while (await dataReader.ReadAsync(cancellationToken))
        {
            if (dataReader["geom_wkb"] is not byte[] wkb || wkb.Length == 0)
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

        throw new InvalidOperationException("PostGIS provider requires a connection string or connection string name.");
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
        sql.Append($"SELECT {idColumn}, ST_AsBinary({geometryColumn}) AS geom_wkb{projection} FROM {table} ");
        sql.Append($"WHERE ST_Intersects({geometryColumn}, ST_MakeEnvelope(@minx, @miny, @maxx, @maxy, @srid))");

        // PostgreSQL won't implicitly compare a non-text column to a text parameter, so cast the
        // filter column to text (variant filter values are always bound as text parameters).
        var predicate = VariantFilterSql.Build(query.Variant.Filter, QuoteFilterColumn, i => $"@v{i}", out filterValues);
        if (predicate is not null)
        {
            sql.Append(" AND ");
            sql.Append(predicate);
        }

        return sql.ToString();
    }

    private static string QuoteFilterColumn(string value) => $"{QuoteIdentifier(value)}::text";

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
