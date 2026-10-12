# NuExt.System.Data.SQLite

`NuExt.System.Data.SQLite` extends `System.Data.SQLite` with synchronized connection access, explicit transaction scopes, database context management, and utilities for common data access operations. It provides process-local coordination for connections accessing the same SQLite database and helps reduce lock contention in concurrent applications.

[![NuGet](https://img.shields.io/nuget/v/NuExt.System.Data.SQLite.svg)](https://www.nuget.org/packages/NuExt.System.Data.SQLite)
[![Ask DeepWiki](https://img.shields.io/badge/Ask-DeepWiki-blue)](https://deepwiki.com/nu-ext/NuExt.System.Data.SQLite)
[![Build](https://github.com/nu-ext/NuExt.System.Data.SQLite/actions/workflows/ci.yml/badge.svg)](https://github.com/nu-ext/NuExt.System.Data.SQLite/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/nu-ext/NuExt.System.Data.SQLite?label=license)](https://github.com/nu-ext/NuExt.System.Data.SQLite/blob/main/LICENSE)
[![Downloads](https://img.shields.io/nuget/dt/NuExt.System.Data.SQLite.svg)](https://www.nuget.org/packages/NuExt.System.Data.SQLite)

### Features

- **Synchronized Database Access**: Coordinates operations through reentrant process-local locks, including shared locks for multiple connections targeting the same database.
- **Transaction Scope Management**: Provides explicit transaction state tracking, prevents nested transactions, and supports coordinated execution through reentrant connection locks.
- **Database Context Management**: Manages connection state, transaction lifetime, commit, rollback, and deterministic resource cleanup.
- **ADO.NET Utilities**: Provides helpers for commands, parameters, readers, data adapters, schema inspection, and common SQLite operations.
- **Schema Versioning**: Supports controlled database schema updates through the existing converter-based mechanism.

### Important Considerations

SQLite allows multiple readers but serializes writes according to the configured journal mode and transaction state. Long-running write transactions can delay other operations and increase the likelihood of lock timeouts.

The library's shared lock is intentionally process-local. Applications that access the same database from multiple processes must still rely on SQLite locking, timeouts, and appropriate journal configuration.

### Concurrency and Locking

`SQLiteDbConnection` uses a reentrant process-local lock to coordinate database operations. When shared locking is enabled, connections targeting the same database use the same lock. When shared locking is disabled, each wrapper connection uses its own lock.

This coordination helps reduce competing write operations and `"database is locked"` errors originating within the same process. It does not replace SQLite file locking and cannot coordinate access from other processes or external database tools.

Transactions can be used directly when the caller guarantees exclusive connection access. Concurrent or reentrant scenarios should execute the entire transaction lifetime within `AcquireLock` or use `ExecuteInTransaction`.

#### Transaction Example

```csharp
connection.ExecuteInTransaction(transaction =>
{
    connection.ExecuteNonQuery(
        "UPDATE movies SET title = @title WHERE id = @id",
        CommandBehavior.Default,
        cancellationToken,
        DbType.String.CreateInputParam("@title", title),
        DbType.Int64.CreateInputParam("@id", id));

    transaction.Commit();
}, cancellationToken);
```

The callback is responsible for committing the transaction or explicitly rolling it back when the operation completes normally. If the callback throws while the transaction is still active, the transaction is rolled back automatically without replacing the original exception with a rollback error.

### Commonly Used Types

- **`System.Data.SQLite.SQLiteDalBase`**: Base class for SQLite-specific Data Access Layer (DAL) operations.
- **`System.Data.SQLite.SQLiteDbConnection`**: Wraps SQLite connections with synchronized process-local access, transaction coordination, and utilities for common database operations.
- **`System.Data.SQLite.SQLiteDbContext`**: Manages connection state and transaction lifetime, with optional validation that the connection lock remains held for the lifetime of the context.
- **`System.Data.SQLite.SQLiteDbConverter`**: Base class for applying updates to the SQLite database schema.
- **`System.Data.SQLite.SQLiteDbTransaction`**: Wraps SQLite transactions with explicit state tracking, commit and rollback operations, and transaction lifetime diagnostics.

### Installation

You can install `NuExt.System.Data.SQLite` via [NuGet](https://www.nuget.org/):

```sh
dotnet add package NuExt.System.Data.SQLite
```

Or through the Visual Studio package manager:

1. Go to `Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution...`.
2. Search for `NuExt.System.Data.SQLite`.
3. Click "Install".

### Usage Examples

For examples of how to use these classes, see the [samples](samples). These samples provide practical guidance on implementing the library's features in real-world scenarios.

### Contributing

Contributions are welcome! Feel free to submit issues, fork the repository, and send pull requests. Your feedback and suggestions for improvement are highly appreciated.

### License

Licensed under the MIT License. See the LICENSE file for details.