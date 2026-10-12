using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using static System.Data.SQLite.SQLiteDbConnectionExtensions;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Methods

    #region CreateCommand

    /// <summary>
    /// Creates a new SQLite command associated with this connection.
    /// </summary>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteCommand CreateCommand()
    {
        CheckDisposed();
        return _conn.CreateCommand();
    }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    /// <summary>
    /// Creates a command with the specified SQL statement and parameters.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteCommand CreateCommand(string sql, params ReadOnlySpan<SQLiteParameter> parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);

        SQLiteCommand command = CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return command;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddParameters(SQLiteCommand command, ReadOnlySpan<SQLiteParameter> parameters)
    {
        ValidateParameters(parameters);
        for (int i = 0; i < parameters.Length; i++)
        {
            command.Parameters.Add(parameters[i]);
        }
    }

    [Conditional("DEBUG")]
    private static void ValidateParameters(ReadOnlySpan<SQLiteParameter> parameters)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            for (int j = 0; j < i; j++)
            {
                Debug.Assert(!string.Equals(parameters[i]?.ParameterName, parameters[j]?.ParameterName, StringComparison.Ordinal), $"Duplicate parameter name: {parameters[i]?.ParameterName}");
            }
        }
    }
#else
    /// <summary>
    /// Creates a command with the specified SQL statement and parameters.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteCommand CreateCommand(string sql, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);

        SQLiteCommand command = CreateCommand();
        command.CommandText = sql;
        if (parameters is { Length: > 0 })
        {
            Debug.Assert(new HashSet<string>(parameters.Select(p => p.ParameterName)).Count == parameters.Length);
            command.Parameters.AddRange(parameters);
        }
        return command;
    }
#endif

    /// <summary>
    /// Creates an INSERT command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to insert.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, string? expr = null, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count() != parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandInsert)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandInsert)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"INSERT INTO {QuoteIdentifier(tableName)} (");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}{QuoteIdentifier(field)}");
            delimiter = ", ";
        }
        builder.Append(") VALUES (");
        delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}@{field}");
            delimiter = ", ";
        }
        builder.Append(')');
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    /// <summary>
    /// Creates an INSERT OR IGNORE command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to insert.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, string? expr = null, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count() != parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandInsertOrIgnore)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandInsertOrIgnore)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"INSERT OR IGNORE INTO {QuoteIdentifier(tableName)} (");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}{QuoteIdentifier(field)}");
            delimiter = ", ";
        }
        builder.Append(") VALUES (");
        delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}@{field}");
            delimiter = ", ";
        }
        builder.Append(')');
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    /// <summary>
    /// Creates an INSERT OR REPLACE command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to insert.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, string? expr = null, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count() != parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandInsertOrReplace)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandInsertOrReplace)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"REPLACE INTO {QuoteIdentifier(tableName)} (");//Alias for INSERT OR REPLACE
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}{QuoteIdentifier(field)}");
            delimiter = ", ";
        }
        builder.Append(") VALUES (");
        delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}@{field}");
            delimiter = ", ";
        }
        builder.Append(')');
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    /// <summary>
    /// Creates a SELECT command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandSelect(string tableName, IEnumerable<string> fields, string? expr = null, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandSelect(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append("SELECT ");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}{QuoteIdentifier(field)}");
            delimiter = ", ";
        }
        builder.Append($" FROM {QuoteIdentifier(tableName)}");
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    /// <summary>
    /// Creates an UPDATE command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to update.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, string? expr = null, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count() > parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandUpdate)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandUpdate)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"UPDATE {QuoteIdentifier(tableName)} SET ");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}{QuoteIdentifier(field)}=@{field}");
            delimiter = ", ";
        }
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    /// <summary>
    /// Creates a DELETE command for the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandDelete(string tableName, string? expr = null, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandDelete(string tableName, string? expr = null, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"DELETE FROM {QuoteIdentifier(tableName)}");
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    #endregion

    #region ExecuteNonQuery

    /// <summary>
    /// Executes the specified SQL statement and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of affected rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteNonQuery(string sql, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteNonQuery(string sql, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommand(sql, parameters);
        return ExecuteNonQuery(command, behavior, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and returns the number of affected rows.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of affected rows.</returns>
    public int ExecuteNonQuery(SQLiteCommand command, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(() =>
            {
                QuietOpen(out var open);
                try
                {
                    return command.ExecuteNonQuery(behavior);
                }
                catch (Exception ex)
                {
                    Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    throw;
                }
                finally
                {
                    QuietClose(open);
                }
            }, cancellationToken);
    }

    #endregion

    #region ExecuteReader

    /// <summary>
    /// Executes the specified SQL query and invokes an action for each returned row.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommand(sql, parameters);
        return ExecuteReader(command, read, behavior, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and invokes an action for each returned row.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of rows read.</returns>
    public int ExecuteReader(SQLiteCommand command, Action<SQLiteDataReader>? read, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(() =>
            {
                int count = 0;
                QuietOpen(out var open);
                try
                {
                    using var reader = command.ExecuteReader(behavior);
                    while (reader.Read())
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            read?.Invoke(reader);
                        }
                        catch
                        {
                            command.Cancel();
                            throw;
                        }
                        count++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
                    throw;
                }
                finally
                {
                    QuietClose(open);
                }
                return count;
            }, cancellationToken);
    }

    #endregion

    #region ExecuteScalar

    /// <summary>
    /// Executes the specified SQL query and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public object? ExecuteScalar(string sql, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public object? ExecuteScalar(string sql, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommand(sql, parameters);
        return ExecuteScalar(command, behavior, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    public object? ExecuteScalar(SQLiteCommand command, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(() =>
        {
            QuietOpen(out var open);
            try
            {
                return command.ExecuteScalar(behavior);
            }
            catch (Exception ex)
            {
                Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
                throw;
            }
            finally
            {
                QuietClose(open);
            }
        }, cancellationToken);
    }

    #endregion

    #endregion
}