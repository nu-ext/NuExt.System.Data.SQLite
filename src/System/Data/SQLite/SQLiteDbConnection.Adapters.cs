using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Methods

    #region CreateAdapter

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteDataAdapter CreateAdapter()
    {
        CheckDisposed();
        return new SQLiteDataAdapter();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteDataAdapter CreateAdapter(SQLiteCommand command)
    {
        CheckDisposed();
        Debug.Assert(command != null, $"{nameof(command)} is null");
        ArgumentNullException.ThrowIfNull(command);

        return new SQLiteDataAdapter() { SelectCommand = command };
    }

    #endregion

    #region Select

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DataTable Select(string tableName, IEnumerable<string> fields, string? expr = null, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        return Select(CreateCommandSelect(tableName, fields, expr, parameters), cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DataTable Select(string sql, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        return Select(CreateCommand(sql, parameters), cancellationToken);
    }

    public DataTable Select(SQLiteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var adapter = CreateAdapter();
        try
        {
            adapter.SelectCommand = command;
            var table = new DataTable();
            Fill(adapter, table, cancellationToken);
            return table;
        }
        catch (Exception ex)
        {
            Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
            throw;
        }
    }

    #endregion

    #region SelectForUpdate

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr = null, bool generateCommands = true, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        return SelectForUpdate(CreateCommandSelect(tableName, fields, expr, parameters), generateCommands, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, bool generateCommands = true, CancellationToken cancellationToken = default, params SQLiteParameter[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        return SelectForUpdate(CreateCommand(sql, parameters), generateCommands, cancellationToken);
    }

    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(SQLiteCommand command, bool generateCommands = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var adapter = CreateAdapter();
        try
        {
            adapter.SelectCommand = command;
            var table = new DataTable();
            Fill(adapter, table, cancellationToken);
            if (generateCommands)
            {
                var cmdBuilder = new SQLiteCommandBuilder(adapter);
#if DEBUG_
            var insertCommand = cmdBuilder.GetInsertCommand();
            var updateCommand = cmdBuilder.GetUpdateCommand();
            var deleteCommand = cmdBuilder.GetDeleteCommand();
            insertCommand?.Dispose();
            updateCommand?.Dispose();
            deleteCommand?.Dispose();
#endif
            }
            return (adapter, table);
        }
        catch (Exception ex)
        {
            Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
            adapter.Dispose();
            throw;
        }
    }

    #endregion

    #region Fill

    public void Fill(SQLiteDataAdapter adapter, DataTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(table);

        AcquireLock(() => adapter.Fill(table), cancellationToken);
    }

    #endregion

    #region Update

    public int Update(SQLiteDataAdapter adapter, DataTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(table);

        //Update requires a valid UpdateCommand when passed DataRow collection with modified rows.
        return AcquireLock(() => adapter.Update(table), cancellationToken);
    }

    #endregion

    #endregion
}
