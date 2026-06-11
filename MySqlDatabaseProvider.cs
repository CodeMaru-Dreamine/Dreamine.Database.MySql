using Dreamine.Database.Abstractions;
using Dreamine.Database.Core.Mapping;
using Dreamine.Database.Core.Providers;
using MySqlConnector;
using System.Data;

namespace Dreamine.Database.MySql;

/// <summary>
/// Provides a MySQL database provider implementation.
/// </summary>
public sealed class MySqlDatabaseProvider : DatabaseProviderBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlDatabaseProvider"/> class.
    /// </summary>
    /// <param name="connectionString">The MySQL connection string.</param>
    public MySqlDatabaseProvider(string connectionString)
        : base(connectionString)
    {
    }

    public override DatabaseProviderKind Kind => DatabaseProviderKind.MySql;

    public override void EnsureDatabaseExists()
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            base.EnsureDatabaseExists();
            return;
        }

        builder.Database = string.Empty;

        using var connection = new MySqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(databaseName)}";
        command.ExecuteNonQuery();
    }

    public override async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default)
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            await base.EnsureDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        builder.Database = string.Empty;

        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(databaseName)}";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public override bool IsTableExists(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        const string sql = """
            SELECT COUNT(1)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
            """;

        return ExecuteScalar<long>(sql, new { TableName = tableName }) > 0;
    }

    public override async Task<bool> IsTableExistsAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        const string sql = """
            SELECT COUNT(1)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
            """;

        var count = await ExecuteScalarAsync<long>(sql, new { TableName = tableName }, cancellationToken)
            .ConfigureAwait(false);
        return count > 0;
    }

    protected override IDbConnection CreateConnection()
    {
        return new MySqlConnection(ConnectionString);
    }

    protected override string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";
    }

    protected override string BuildCreateTableSql(DatabaseEntityMap map)
    {
        var columns = map.Properties.Select(property =>
        {
            var sql = $"{QuoteIdentifier(property.ColumnName)} {GetSqlType(property)}";
            if (property.IsKey)
            {
                sql += property.IsGenerated ? " AUTO_INCREMENT PRIMARY KEY" : " PRIMARY KEY";
            }

            return sql;
        });

        return $"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(map.TableName)} ({string.Join(", ", columns)})";
    }

    protected override string GetSqlType(DatabasePropertyMap property)
    {
        var type = property.PropertyType;

        if (type == typeof(bool))
        {
            return "TINYINT(1)";
        }

        if (type == typeof(byte) || type == typeof(short))
        {
            return "SMALLINT";
        }

        if (type == typeof(int))
        {
            return "INT";
        }

        if (type == typeof(long))
        {
            return "BIGINT";
        }

        if (type == typeof(float) || type == typeof(double))
        {
            return "DOUBLE";
        }

        if (type == typeof(decimal))
        {
            return "DECIMAL(18, 4)";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return "DATETIME";
        }

        if (type == typeof(byte[]))
        {
            return "BLOB";
        }

        return "TEXT";
    }
}
