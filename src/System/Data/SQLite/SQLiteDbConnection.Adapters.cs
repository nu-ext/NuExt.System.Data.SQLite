using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region CreateAdapter

    /// <summary>
    /// Creates a new SQLite data adapter.
    /// </summary>
    /// <returns>The created SQLite data adapter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteDataAdapter CreateAdapter()
    {
        CheckDisposed();
        return new SQLiteDataAdapter();
    }

    /// <summary>
    /// Creates a new SQLite data adapter with the specified select command.
    /// </summary>
    /// <param name="command">The select command to assign to the adapter.</param>
    /// <returns>The created SQLite data adapter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteDataAdapter CreateAdapter(SQLiteCommand command)
    {
        CheckDisposed();
        Debug.Assert(command != null, $"{nameof(command)} is null");
        ArgumentNullException.ThrowIfNull(command);

        return new SQLiteDataAdapter { SelectCommand = command };
    }

    #endregion

    #region Select

    /// <summary>
    /// Selects data from the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>A table containing the selected data.</returns>
    public DataTable Select(string tableName, IEnumerable<string> fields, CancellationToken cancellationToken = default)
    {
        return Select(tableName, fields, null, cancellationToken);
    }

    /// <summary>
    /// Selects data from the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>A table containing the selected data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public DataTable Select(string tableName, IEnumerable<string> fields, string? expr, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public DataTable Select(string tableName, IEnumerable<string> fields, string? expr, params SQLiteParameter[] parameters)
#endif
    {
        return Select(tableName, fields, expr, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Selects data from the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>A table containing the selected data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public DataTable Select(string tableName, IEnumerable<string> fields, string? expr, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public DataTable Select(string tableName, IEnumerable<string> fields, string? expr, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommandSelect(tableName, fields, expr, parameters);
        return Select(command, cancellationToken);
    }

    /// <summary>
    /// Executes the specified query and returns the selected data.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>A table containing the selected data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public DataTable Select(string sql, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public DataTable Select(string sql, params SQLiteParameter[] parameters)
#endif
    {
        return Select(sql, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified query and returns the selected data.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>A table containing the selected data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public DataTable Select(string sql, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public DataTable Select(string sql, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        using var command = CreateCommand(sql, parameters);
        return Select(command, cancellationToken);
    }

    /// <summary>
    /// Executes the specified command and returns the selected data.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>A table containing the selected data.</returns>
    public DataTable Select(SQLiteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

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
            Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
            throw;
        }
    }

    #endregion

    #region SelectForUpdate

    /// <summary>
    /// Selects data from the specified table and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, CancellationToken cancellationToken = default)
    {
        return SelectForUpdate(tableName, fields, null, true, cancellationToken);
    }

    /// <summary>
    /// Selects data from the specified table and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="generateCommands"><see langword="true"/> to generate insert, update, and delete commands; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, bool generateCommands, CancellationToken cancellationToken = default)
    {
        return SelectForUpdate(tableName, fields, null, generateCommands, cancellationToken);
    }

    /// <summary>
    /// Selects data from the specified table and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, params SQLiteParameter[] parameters)
#endif
    {
        return SelectForUpdate(tableName, fields, expr, true, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Selects data from the specified table and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        return SelectForUpdate(tableName, fields, expr, true, cancellationToken, parameters);
    }

    /// <summary>
    /// Selects data from the specified table and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="generateCommands"><see langword="true"/> to generate insert, update, and delete commands; otherwise, <see langword="false"/>.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, bool generateCommands, params ReadOnlySpan<SQLiteParameter> parameters)
#else
public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, bool generateCommands, params SQLiteParameter[] parameters)
#endif
    {
        return SelectForUpdate(tableName, fields, expr, generateCommands, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Selects data from the specified table and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="fields">The fields to select.</param>
    /// <param name="expr">An optional SQL expression appended to the command.</param>
    /// <param name="generateCommands"><see langword="true"/> to generate insert, update, and delete commands; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, bool generateCommands, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string tableName, IEnumerable<string> fields, string? expr, bool generateCommands, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        cancellationToken.ThrowIfCancellationRequested();

        return SelectForUpdate(CreateCommandSelect(tableName, fields, expr, parameters), generateCommands, cancellationToken);
    }

    /// <summary>
    /// Executes the specified query and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, params SQLiteParameter[] parameters)
#endif
    {
        return SelectForUpdate(sql, true, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified query and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        return SelectForUpdate(sql, true, cancellationToken, parameters);
    }

    /// <summary>
    /// Executes the specified query and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="generateCommands"><see langword="true"/> to generate insert, update, and delete commands; otherwise, <see langword="false"/>.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, bool generateCommands, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, bool generateCommands, params SQLiteParameter[] parameters)
#endif
    {
        return SelectForUpdate(sql, generateCommands, CancellationToken.None, parameters);
    }

    /// <summary>
    /// Executes the specified query and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="generateCommands"><see langword="true"/> to generate insert, update, and delete commands; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <param name="parameters">The parameters to add to the command.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, bool generateCommands, CancellationToken cancellationToken, params ReadOnlySpan<SQLiteParameter> parameters)
#else
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(string sql, bool generateCommands, CancellationToken cancellationToken, params SQLiteParameter[] parameters)
#endif
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        cancellationToken.ThrowIfCancellationRequested();

        return SelectForUpdate(CreateCommand(sql, parameters), generateCommands, cancellationToken);
    }

    /// <summary>
    /// Executes the specified command and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(SQLiteCommand command, CancellationToken cancellationToken)
    {
        return SelectForUpdate(command, true, cancellationToken);
    }

    /// <summary>
    /// Executes the specified command and creates an adapter that can update the returned data.
    /// </summary>
    /// <param name="command">The SQLite command to execute.</param>
    /// <param name="generateCommands"><see langword="true"/> to generate insert, update, and delete commands; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>
    /// The configured data adapter and selected data table.
    /// The caller is responsible for disposing the returned adapter.
    /// </returns>
    public (SQLiteDataAdapter Adapter, DataTable Table) SelectForUpdate(SQLiteCommand command, bool generateCommands = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

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
            Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
            adapter.Dispose();
            throw;
        }
    }

    #endregion

    #region Fill

    /// <summary>
    /// Fills the specified data table using the data adapter.
    /// </summary>
    /// <param name="adapter">The data adapter used to retrieve data.</param>
    /// <param name="table">The data table to fill.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    public void Fill(SQLiteDataAdapter adapter, DataTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(table);
        cancellationToken.ThrowIfCancellationRequested();

        AcquireLock(static state => state.Adapter.Fill(state.Table), (Adapter: adapter, Table: table), cancellationToken);
    }

    #endregion

    #region Update

    /// <summary>
    /// Updates the database using the changes in the specified data table.
    /// </summary>
    /// <param name="adapter">The data adapter used to update the database.</param>
    /// <param name="table">The data table containing the changes.</param>
    /// <param name="cancellationToken">A token to observe while waiting to acquire the connection lock.</param>
    /// <returns>The number of rows successfully updated.</returns>
    public int Update(SQLiteDataAdapter adapter, DataTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(table);
        cancellationToken.ThrowIfCancellationRequested();

        // Update requires a valid UpdateCommand when passed a DataRow collection with modified rows.
        return AcquireLock(static state => state.Adapter.Update(state.Table), (Adapter: adapter, Table: table), cancellationToken);
    }

    #endregion
}
