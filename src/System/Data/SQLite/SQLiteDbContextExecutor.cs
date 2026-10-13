namespace System.Data.SQLite;

/// <summary>
/// Executes database operations within owned synchronized SQLite database contexts.
/// </summary>
internal sealed class SQLiteDbContextExecutor : IDbContextExecutor<SQLiteDbContext>
{
    private readonly Func<SQLiteDbConnection> _createConnection;

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbContextExecutor"/> class.
    /// </summary>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createConnection"/> is <see langword="null"/>.</exception>
    internal SQLiteDbContextExecutor(Func<SQLiteDbConnection> createConnection)
    {
        _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
    }

    /// <inheritdoc/>
    public ValueTask ExecuteAsync(Action<SQLiteDbContext> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _createConnection();
        connection.AcquireLock(static state =>
        {
            using var context = new SQLiteDbContext(state.Connection);
            try
            {
                state.Action(context);
                context.Commit();
            }
            catch (Exception ex)
            {
                context.TryRollback(ex);
                throw;
            }
        }, (Connection: connection, Action: action), cancellationToken);

        return default;
    }

    /// <inheritdoc/>
    public async ValueTask ExecuteAsync(Func<SQLiteDbContext, ValueTask> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _createConnection();
        await connection.AcquireLockAsync(static async state =>
        {
            using var context = new SQLiteDbContext(state.Connection);
            try
            {
                await state.Action(context).ConfigureAwait(false);
                context.Commit();
            }
            catch (Exception ex)
            {
                context.TryRollback(ex);
                throw;
            }
        }, (Connection: connection, Action: action), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<T> ExecuteAsync<T>(Func<SQLiteDbContext, T> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _createConnection();
        T result = connection.AcquireLock(static state =>
        {
            using var context = new SQLiteDbContext(state.Connection);
            try
            {
                T value = state.Func(context);
                context.Commit();
                return value;
            }
            catch (Exception ex)
            {
                context.TryRollback(ex);
                throw;
            }
        }, (Connection: connection, Func: func), cancellationToken);

        return new ValueTask<T>(result);
    }

    /// <inheritdoc/>
    public async ValueTask<T> ExecuteAsync<T>(Func<SQLiteDbContext, ValueTask<T>> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _createConnection();
        return await connection.AcquireLockAsync(static async state =>
        {
            using var context = new SQLiteDbContext(state.Connection);
            try
            {
                T result = await state.Func(context).ConfigureAwait(false);
                context.Commit();
                return result;
            }
            catch (Exception ex)
            {
                context.TryRollback(ex);
                throw;
            }
        }, (Connection: connection, Func: func), cancellationToken).ConfigureAwait(false);
    }
}