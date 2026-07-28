using System.Data;
using System.Reflection;
using Dreamine.Database.Abstractions;
using Dreamine.Database.Abstractions.Mapping;
using Dreamine.Database.Core.Mapping;
using MySqlConnector;
using Xunit;

namespace Dreamine.Database.MySql.Tests;

public sealed class MySqlDatabaseProviderTests
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=dreamine_tests;User ID=tester;Password=test;";

    [Fact]
    public void Constructor_RejectsMissingConnectionStrings()
    {
        Assert.Throws<ArgumentNullException>(() => new MySqlDatabaseProvider(null!));
        Assert.Throws<ArgumentException>(() => new MySqlDatabaseProvider(""));
        Assert.Throws<ArgumentException>(() => new MySqlDatabaseProvider(" "));
    }

    [Fact]
    public void Provider_ReportsKindAndPreservesConnectionString()
    {
        var provider = CreateProvider();

        Assert.Equal(DatabaseProviderKind.MySql, provider.Kind);
        Assert.Equal(ConnectionString, provider.ConnectionString);
    }

    [Theory]
    [InlineData("orders", "`orders`")]
    [InlineData("order`items", "`order``items`")]
    [InlineData("한글 테이블", "`한글 테이블`")]
    public void QuoteIdentifier_UsesMySqlBackticks(string identifier, string expected)
    {
        Assert.Equal(expected, Invoke<string>("QuoteIdentifier", identifier));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void QuoteIdentifier_RejectsMissingIdentifiers(string? identifier)
    {
        var exception = Assert.Throws<TargetInvocationException>(
            () => Invoke<string>("QuoteIdentifier", identifier!));

        Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    [Fact]
    public void CreateConnection_ReturnsClosedMySqlConnection()
    {
        using var connection = Invoke<IDbConnection>("CreateConnection");
        var mysqlConnection = Assert.IsType<MySqlConnection>(connection);

        Assert.Equal(ConnectionState.Closed, mysqlConnection.State);
        Assert.Equal("dreamine_tests", mysqlConnection.Database);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsTableExists_RejectsMissingNames(string? tableName)
    {
        var provider = CreateProvider();

        Assert.ThrowsAny<ArgumentException>(() => provider.IsTableExists(tableName!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task IsTableExistsAsync_RejectsMissingNames(string? tableName)
    {
        var provider = CreateProvider();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.IsTableExistsAsync(tableName!));
    }

    [Theory]
    [InlineData("bad/name")]
    [InlineData("bad;name")]
    [InlineData("bad`name")]
    public void EnsureDatabaseExists_RejectsUnsafeDatabaseNamesBeforeConnecting(string databaseName)
    {
        var provider = CreateProvider(databaseName);

        var exception = Assert.Throws<ArgumentException>(provider.EnsureDatabaseExists);

        Assert.Equal("identifier", exception.ParamName);
    }

    [Fact]
    public void EnsureDatabaseExists_RejectsDatabaseNamesLongerThanMySqlLimit()
    {
        var provider = CreateProvider(new string('a', 65));

        Assert.Throws<ArgumentException>(provider.EnsureDatabaseExists);
    }

    [Theory]
    [InlineData("bad/name")]
    [InlineData("bad;name")]
    [InlineData("bad`name")]
    public async Task EnsureDatabaseExistsAsync_RejectsUnsafeDatabaseNamesBeforeConnecting(
        string databaseName)
    {
        var provider = CreateProvider(databaseName);

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.EnsureDatabaseExistsAsync());
    }

    [Fact]
    public void BuildCreateTableSql_UsesMySqlTypesAndGeneratedKeySyntax()
    {
        var map = DatabaseEntityMap.Create<AllTypesEntity>();

        var sql = Invoke<string>("BuildCreateTableSql", map);

        Assert.StartsWith("CREATE TABLE IF NOT EXISTS `all_types` (", sql, StringComparison.Ordinal);
        Assert.Contains("`Id` BIGINT AUTO_INCREMENT PRIMARY KEY", sql, StringComparison.Ordinal);
        Assert.Contains("`Enabled` TINYINT(1)", sql, StringComparison.Ordinal);
        Assert.Contains("`Small` SMALLINT", sql, StringComparison.Ordinal);
        Assert.Contains("`Count` INT", sql, StringComparison.Ordinal);
        Assert.Contains("`Ratio` DOUBLE", sql, StringComparison.Ordinal);
        Assert.Contains("`Amount` DECIMAL(18, 4)", sql, StringComparison.Ordinal);
        Assert.Contains("`CreatedAt` DATETIME", sql, StringComparison.Ordinal);
        Assert.Contains("`Payload` BLOB", sql, StringComparison.Ordinal);
        Assert.Contains("`Name` TEXT", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(AllTypesEntity.Enabled), "TINYINT(1)")]
    [InlineData(nameof(AllTypesEntity.SmallByte), "SMALLINT")]
    [InlineData(nameof(AllTypesEntity.Small), "SMALLINT")]
    [InlineData(nameof(AllTypesEntity.Count), "INT")]
    [InlineData(nameof(AllTypesEntity.Id), "BIGINT")]
    [InlineData(nameof(AllTypesEntity.RatioFloat), "DOUBLE")]
    [InlineData(nameof(AllTypesEntity.Ratio), "DOUBLE")]
    [InlineData(nameof(AllTypesEntity.Amount), "DECIMAL(18, 4)")]
    [InlineData(nameof(AllTypesEntity.CreatedAt), "DATETIME")]
    [InlineData(nameof(AllTypesEntity.ChangedAt), "DATETIME")]
    [InlineData(nameof(AllTypesEntity.Payload), "BLOB")]
    [InlineData(nameof(AllTypesEntity.Name), "TEXT")]
    public void GetSqlType_MapsClrTypes(string propertyName, string expected)
    {
        var property = typeof(AllTypesEntity).GetProperty(propertyName)!;
        var map = DatabasePropertyMap.Create(property);

        Assert.Equal(expected, Invoke<string>("GetSqlType", map));
    }

    private static MySqlDatabaseProvider CreateProvider() =>
        new(ConnectionString);

    private static MySqlDatabaseProvider CreateProvider(string databaseName)
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString)
        {
            Database = databaseName
        };
        return new MySqlDatabaseProvider(builder.ConnectionString);
    }

    private static T Invoke<T>(string methodName, params object[] arguments)
    {
        var method = typeof(MySqlDatabaseProvider).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (T)method.Invoke(CreateProvider(), arguments)!;
    }

    [DatabaseTable("all_types")]
    private sealed class AllTypesEntity
    {
        [DatabaseKey]
        [DatabaseGenerated]
        public long Id { get; set; }

        public bool Enabled { get; set; }

        public byte SmallByte { get; set; }

        public short Small { get; set; }

        public int Count { get; set; }

        public float RatioFloat { get; set; }

        public double Ratio { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTimeOffset ChangedAt { get; set; }

        public byte[] Payload { get; set; } = [];

        public string Name { get; set; } = "";
    }
}
