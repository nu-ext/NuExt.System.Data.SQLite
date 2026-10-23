# NuExt.System.Data.SQLite

`NuExt.System.Data.SQLite` is a high-performance ADO.NET infrastructure layer for SQLite-based .NET desktop, hybrid, and modular applications. It extends `System.Data.SQLite` with process-local connection synchronization, explicit transaction and database context management, lightweight DAL infrastructure, and transactional migrations with validated history tracking.

[![NuGet](https://img.shields.io/nuget/v/NuExt.System.Data.SQLite.svg)](https://www.nuget.org/packages/NuExt.System.Data.SQLite)
[![Ask DeepWiki](https://img.shields.io/badge/Ask-DeepWiki-blue)](https://deepwiki.com/nu-ext/NuExt.System.Data.SQLite)
[![Build](https://github.com/nu-ext/NuExt.System.Data.SQLite/actions/workflows/ci.yml/badge.svg)](https://github.com/nu-ext/NuExt.System.Data.SQLite/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/nu-ext/NuExt.System.Data.SQLite?label=license)](https://github.com/nu-ext/NuExt.System.Data.SQLite/blob/main/LICENSE)
[![Downloads](https://img.shields.io/nuget/dt/NuExt.System.Data.SQLite.svg)](https://www.nuget.org/packages/NuExt.System.Data.SQLite)

## Features

- **Synchronized Database Access**: Coordinates operations through reentrant process-local locks, including shared locks for multiple connections targeting the same database.
- **Transaction Scope Management**: Provides explicit transaction state tracking, prevents nested transactions, preserves original exceptions when rollback fails, and supports coordinated execution through reentrant connection locks.
- **Database Context Management**: Manages connection state, transaction lifetime, commit, rollback, and deterministic resource cleanup.
- **ADO.NET Utilities**: Provides helpers for commands, parameters, readers, data adapters, schema inspection, and common SQLite operations.
- **Allocation-Conscious APIs**: Uses span-based parameters, `ValueTask`-based coordination, pooling, and explicit callback state in performance-sensitive paths.
- **Database Migrations**: Applies schema and data migrations transactionally, supports independent scopes and cross-scope dependencies, records the resulting application order, and strictly validates each migration sequence.

## Why NuExt.System.Data.SQLite?

`NuExt.System.Data.SQLite` is intended for applications that use SQLite directly through ADO.NET and need a lightweight DAL foundation, predictable transaction ownership, synchronized multi-step operations, and forward-only schema evolution without adopting an ORM.

The library keeps SQL and ADO.NET primitives visible while centralizing repetitive infrastructure. Applications retain direct control over commands, parameters, readers, resource lifetimes, and transaction boundaries, while DAL components share a consistent execution model for standalone operations and coordinated units of work.

Database migrations follow the same ownership, locking, and transaction rules as application data access. Independent modules can evolve their schemas separately and declare dependencies without being forced into a single global migration sequence. Strict history validation prevents incompatible migration sequences from being applied silently.

By preserving SQLite's synchronous execution model and avoiding hidden change tracking, implicit query generation, and artificial asynchronous wrappers, the library keeps execution predictable, makes performance costs visible, and allows database behavior to be understood directly from application code.

## API Overview

### Data Access and Transactions

- **`SQLiteDalBase`**: Base class for DAL implementations with provider-managed operations and caller-owned transactional contexts shared across multiple DAL components.
- **`SQLiteDbConnection`**: Provides synchronized connection access, transaction coordination, command execution, schema inspection, and common SQLite operations.
- **`SQLiteDbTransaction`**: Tracks transaction state and provides explicit commit and rollback operations.
- **`SQLiteDbContext`**: Provides a connection and transaction shared across multiple DAL components within a caller-owned or provider-managed operation.

### Database Migrations

- **`SQLiteDbMigration`**: Defines a forward-only schema or data migration.
- **`SQLiteDbMigrationContext`**: Provides command execution within a migration transaction.
- **`SQLiteDbMigrationDependency`**: Declares a dependency on a migration from another scope.
- **`SQLiteDbMigrationPlan`**: Coordinates dependent migration sequences across multiple scopes.

## Concurrency and Locking

SQLite provides file-level locking and allows concurrent access through multiple connections, but application-level operations often consist of several related steps that must remain synchronized as a single scope. A transaction may include opening a connection, executing multiple commands, committing or rolling back, and restoring the original connection state. Coordinating only the individual SQL commands is not sufficient because another operation could enter between these steps.

`SQLiteDbConnection` addresses this by using a reentrant process-local lock around complete database operation scopes. Reentrancy allows higher-level operations, DAL components, and helper methods to call each other while participating in the same synchronized scope without deadlocking.

When shared locking is enabled, connections targeting the same database use the same process-local lock. This serializes competing operation scopes within the current process, reduces write contention, and helps prevent avoidable `"database is locked"` errors. When shared locking is disabled, each wrapper connection uses its own lock.

The process-local lock complements SQLite locking rather than replacing it. It cannot coordinate access from other processes or external database tools, which must still rely on SQLite file locking, busy timeouts, and an appropriate journal mode.

Transactions can be used directly when the caller guarantees exclusive connection access. Concurrent or reentrant scenarios should keep the complete transaction lifetime within `AcquireLock`, `ExecuteInTransaction`, or a provider-managed `SQLiteDbContext` scope.

## Database Context Ownership

SQLite DAL operations support both provider-managed and caller-owned database contexts.

When no context is supplied, the provider creates the connection and context, acquires the required lock, commits the transaction on success, rolls it back on failure, and releases all owned resources.

An existing `SQLiteDbContext` can instead be shared across multiple DAL components. In this mode, the caller retains ownership of the transaction and decides when to commit, roll back, and dispose the context.

## Quick Examples

### DAL Example

```csharp
internal sealed class MovieDal(Func<SQLiteDbConnection> createConnection)
    : SQLiteDalBase(createConnection)
{
    public ValueTask<MovieDto?> LoadMovieAsync(
        SQLiteDbContext? context,
        long id,
        CancellationToken cancellationToken)
    {
        return ExecuteInDbContextAsync(context, ctx =>
        {
            using var command = ctx.Connection.CreateCommandSelect(
                "Movies",
                ["Id", "Title", "Description", "DateReleased"],
                "WHERE Id = @Id LIMIT 1",
                DbType.Int64.CreateInputParam("@Id", id));

            MovieDto? movie = null;

            ctx.Connection.ExecuteReader(
                command,
                reader =>
                {
                    movie = new MovieDto(
                        reader.GetInt64("Id"),
                        reader.GetString("Title"),
                        reader.GetNullableString("Description"),
                        reader.GetDateTime("DateReleased"));
                },
                cancellationToken: cancellationToken);

            return movie;
        }, cancellationToken);
    }
}
```

Passing `null` as the database context creates a provider-managed operation that owns its connection, lock, transaction, and resource lifetime. Passing an existing `SQLiteDbContext` allows multiple DAL components to participate in the same caller-owned transaction.

### Transaction Example

```csharp
connection.ExecuteInTransaction(transaction =>
{
    connection.ExecuteNonQuery(
        "UPDATE Movies SET Title = @Title WHERE Id = @Id",
        cancellationToken,
        DbType.String.CreateInputParam("@Title", title),
        DbType.Int64.CreateInputParam("@Id", id));

    transaction.Commit();
}, cancellationToken);
```

The callback is responsible for committing the transaction or explicitly rolling it back when the operation completes normally. If the callback throws while the transaction is still active, the transaction is rolled back automatically without replacing the original exception with a rollback error.

### Migration Example

```csharp
internal sealed class CreateMoviesSchema_0001 : SQLiteDbMigration
{
    public override string Id => "0001_CreateMoviesSchema";

    public override void Up(SQLiteDbMigrationContext context)
    {
        context.ExecuteNonQuery(
            """
            CREATE TABLE Movies (
                Id INTEGER PRIMARY KEY,
                Title TEXT NOT NULL
            );
            """);
    }
}

SQLiteDbMigration[] migrations =
[
    new CreateMoviesSchema_0001()
];

migrations.Migrate(CreateConnection, "1.0.0", cancellationToken);
```

`Migrate` applies a single ordered migration sequence within the default scope. The complete migration batch runs within a synchronized transaction and is rolled back if any migration fails.

### Multiple Migration Scopes

```csharp
internal sealed class CreateVideoSchema_0001 : SQLiteDbMigration
{
    public override string Id => "0001_CreateVideoSchema";

    public override IReadOnlyList<SQLiteDbMigrationDependency> Dependencies =>
    [
        new("core", "0001_CreateMoviesSchema")
    ];

    public override void Up(SQLiteDbMigrationContext context)
    {
        context.ExecuteNonQuery(
            """
            CREATE TABLE Videos (
                Id INTEGER PRIMARY KEY,
                MovieId INTEGER NOT NULL,
                FileName TEXT NOT NULL
            );
            """);
    }
}

SQLiteDbMigration[] coreMigrations =
[
    new CreateMoviesSchema_0001()
];

SQLiteDbMigration[] videoModuleMigrations =
[
    new CreateVideoSchema_0001()
];

var plan = new SQLiteDbMigrationPlan()
    .AddScope("core", coreMigrations, "1.0.0")
    .AddScope("video", videoModuleMigrations, "1.0.0");

plan.Migrate(CreateConnection, cancellationToken);
```

Use `MigrateScope` when an application component owns its own migration sequence. For example, the application core and an optional video module can evolve independently without coordinating migration identifiers or sharing one global version number.

Use `SQLiteDbMigrationPlan` when one component requires schema changes introduced by another. The plan determines which migration can run next, preserves the declared order within every scope, and rolls back the complete migration batch if a required migration is missing or the dependencies cannot be resolved.

The applied history of each scope must match the beginning of its configured migration sequence. This prevents an application from silently continuing when migrations have been removed, reordered, or replaced after being applied.

Optional dependencies allow a module to integrate with another module when it is installed. A dependency is ignored only when the referenced scope has never been registered or recorded, so removing a previously installed module cannot silently hide its migration history.

Applied migrations are recorded in the `__nuext_migrations` table together with their scope, sequence, dependencies, application time, and optional application version.

When a connection factory is used, the complete plan runs within one synchronized transaction. A plan can also use an existing `SQLiteDbContext` when migrations must participate in a transaction owned by the caller.

## Installation

Via [NuGet](https://www.nuget.org/):

```sh
dotnet add package NuExt.System.Data.SQLite
```

Or via Visual Studio:

1. Go to `Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution...`.
2. Search for `NuExt.System.Data.SQLite`.
3. Click "Install".

## Compatibility

- **.NET Standard 2.0+, .NET 8/9/10, and .NET Framework 4.6.2+**

## Usage Examples

For examples of how to use these classes, see the [samples](https://github.com/nu-ext/NuExt.System.Data.SQLite/tree/main/samples). These samples provide practical guidance on implementing the library's features in real-world scenarios.

## Ecosystem

- [NuExt.System](https://github.com/nu-ext/NuExt.System)
- [NuExt.System.Data](https://github.com/nu-ext/NuExt.System.Data)
- [NuExt.Minimal.Mvvm](https://github.com/nu-ext/NuExt.Minimal.Mvvm)
- [NuExt.Minimal.Mvvm.SourceGenerator](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator)
- [NuExt.Minimal.Behaviors.Wpf](https://github.com/nu-ext/NuExt.Minimal.Behaviors.Wpf)
- [NuExt.Minimal.Mvvm.Wpf](https://github.com/nu-ext/NuExt.Minimal.Mvvm.Wpf)
- [NuExt.Minimal.Mvvm.MahApps.Metro](https://github.com/nu-ext/NuExt.Minimal.Mvvm.MahApps.Metro)

## Contributing

Issues and PRs are welcome. Keep changes minimal and performance-conscious.

## License

MIT. See LICENSE.