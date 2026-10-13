using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using static System.Data.SQLite.SQLiteDbConnectionExtensions;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
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
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, params SQLiteParameter[] parameters)
#endif
    {
        return CreateCommandInsert(tableName, fields, null, parameters);
    }

    /// <summary>
    /// Creates an INSERT command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to insert.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, string? expr, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, string? expr, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyList<string> fieldList = fields as IReadOnlyList<string> ?? GetFieldList(fields);
        Throw.ArgumentExceptionIf(fieldList.Count == 0, $"{nameof(fields)} must contain at least one field.");

        return CreateCommand(CreateInsertCommandText("INSERT INTO ", tableName, fieldList, expr), parameters);
    }

    /// <summary>
    /// Creates an INSERT OR IGNORE command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to insert.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, params SQLiteParameter[] parameters)
#endif
    {
        return CreateCommandInsertOrIgnore(tableName, fields, null, parameters);
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
    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, string? expr, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, string? expr, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyList<string> fieldList = fields as IReadOnlyList<string> ?? GetFieldList(fields);
        Throw.ArgumentExceptionIf(fieldList.Count == 0, $"{nameof(fields)} must contain at least one field.");

        return CreateCommand(CreateInsertCommandText("INSERT OR IGNORE INTO ", tableName, fieldList, expr), parameters);
    }

    /// <summary>
    /// Creates an INSERT OR REPLACE command for the specified table and fields.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to insert.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, params SQLiteParameter[] parameters)
#endif
    {
        return CreateCommandInsertOrReplace(tableName, fields, null, parameters);
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
    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, string? expr, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, string? expr, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyList<string> fieldList = fields as IReadOnlyList<string> ?? GetFieldList(fields);
        Throw.ArgumentExceptionIf(fieldList.Count == 0, $"{nameof(fields)} must contain at least one field.");

        return CreateCommand(CreateInsertCommandText("REPLACE INTO ", tableName, fieldList, expr), parameters);
    }

    private static string CreateInsertCommandText(string commandPrefix, string tableName, IReadOnlyList<string> fields, string? expr)
    {
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append(commandPrefix);
        builder.Append(QuoteIdentifier(tableName));
        builder.Append(" (");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append(delimiter);
            builder.Append(QuoteIdentifier(field));
            delimiter = ", ";
        }
        builder.Append(") VALUES (");
        delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append(delimiter);
            builder.Append('@');
            builder.Append(field);
            delimiter = ", ";
        }
        builder.Append(')');
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return builder.ToString();
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
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyList<string> fieldList = fields as IReadOnlyList<string> ?? GetFieldList(fields);
        Throw.ArgumentExceptionIf(fieldList.Count == 0, $"{nameof(fields)} must contain at least one field.");

        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append("SELECT ");
        string delimiter = string.Empty;
        foreach (string field in fieldList)
        {
            builder.Append(delimiter);
            builder.Append(QuoteIdentifier(field));
            delimiter = ", ";
        }
        builder.Append(" FROM ");
        builder.Append(QuoteIdentifier(tableName));
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
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The created SQLite command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, params SQLiteParameter[] parameters)
#endif
    {
        return CreateCommandUpdate(tableName, fields, null, parameters);
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
    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, string? expr, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, string? expr, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyList<string> fieldList = fields as IReadOnlyList<string> ?? GetFieldList(fields);
        Throw.ArgumentExceptionIf(fieldList.Count == 0, $"{nameof(fields)} must contain at least one field.");

        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append("UPDATE ");
        builder.Append(QuoteIdentifier(tableName));
        builder.Append(" SET ");
        string delimiter = string.Empty;
        foreach (string field in fieldList)
        {
            builder.Append(delimiter);
            builder.Append(QuoteIdentifier(field));
            builder.Append("=@");
            builder.Append(field);
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
        builder.Append("DELETE FROM ");
        builder.Append(QuoteIdentifier(tableName));
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
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of affected rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteNonQuery(string sql, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteNonQuery(sql, CommandBehavior.Default, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL statement and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of affected rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteNonQuery(string sql, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteNonQuery(string sql, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteNonQuery(sql, CommandBehavior.Default, cancellationToken, parameters);
    }

    /// <summary>
    /// Executes the specified SQL statement and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of affected rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteNonQuery(string sql, CommandBehavior behavior, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteNonQuery(string sql, CommandBehavior behavior, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteNonQuery(sql, behavior, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL statement and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of affected rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteNonQuery(string sql, CommandBehavior behavior, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteNonQuery(string sql, CommandBehavior behavior, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
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
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The number of affected rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteNonQuery(SQLiteCommand command, CancellationToken cancellationToken)
    {
        return ExecuteNonQuery(command, CommandBehavior.Default, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and returns the number of affected rows.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The number of affected rows.</returns>
    public int ExecuteNonQuery(SQLiteCommand command, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(static state =>
        {
            state.Connection.QuietOpen(out var open);
            try
            {
                return state.Command.ExecuteNonQuery(state.Behavior);
            }
            catch (Exception ex)
            {
                Debug.Assert(state.Connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                throw;
            }
            finally
            {
                state.Connection.QuietClose(open);
            }
        }, (Connection: this, Command: command, Behavior: behavior), cancellationToken);
    }

    #endregion

    #region ExecuteReader

    /// <summary>
    /// Executes the specified SQL query and invokes an action for each returned row.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteReader(sql, read, CommandBehavior.Default, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action with the provided state for each returned row.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteReader(sql, read, state, CommandBehavior.Default, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action for each returned row.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteReader(sql, read, CommandBehavior.Default, cancellationToken, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action with the provided state for each returned row.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteReader(sql, read, state, CommandBehavior.Default, cancellationToken, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action for each returned row.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteReader(sql, read, behavior, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action with the provided state for each returned row.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, CommandBehavior behavior, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, CommandBehavior behavior, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteReader(sql, read, state, behavior, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action for each returned row.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommand(sql, parameters);
        return ExecuteReader(command, read, behavior, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQL query and invokes an action with the provided state for each returned row.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, CommandBehavior behavior, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public int ExecuteReader<TState>(string sql, Action<SQLiteDataReader, TState>? read, TState state, CommandBehavior behavior, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommand(sql, parameters);
        return ExecuteReader(command, read, state, behavior, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and invokes an action for each returned row.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteReader(SQLiteCommand command, Action<SQLiteDataReader>? read, CancellationToken cancellationToken)
    {
        return ExecuteReader(command, read, CommandBehavior.Default, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and invokes an action with the provided state for each returned row.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <returns>The number of rows read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteReader<TState>(SQLiteCommand command, Action<SQLiteDataReader, TState>? read, TState state, CancellationToken cancellationToken)
    {
        return ExecuteReader(command, read, state, CommandBehavior.Default, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and invokes an action for each returned row.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <returns>The number of rows read.</returns>
    public int ExecuteReader(SQLiteCommand command, Action<SQLiteDataReader>? read, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(static context =>
        {
            int count = 0;
            context.Connection.QuietOpen(out var open);
            try
            {
                using var reader = context.Command.ExecuteReader(context.Behavior);
                while (reader.Read())
                {
                    try
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        context.Read?.Invoke(reader);
                    }
                    catch
                    {
                        context.Command.Cancel();
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
                Debug.Assert(context.Connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                throw;
            }
            finally
            {
                context.Connection.QuietClose(open);
            }
            return count;
        }, (Connection: this, Command: command, Read: read, Behavior: behavior, CancellationToken: cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and invokes an action with the provided state for each returned row.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="read">The action to invoke for each returned row.</param>
    /// <param name="state">The state to pass to the action.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock and reading rows.</param>
    /// <returns>The number of rows read.</returns>
    public int ExecuteReader<TState>(SQLiteCommand command, Action<SQLiteDataReader, TState>? read, TState state, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(static context =>
        {
            int count = 0;
            context.Connection.QuietOpen(out var open);
            try
            {
                using var reader = context.Command.ExecuteReader(context.Behavior);
                while (reader.Read())
                {
                    try
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        context.Read?.Invoke(reader, context.State);
                    }
                    catch
                    {
                        context.Command.Cancel();
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
                Debug.Assert(context.Connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                throw;
            }
            finally
            {
                context.Connection.QuietClose(open);
            }
            return count;
        }, (Connection: this, Command: command, Read: read, State: state, Behavior: behavior, CancellationToken: cancellationToken), cancellationToken);
    }

    #endregion

    #region ExecuteScalar

    /// <summary>
    /// Executes the specified SQL query and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public object? ExecuteScalar(string sql, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public object? ExecuteScalar(string sql, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteScalar(sql, CommandBehavior.Default, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public object? ExecuteScalar(string sql, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public object? ExecuteScalar(string sql, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteScalar(sql, CommandBehavior.Default, cancellationToken, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public object? ExecuteScalar(string sql, CommandBehavior behavior, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public object? ExecuteScalar(string sql, CommandBehavior behavior, params SQLiteParameter[] parameters)
#endif
    {
        return ExecuteScalar(sql, behavior, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified SQL query and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public object? ExecuteScalar(string sql, CommandBehavior behavior, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public object? ExecuteScalar(string sql, CommandBehavior behavior, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
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
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? ExecuteScalar(SQLiteCommand command, CancellationToken cancellationToken)
    {
        return ExecuteScalar(command, CommandBehavior.Default, cancellationToken);
    }

    /// <summary>
    /// Executes the specified SQLite command and returns the first column of the first row in the result set.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="behavior">The command behavior.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The first column of the first row in the result set, or <see langword="null"/> if the result set is empty.</returns>
    public object? ExecuteScalar(SQLiteCommand command, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        return AcquireLock(static state =>
        {
            state.Connection.QuietOpen(out var open);
            try
            {
                return state.Command.ExecuteScalar(state.Behavior);
            }
            catch (Exception ex)
            {
                Debug.Assert(state.Connection.AssertsDisabled, ex.GetType() + ": " + ex.Message);
                throw;
            }
            finally
            {
                state.Connection.QuietClose(open);
            }
        }, (Connection: this, Command: command, Behavior: behavior), cancellationToken);
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[] GetFieldList(IEnumerable<string> fields)
    {
        ValueListBuilder<string> builder = new ValueListBuilder<string>([null, null, null, null, null, null, null, null]);
        foreach (string field in fields)
        {
            builder.Append(field);
        }
        return builder.ToArray();
    }
}