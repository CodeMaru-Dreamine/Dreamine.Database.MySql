using Dreamine.Database.Abstractions;
using Dreamine.Database.Core.Mapping;
using Dreamine.Database.Core.Providers;
using MySqlConnector;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Dreamine.Database.MySql;

/// <summary>
/// \if KO
/// <para>MySQL용 Dreamine 데이터베이스 공급자 구현을 제공합니다.</para>
/// \endif
/// \if EN
/// <para>Provides a Dreamine database-provider implementation for MySQL.</para>
/// \endif
/// </summary>
public sealed class MySqlDatabaseProvider : DatabaseProviderBase
{
    /// <summary>
    /// \if KO
    /// <para>지정한 연결 문자열로 <see cref="MySqlDatabaseProvider"/>의 새 인스턴스를 초기화합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Initializes a new <see cref="MySqlDatabaseProvider"/> instance with the specified connection string.</para>
    /// \endif
    /// </summary>
    /// <param name="connectionString">
    /// \if KO
    /// <para>MySQL 연결 문자열입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The MySQL connection string.</para>
    /// \endif
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="connectionString"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="connectionString"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="connectionString"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="connectionString"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    public MySqlDatabaseProvider(string connectionString)
        : base(connectionString)
    {
    }

    /// <summary>
    /// \if KO
    /// <para>MySQL 공급자 종류를 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the MySQL provider kind.</para>
    /// \endif
    /// </summary>
    public override DatabaseProviderKind Kind => DatabaseProviderKind.MySql;

    /// <summary>
    /// \if KO
    /// <para>연결 문자열에 지정된 MySQL 데이터베이스가 없으면 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates the MySQL database named by the connection string when it does not exist.</para>
    /// \endif
    /// </summary>
    public override void EnsureDatabaseExists()
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            base.EnsureDatabaseExists();
            return;
        }

        var createDatabaseSql = BuildCreateDatabaseSql(databaseName);
        builder.Database = string.Empty;

        using var connection = new MySqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        ConfigureCreateDatabaseCommand(command, createDatabaseSql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// \if KO
    /// <para>연결 문자열에 지정된 MySQL 데이터베이스가 없으면 비동기적으로 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously creates the MySQL database named by the connection string when it does not exist.</para>
    /// \endif
    /// </summary>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>작업 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the operation.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>데이터베이스 확인 및 생성 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task representing database verification and creation.</para>
    /// \endif
    /// </returns>
    public override async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default)
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            await base.EnsureDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var createDatabaseSql = BuildCreateDatabaseSql(databaseName);
        builder.Database = string.Empty;

        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        ConfigureCreateDatabaseCommand(command, createDatabaseSql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// \if KO
    /// <para>현재 MySQL 데이터베이스에 지정한 테이블이 존재하는지 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Determines whether the specified table exists in the current MySQL database.</para>
    /// \endif
    /// </summary>
    /// <param name="tableName">
    /// \if KO
    /// <para>확인할 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table name to inspect.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether the table exists.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>현재 MySQL 데이터베이스에 지정한 테이블이 존재하는지 비동기적으로 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously determines whether the specified table exists in the current MySQL database.</para>
    /// \endif
    /// </summary>
    /// <param name="tableName">
    /// \if KO
    /// <para>확인할 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table name to inspect.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>조회 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the query.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether the table exists.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>구성된 연결 문자열을 사용하는 새 MySQL 연결을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates a new MySQL connection using the configured connection string.</para>
    /// \endif
    /// </summary>
    /// <returns>
    /// \if KO
    /// <para>닫힌 MySQL 연결입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A closed MySQL connection.</para>
    /// \endif
    /// </returns>
    protected override IDbConnection CreateConnection()
    {
        return new MySqlConnection(ConnectionString);
    }

    /// <summary>
    /// \if KO
    /// <para>MySQL 백틱 문법으로 식별자를 안전하게 인용합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Safely quotes an identifier using MySQL backtick syntax.</para>
    /// \endif
    /// </summary>
    /// <param name="identifier">
    /// \if KO
    /// <para>인용할 식별자입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The identifier to quote.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>이스케이프하고 인용한 식별자입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The escaped and quoted identifier.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="identifier"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="identifier"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="identifier"/>가 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="identifier"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    protected override string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";
    }

    private static void ValidateDatabaseIdentifier(string identifier)
    {
        if (identifier.Length > 64 ||
            identifier.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '_' or '-' or ' ')))
        {
            throw new ArgumentException(
                "MySQL database names may contain only letters, digits, spaces, underscores, and hyphens, up to 64 characters.",
                nameof(identifier));
        }
    }

    [SuppressMessage(
        "Security",
        "S2077:SQL queries should not be dynamically formatted",
        Justification = "Database identifiers cannot be SQL parameters. The identifier is constrained to MySQL's 64-character limit and an explicit alphanumeric allowlist before it is quoted.")]
    private string BuildCreateDatabaseSql(string databaseName)
    {
        ValidateDatabaseIdentifier(databaseName);
        return $"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(databaseName)}";
    }

    private static void ConfigureCreateDatabaseCommand(
        MySqlCommand command,
        string commandText)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        command.CommandText = commandText;
    }

    /// <summary>
    /// \if KO
    /// <para>자동 증가 키를 지원하는 MySQL CREATE TABLE SQL을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds MySQL CREATE TABLE SQL with auto-increment key support.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>테이블 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table entity map.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>MySQL CREATE TABLE SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The MySQL CREATE TABLE SQL.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>CLR 속성 형식을 대응하는 MySQL 열 형식으로 변환합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Converts a CLR property type to its corresponding MySQL column type.</para>
    /// \endif
    /// </summary>
    /// <param name="property">
    /// \if KO
    /// <para>변환할 속성 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The property mapping to convert.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>MySQL 열 형식 선언입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The MySQL column-type declaration.</para>
    /// \endif
    /// </returns>
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
