# NuExt.System.Data.SQLite

`NuExt.System.Data.SQLite` extends `System.Data.SQLite` with process-local connection synchronization, explicit transaction and database context management, lightweight DAL infrastructure, and transactional database migrations with validated history tracking for modern .NET applications.

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
- **Allocation-Conscious APIs**: Provides span-based parameters, `ValueTask`-based coordination, and explicit-state callbacks that help avoid closure allocations.
- **Database Migrations**: Applies ordered schema and data migrations transactionally, records migration history with optional version metadata, and validates that the applied history matches the configured migration sequence.

## Why NuExt.System.Data.SQLite?

`NuExt.System.Data.SQLite` is designed as a performance-oriented infrastructure layer over ADO.NET rather than an ORM. It keeps SQL, commands, parameters, readers, and transactions explicit while removing repetitive connection, transaction, and resource lifecycle code. It also preserves SQLite's synchronous execution model instead of wrapping database operations in artificial asynchronous work.

The library supports both standalone DAL operations and coordinated units of work, protects complete database operation scopes during concurrent and reentrant execution, and provides forward-only migrations with deterministic ordering and strict history validation.

The result is a lightweight data access foundation that adds predictable resource ownership, transaction coordination, and schema evolution without introducing change tracking, SQL generation, or a custom query language.

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

Migrations are applied in the order in which they are registered. Applied migrations are recorded in the `__nuext_migrations` table together with their application time and optional version information. When a connection factory is used, the complete migration batch is executed within a synchronized transaction and is rolled back if any migration fails. Migrations can also run within an existing `SQLiteDbContext`, allowing migration and application operations to share the same caller-owned transaction.

## Commonly Used Types

- **`System.Data.SQLite.SQLiteDalBase`**: Base class for SQLite DAL implementations with synchronized provider-managed context execution and access to connection creation for custom scenarios.
- **`System.Data.SQLite.SQLiteDbConnection`**: Wraps SQLite connections with process-local synchronization, transaction coordination, command execution, schema inspection, and common data access utilities.
- **`System.Data.SQLite.SQLiteDbContext`**: Manages connection state and transaction lifetime, with optional validation that the connection lock remains held for the lifetime of the context.
- **`System.Data.SQLite.SQLiteDbMigration`**: Base class for defining ordered SQLite schema and data migrations.
- **`System.Data.SQLite.SQLiteDbMigrationContext`**: Provides the execution context used by SQLite migrations.
- **`System.Data.SQLite.SQLiteDbTransaction`**: Wraps SQLite transactions with explicit state tracking, commit and rollback operations, and transaction lifetime diagnostics.

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

**.NET Standard 2.0+, .NET 8/9/10 and .NET Framework 4.6.2+**

## Usage Examples

For examples of how to use these classes, see the [samples](samples). These samples provide practical guidance on implementing the library's features in real-world scenarios.

## Ecosystem

- [NuExt.Minimal.Mvvm](https://github.com/nu-ext/NuExt.Minimal.Mvvm)
- [NuExt.Minimal.Mvvm.SourceGenerator](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator)
- [NuExt.Minimal.Behaviors.Wpf](https://github.com/nu-ext/NuExt.Minimal.Behaviors.Wpf)
- [NuExt.Minimal.Mvvm.Wpf](https://github.com/nu-ext/NuExt.Minimal.Mvvm.Wpf)
- [NuExt.Minimal.Mvvm.MahApps.Metro](https://github.com/nu-ext/NuExt.Minimal.Mvvm.MahApps.Metro)
- [NuExt.System](https://github.com/nu-ext/NuExt.System)
- [NuExt.System.Data](https://github.com/nu-ext/NuExt.System.Data)

## Contributing

Issues and PRs are welcome. Keep changes minimal and performance-conscious.

## License

MIT. See LICENSE.