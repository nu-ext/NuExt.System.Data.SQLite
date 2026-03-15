using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Methods

    #region CreateCommand

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteCommand CreateCommand()
    {
        return _conn.CreateCommand();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteCommand CreateCommand(string sql, params SQLiteParameter[] parameters)
    {
        SQLiteCommand command = CreateCommand();
        command.CommandText = sql;
        if (parameters is { Length: > 0 })
        {
            Debug.Assert(new HashSet<string>(parameters.Select(p => p.ParameterName)).Count == parameters.Length);
            command.Parameters.AddRange(parameters);
        }
        return command;
    }

    public SQLiteCommand CreateCommandInsert(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        if (fields.Count() != parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandInsert)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandInsert)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"INSERT INTO \"{tableName}\" (");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}\"{field}\"");
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

    public SQLiteCommand CreateCommandInsertOrIgnore(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        if (fields.Count() != parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandInsertOrIgnore)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandInsertOrIgnore)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"INSERT OR IGNORE INTO \"{tableName}\" (");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}\"{field}\"");
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

    public SQLiteCommand CreateCommandInsertOrReplace(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        if (fields.Count() != parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandInsertOrReplace)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandInsertOrReplace)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"REPLACE INTO \"{tableName}\" (");//Alias for INSERT OR REPLACE
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}\"{field}\"");
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

    public SQLiteCommand CreateCommandSelect(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append("SELECT ");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}\"{field}\"");
            delimiter = ", ";
        }
        builder.Append($" FROM \"{tableName}\"");
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    public SQLiteCommand CreateCommandUpdate(string tableName, IEnumerable<string> fields, string? expr = null, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        if (fields.Count() > parameters.Length)
        {
            Debug.Assert(false, $"{nameof(CreateCommandUpdate)}: argument count mismatch");
            Throw.ArgumentException($"{nameof(CreateCommandUpdate)}: argument count mismatch");
        }
        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"UPDATE \"{tableName}\" SET ");
        string delimiter = string.Empty;
        foreach (string field in fields)
        {
            builder.Append($"{delimiter}\"{field}\"=@{field}");
            delimiter = ", ";
        }
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    public SQLiteCommand CreateCommandDelete(string tableName, string? expr = null, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
        builder.Append($"DELETE FROM \"{tableName}\"");
        if (!string.IsNullOrEmpty(expr))
        {
            builder.Append(' ');
            builder.Append(expr);
        }
        return CreateCommand(builder.ToString(), parameters);
    }

    #endregion

    #region ExecuteNonQuery

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteNonQuery(string sql, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);

        using var command = CreateCommand(sql, parameters);
        return ExecuteNonQuery(command, behavior, cancellationToken);
    }

    public int ExecuteNonQuery(SQLiteCommand command, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return AcquireLock(() =>
            {
                QuietOpen(out var open);
                try
                {
                    return command.ExecuteNonQuery(behavior);
                }
                catch (Exception ex)
                {
                    Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteReader(string sql, Action<SQLiteDataReader>? read, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);

        using var command = CreateCommand(sql, parameters);
        return ExecuteReader(command, read, behavior, cancellationToken);
    }

    public int ExecuteReader(SQLiteCommand command, Action<SQLiteDataReader>? read, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

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
                    Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? ExecuteScalar(string sql, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);

        using var command = CreateCommand(sql, parameters);
        return ExecuteScalar(command, behavior, cancellationToken);
    }

    public object? ExecuteScalar(SQLiteCommand command, CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return AcquireLock(() =>
        {
            QuietOpen(out var open);
            try
            {
                return command.ExecuteScalar(behavior);
            }
            catch (Exception ex)
            {
                Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
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
