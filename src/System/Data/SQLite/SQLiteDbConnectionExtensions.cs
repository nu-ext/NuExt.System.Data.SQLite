using System.Diagnostics;

namespace System.Data.SQLite;

public static class SQLiteDbConnectionExtensions
{
    /// <summary>
    /// Executes the specified action within a database transaction.
    /// The action is responsible for committing or rolling back the transaction.
    /// If the action throws an exception, an active transaction is rolled back automatically.
    /// If the action completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="action">The action to execute within the transaction.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static void ExecuteInTransaction(this SQLiteDbConnection connection, Action<SQLiteDbTransaction> action, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(action is not null, $"{nameof(action)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    context.Action(transaction);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, Action: action), cancellationToken);
    }

    /// <summary>
    /// Executes the specified action with the provided state within a database transaction.
    /// The action is responsible for committing or rolling back the transaction.
    /// If the action throws an exception, an active transaction is rolled back automatically.
    /// If the action completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="action">The action to execute within the transaction.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static void ExecuteInTransaction<TState>(this SQLiteDbConnection connection, Action<SQLiteDbTransaction, TState> action, TState state, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(action is not null, $"{nameof(action)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    context.Action(transaction, context.State);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, Action: action, State: state), cancellationToken);
    }

    /// <summary>
    /// Executes the specified action within a database transaction using the specified isolation level.
    /// The action is responsible for committing or rolling back the transaction.
    /// If the action throws an exception, an active transaction is rolled back automatically.
    /// If the action completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="isolationLevel">The isolation level of the transaction.</param>
    /// <param name="action">The action to execute within the transaction.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static void ExecuteInTransaction(this SQLiteDbConnection connection, IsolationLevel isolationLevel, Action<SQLiteDbTransaction> action, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(action is not null, $"{nameof(action)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction(context.IsolationLevel);
                try
                {
                    context.Action(transaction);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, IsolationLevel: isolationLevel, Action: action), cancellationToken);
    }

    /// <summary>
    /// Executes the specified action with the provided state within a database transaction using the specified isolation level.
    /// The action is responsible for committing or rolling back the transaction.
    /// If the action throws an exception, an active transaction is rolled back automatically.
    /// If the action completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="isolationLevel">The isolation level of the transaction.</param>
    /// <param name="action">The action to execute within the transaction.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static void ExecuteInTransaction<TState>(this SQLiteDbConnection connection, IsolationLevel isolationLevel, Action<SQLiteDbTransaction, TState> action, TState state, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(action is not null, $"{nameof(action)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction(context.IsolationLevel);
                try
                {
                    context.Action(transaction, context.State);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, IsolationLevel: isolationLevel, Action: action, State: state), cancellationToken);
    }

    /// <summary>
    /// Executes the specified function within a database transaction.
    /// The function is responsible for committing or rolling back the transaction.
    /// If the function throws an exception, an active transaction is rolled back automatically.
    /// If the function completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="func">The function to execute within the transaction.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The result returned by the function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static T ExecuteInTransaction<T>(this SQLiteDbConnection connection, Func<SQLiteDbTransaction, T> func, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(func is not null, $"{nameof(func)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        return connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    return context.Func(transaction);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, Func: func), cancellationToken);
    }

    /// <summary>
    /// Executes the specified function with the provided state within a database transaction.
    /// The function is responsible for committing or rolling back the transaction.
    /// If the function throws an exception, an active transaction is rolled back automatically.
    /// If the function completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the function.</typeparam>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="func">The function to execute within the transaction.</param>
    /// <param name="state">The state to pass to the function.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The result returned by the function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static TResult ExecuteInTransaction<TState, TResult>(this SQLiteDbConnection connection, Func<SQLiteDbTransaction, TState, TResult> func, TState state, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(func is not null, $"{nameof(func)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        return connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    return context.Func(transaction, context.State);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, Func: func, State: state), cancellationToken);
    }

    /// <summary>
    /// Executes the specified function within a database transaction using the specified isolation level.
    /// The function is responsible for committing or rolling back the transaction.
    /// If the function throws an exception, an active transaction is rolled back automatically.
    /// If the function completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="isolationLevel">The isolation level of the transaction.</param>
    /// <param name="func">The function to execute within the transaction.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The result returned by the function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static T ExecuteInTransaction<T>(this SQLiteDbConnection connection, IsolationLevel isolationLevel, Func<SQLiteDbTransaction, T> func, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(func is not null, $"{nameof(func)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        return connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction(context.IsolationLevel);
                try
                {
                    return context.Func(transaction);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, IsolationLevel: isolationLevel, Func: func), cancellationToken);
    }

    /// <summary>
    /// Executes the specified function with the provided state within a database transaction using the specified isolation level.
    /// The function is responsible for committing or rolling back the transaction.
    /// If the function throws an exception, an active transaction is rolled back automatically.
    /// If the function completes without committing or rolling back, the transaction is disposed without being committed.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the function.</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the function.</typeparam>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="isolationLevel">The isolation level of the transaction.</param>
    /// <param name="func">The function to execute within the transaction.</param>
    /// <param name="state">The state to pass to the function.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The result returned by the function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public static TResult ExecuteInTransaction<TState, TResult>(this SQLiteDbConnection connection, IsolationLevel isolationLevel, Func<SQLiteDbTransaction, TState, TResult> func, TState state, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(func is not null, $"{nameof(func)} is null");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        return connection.AcquireLock(static context =>
        {
            SQLiteDbConnection connection = context.Connection;

            Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
            Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

            connection.QuietOpen(out var open);
            try
            {
                using var transaction = connection.BeginTransaction(context.IsolationLevel);
                try
                {
                    return context.Func(transaction, context.State);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex is OperationCanceledException || connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    if (transaction.IsActive)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception rollbackException)
                        {
                            Trace.WriteLine($"SQLite transaction rollback failed: {rollbackException}");
                            Debug.Fail($"SQLite transaction rollback failed: {rollbackException}");
                        }
                    }
                    throw;
                }
            }
            finally
            {
                connection.QuietClose(open);
            }
        }, (Connection: connection, IsolationLevel: isolationLevel, Func: func, State: state), cancellationToken);
    }

    internal static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Gets the names of the columns in the specified table.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>A collection containing the table column names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> is <see langword="null"/> or empty.</exception>
    public static ICollection<string> GetTableColumns(this SQLiteDbConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(tableName), $"{nameof(tableName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        connection.ExecuteReader($"PRAGMA table_info({QuoteIdentifier(tableName)})", Read, columns, CommandBehavior.Default, cancellationToken);
        return columns;

        static void Read(SQLiteDataReader reader, HashSet<string> columns)
        {
            string name = reader.GetString("name");
            columns.Add(name);
        }
    }

    /// <summary>
    /// Gets the schema of the specified table.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>An empty data table containing the schema of the specified table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> is <see langword="null"/> or empty.</exception>
    public static DataTable GetTableSchema(this SQLiteDbConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(tableName), $"{nameof(tableName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var schema = connection.Select($"SELECT * FROM {QuoteIdentifier(tableName)} WHERE 0=1", cancellationToken);
        schema.TableName = tableName;
        return schema;
    }

    /// <summary>
    /// Gets the names of all tables in the database.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>A collection containing the table names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public static ICollection<string> GetAllTables(this SQLiteDbConnection connection, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        ArgumentNullException.ThrowIfNull(connection);

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        connection.ExecuteReader("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name", Read, tables, CommandBehavior.Default, cancellationToken);
        return tables;

        static void Read(SQLiteDataReader reader, HashSet<string> tables)
        {
            string name = reader.GetString(0);
            tables.Add(name);
        }
    }

    /// <summary>
    /// Gets the names of all views in the database.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>A collection containing the view names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public static ICollection<string> GetAllViews(this SQLiteDbConnection connection, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        ArgumentNullException.ThrowIfNull(connection);

        var views = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        connection.ExecuteReader("SELECT name FROM sqlite_master WHERE type='view' ORDER BY name", Read, views, CommandBehavior.Default, cancellationToken);
        return views;

        static void Read(SQLiteDataReader reader, HashSet<string> views)
        {
            string name = reader.GetString(0);
            views.Add(name);
        }
    }

    /// <summary>
    /// Gets the names of the indexes associated with the specified table.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>A collection containing the index names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> is <see langword="null"/> or empty.</exception>
    public static ICollection<string> GetTableIndexes(this SQLiteDbConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(tableName), $"{nameof(tableName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        connection.ExecuteReader("SELECT name FROM sqlite_master WHERE type='index' AND tbl_name=@tbl_name ORDER BY name", Read, indexes, CommandBehavior.Default, cancellationToken, DbType.String.CreateInputParam("@tbl_name", tableName));
        return indexes;

        static void Read(SQLiteDataReader reader, HashSet<string> indexes)
        {
            string name = reader.GetString(0);
            indexes.Add(name);
        }
    }

    /// <summary>
    /// Determines whether the specified table contains no rows.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns><see langword="true"/> if the table contains no rows; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> is <see langword="null"/> or empty.</exception>
    public static bool IsTableEmpty(this SQLiteDbConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(tableName), $"{nameof(tableName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var res = connection.ExecuteScalar($"SELECT 1 FROM {QuoteIdentifier(tableName)} LIMIT 1", CommandBehavior.Default, cancellationToken);
        return res is null;
    }

    /// <summary>
    /// Determines whether the specified table exists.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns><see langword="true"/> if the table exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> is <see langword="null"/> or empty.</exception>
    public static bool TableExists(this SQLiteDbConnection connection, string tableName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(tableName), $"{nameof(tableName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var res = connection.ExecuteScalar("SELECT 1 FROM sqlite_master WHERE name=@name AND type='table' LIMIT 1",
            CommandBehavior.Default, cancellationToken, DbType.String.CreateInputParam("@name", tableName));
        return res is not null;
    }

    /// <summary>
    /// Determines whether the specified index exists for the specified table.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns><see langword="true"/> if the index exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tableName"/> or <paramref name="indexName"/> is <see langword="null"/> or empty.</exception>
    public static bool TableIndexExists(this SQLiteDbConnection connection, string tableName, string indexName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(tableName), $"{nameof(tableName)} is null or empty");
        Debug.Assert(!string.IsNullOrEmpty(indexName), $"{nameof(indexName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var res = connection.ExecuteScalar(
            "SELECT 1 FROM sqlite_master WHERE tbl_name=@tbl_name AND name=@name AND type='index' LIMIT 1",
            CommandBehavior.Default, cancellationToken,
            DbType.String.CreateInputParam("@tbl_name", tableName),
            DbType.String.CreateInputParam("@name", indexName));
        return res is not null;
    }

    /// <summary>
    /// Determines whether the specified trigger exists.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="triggerName">The name of the trigger.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns><see langword="true"/> if the trigger exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="triggerName"/> is <see langword="null"/> or empty.</exception>
    public static bool TriggerExists(this SQLiteDbConnection connection, string triggerName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(triggerName), $"{nameof(triggerName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(triggerName);

        var res = connection.ExecuteScalar("SELECT 1 FROM sqlite_master WHERE name=@name AND type='trigger' LIMIT 1",
            CommandBehavior.Default, cancellationToken, DbType.String.CreateInputParam("@name", triggerName));
        return res is not null;
    }

    /// <summary>
    /// Determines whether the specified view exists.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="viewName">The name of the view.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns><see langword="true"/> if the view exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="viewName"/> is <see langword="null"/> or empty.</exception>
    public static bool ViewExists(this SQLiteDbConnection connection, string viewName, CancellationToken cancellationToken = default)
    {
        Debug.Assert(connection is not null, $"{nameof(connection)} is null");
        Debug.Assert(!string.IsNullOrEmpty(viewName), $"{nameof(viewName)} is null or empty");
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(viewName);

        var res = connection.ExecuteScalar("SELECT 1 FROM sqlite_master WHERE name=@name AND type='view' LIMIT 1",
            CommandBehavior.Default, cancellationToken, DbType.String.CreateInputParam("@name", viewName));
        return res is not null;
    }
}
