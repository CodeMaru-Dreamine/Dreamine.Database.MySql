# Dreamine.Database.MySql

[![CI](https://github.com/CodeMaru-Dreamine/Dreamine.Database.MySql/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/CodeMaru-Dreamine/Dreamine.Database.MySql/actions/workflows/ci.yml) [![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=CodeMaru-Dreamine_Dreamine.Database.MySql&metric=alert_status&branch=main)](https://sonarcloud.io/summary/new_code?id=CodeMaru-Dreamine_Dreamine.Database.MySql&branch=main) [![Security](https://sonarcloud.io/api/project_badges/measure?project=CodeMaru-Dreamine_Dreamine.Database.MySql&metric=security_rating&branch=main)](https://sonarcloud.io/summary/new_code?id=CodeMaru-Dreamine_Dreamine.Database.MySql&branch=main) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=CodeMaru-Dreamine_Dreamine.Database.MySql&metric=coverage&branch=main)](https://sonarcloud.io/summary/new_code?id=CodeMaru-Dreamine_Dreamine.Database.MySql&branch=main)

[![License](https://img.shields.io/github/license/CodeMaru-Dreamine/Dreamine.Database.MySql?label=license)](./LICENSE) [![.NET](https://img.shields.io/badge/.NET-8-512BD4)](https://dotnet.microsoft.com/) [![NuGet](https://img.shields.io/nuget/v/Dreamine.Database.MySql?label=nuget)](https://www.nuget.org/packages/Dreamine.Database.MySql) [![Downloads](https://img.shields.io/nuget/dt/Dreamine.Database.MySql?label=downloads)](https://www.nuget.org/packages/Dreamine.Database.MySql)

[![Docs](https://img.shields.io/badge/📘_Docs-dreamine.kr-2F80ED)](https://dreamine.kr) [![Guide](https://img.shields.io/badge/📘_Guide-dreamine.kr-3498DB)](https://dreamine.kr) [![Playground](https://img.shields.io/badge/🎮_Playground-dreamine.kr-8E44AD)](https://dreamine.kr) [![Book](https://img.shields.io/badge/📖_Book-Practical_MVVM_Architecture-111111)](https://dreamine.kr)

`Dreamine.Database.MySql` is the MySQL provider for the Dreamine Database package family.

[한국어 문서](./README_KO.md)

## Package Role

This package implements `IDatabaseProvider` for MySQL using `MySqlConnector`.

```text
Dreamine.Database.Abstractions
        ↑
Dreamine.Database.Core
        ↑
Dreamine.Database.MySql
```

## Features

- MySQL connection creation
- Backtick identifier quoting
- MySQL type mapping
- `CREATE DATABASE IF NOT EXISTS` during `EnsureDatabaseExists()`
- `CREATE TABLE IF NOT EXISTS` table creation
- CRUD support through the shared `DatabaseProviderBase`

## Quick Start

```csharp
using Dreamine.Database.MySql;

var provider = new MySqlDatabaseProvider(
    "Server=localhost;Port=3306;Database=dreamine_sample;User ID=root;Password=1234;");

provider.EnsureDatabaseExists();
provider.CreateTable<SampleCustomer>();
provider.Insert(new SampleCustomer
{
    Name = "Dreamine",
    Role = "Operator",
    CreatedAt = DateTime.Now
});
```

## Database Creation Note

`EnsureDatabaseExists()` connects to the MySQL server without the selected database, creates the database when it is missing, and then later CRUD operations use the original connection string.

The configured account must have permission to create databases. If the account does not have that permission, create the database manually and then run the sample again.

## Dependencies

- `Dreamine.Database.Abstractions`
- `Dreamine.Database.Core`
- `MySqlConnector`

## Target Framework

```text
net8.0
```

## Samples and Tests

- Unit tests: `20_SOURCES/200. Tests/Dreamine.FullKit.Tests/Database`
- WPF sample: `20_SOURCES/998. DEMO/000. Sample/010. Wpfs/SampleSmart/Pages/PageSub/PageDatabase.xaml`

## License

MIT License
