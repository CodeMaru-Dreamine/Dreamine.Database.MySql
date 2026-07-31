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
    private const string UnavailableServer =
        "Server=127.0.0.1;Port=1;User ID=tester;Password=test;Connection Timeout=1;";

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

    [Theory]
    [InlineData("dreamine")]
    [InlineData("Dreamine 2026")]
    [InlineData("드리마인_데이터")]
    [InlineData("dreamine-test")]
    public void BuildCreateDatabaseSql_AcceptsSupportedNames(string databaseName)
    {
        var sql = Invoke<string>("BuildCreateDatabaseSql", databaseName);

        Assert.Equal($"CREATE DATABASE IF NOT EXISTS `{databaseName}`", sql);
    }

    [Fact]
    public void BuildCreateDatabaseSql_AcceptsMaximumLengthName()
    {
        var databaseName = new string('a', 64);

        var sql = Invoke<string>("BuildCreateDatabaseSql", databaseName);

        Assert.EndsWith($"`{databaseName}`", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureCreateDatabaseCommand_AssignsValidatedCommandText()
    {
        using var command = new MySqlCommand();
        const string sql = "CREATE DATABASE IF NOT EXISTS `dreamine`";

        InvokeStatic("ConfigureCreateDatabaseCommand", command, sql);

        Assert.Equal(sql, command.CommandText);
    }

    [Fact]
    public void ConfigureCreateDatabaseCommand_RejectsInvalidArguments()
    {
        var nullCommand = Assert.Throws<TargetInvocationException>(
            () => InvokeStatic("ConfigureCreateDatabaseCommand", null!, "SELECT 1"));
        Assert.IsType<ArgumentNullException>(nullCommand.InnerException);

        using var command = new MySqlCommand();
        var blankText = Assert.Throws<TargetInvocationException>(
            () => InvokeStatic("ConfigureCreateDatabaseCommand", command, " "));
        Assert.IsType<ArgumentException>(blankText.InnerException, exactMatch: false);
    }

    [Fact]
    public void EnsureDatabaseExists_WithoutCatalog_UsesBaseConnectionPath()
    {
        var provider = new MySqlDatabaseProvider(UnavailableServer);

        Assert.Throws<MySqlException>(provider.EnsureDatabaseExists);
    }

    [Fact]
    public void EnsureDatabaseExists_WithValidCatalog_AttemptsServerConnection()
    {
        var provider = new MySqlDatabaseProvider(
            $"{UnavailableServer}Database=Dreamine 2026;");

        Assert.Throws<MySqlException>(provider.EnsureDatabaseExists);
    }

    [Fact]
    public async Task EnsureDatabaseExistsAsync_WithoutCatalog_ObservesCancellation()
    {
        var provider = new MySqlDatabaseProvider(UnavailableServer);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.EnsureDatabaseExistsAsync(cancellation.Token));
    }

    [Fact]
    public async Task EnsureDatabaseExistsAsync_WithValidCatalog_ObservesCancellation()
    {
        var provider = new MySqlDatabaseProvider(
            $"{UnavailableServer}Database=Dreamine 2026;");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.EnsureDatabaseExistsAsync(cancellation.Token));
    }

    [Fact]
    public void IsTableExists_WithValidName_AttemptsQueryConnection()
    {
        var provider = new MySqlDatabaseProvider(
            $"{UnavailableServer}Database=dreamine_tests;");

        Assert.Throws<MySqlException>(() => provider.IsTableExists("orders"));
    }

    [Fact]
    public async Task IsTableExistsAsync_WithValidName_ObservesCancellation()
    {
        var provider = new MySqlDatabaseProvider(
            $"{UnavailableServer}Database=dreamine_tests;");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.IsTableExistsAsync("orders", cancellation.Token));
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

    [Fact]
    public void BuildCreateTableSql_UsesPrimaryKeyWithoutAutoIncrementForAssignedKey()
    {
        var map = DatabaseEntityMap.Create<AssignedKeyEntity>();

        var sql = Invoke<string>("BuildCreateTableSql", map);

        Assert.Contains("`Id` INT PRIMARY KEY", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("AUTO_INCREMENT", sql, StringComparison.Ordinal);
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

    private static void InvokeStatic(string methodName, params object?[] arguments)
    {
        var method = typeof(MySqlDatabaseProvider).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(null, arguments);
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

    [DatabaseTable("assigned_keys")]
    private sealed class AssignedKeyEntity
    {
        [DatabaseKey]
        public int Id { get; set; }
    }
}
