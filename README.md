# NuExt.System.Data.SQLite

`NuExt.System.Data.SQLite` is a powerful extension library for the SQLite database engine, designed to enhance your data access layer with robust and thread-safe operations. The package offers tools for efficient transaction management, smooth database schema updates, and safe concurrent access, ensuring consistency and reliability in your applications.

[![NuGet](https://img.shields.io/nuget/v/NuExt.System.Data.SQLite.svg)](https://www.nuget.org/packages/NuExt.System.Data.SQLite)
[![Build](https://github.com/nu-ext/NuExt.System.Data.SQLite/actions/workflows/ci.yml/badge.svg)](https://github.com/nu-ext/NuExt.System.Data.SQLite/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/nu-ext/NuExt.System.Data.SQLite?label=license)](https://github.com/nu-ext/NuExt.System.Data.SQLite/blob/main/LICENSE)
[![Downloads](https://img.shields.io/nuget/dt/NuExt.System.Data.SQLite.svg)](https://www.nuget.org/packages/NuExt.System.Data.SQLite)

### Features

- **Safe Concurrent Data Access**: Implements mechanisms to manage parallel data operations across multiple threads within SQLite's locking constraints, ensuring consistency.
- **Robust Database Context Management**: Simplifies the handling of database connections and contexts, making it easy to manage their lifecycle and interactions.
- **Efficient Transaction Handling**: Provides straightforward methods for managing transactions, ensuring atomicity and durability.
- **Schema Updates and Migrations**: Facilitates controlled updates to the database schema through a structured approach.

### Important Considerations

SQLite uses file-level locking during write transactions, which means:
- While a write transaction is active, other threads or processes cannot write to the database. They will wait until the transaction is completed (committed or rolled back).
- Read operations can still proceed concurrently with a write transaction under normal circumstances.

This behavior ensures data integrity and consistency but requires careful management of concurrent write operations to avoid performance bottlenecks.

### Database Locking Error

In scenarios where multiple write operations occur simultaneously, especially in journaled mode, you might encounter the "database is locked" error. `System.Data.SQLite.SQLiteDbConnection` is designed to help avoid this specific issue by managing transactions and concurrency via a set of `AcquireLock` methods. These methods ensure thread-safe execution of database operations while holding a lock and help to prevent "database is locked" errors.

### Commonly Used Types

- **`System.Data.SQLite.SQLiteDalBase`**: Base class for SQLite-specific Data Access Layer (DAL) operations.
- **`System.Data.SQLite.SQLiteDbConnection`**: Wrapper for SQLite connections providing thread-safe concurrent access and a set of methods for data management.
- **`System.Data.SQLite.SQLiteDbContext`**: SQLite database context providing connection and transaction management.
- **`System.Data.SQLite.SQLiteDbConverter`**: Base class for applying updates to the SQLite database schema.
- **`System.Data.SQLite.SQLiteDbTransaction`**: Provides methods for managing SQLite transactions.

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