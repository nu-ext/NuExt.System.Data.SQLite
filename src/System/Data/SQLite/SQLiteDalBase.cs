using System.Diagnostics;

namespace System.Data.SQLite;

/// <summary>
/// Represents an abstract base class for SQLite-specific Data Access Layer (DAL) operations,
/// providing a framework for database context management and execution of SQLite database-related actions.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SQLiteDalBase"/> class with the specified connection creation function.
/// </remarks>
/// <param name="createConnection">A function that creates a new <see cref="SQLiteDbConnection"/>.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="createConnection"/> is null.</exception>
public abstract class SQLiteDalBase(Func<SQLiteDbConnection> createConnection) : DalBase<SQLiteDbContext>
{

    #region Properties

    /// <summary>
    /// Gets the function used to create a new <see cref="SQLiteDbConnection"/>.
    /// </summary>
    protected Func<SQLiteDbConnection> CreateConnection { get; } = createConnection ?? throw new ArgumentNullException(nameof(createConnection));

    #endregion

    #region Methods

    /// <summary>
    /// Creates and initializes an instance of the <see cref="SQLiteDbContext"/>.
    /// This method is not supported in <see cref="SQLiteDalBase"/>.
    /// </summary>
    /// <returns>An instance of <see cref="SQLiteDbContext"/>.</returns>
    /// <exception cref="NotSupportedException">Always thrown as this method is not supported.</exception>
    protected sealed override SQLiteDbContext CreateDbContext()
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Executes an action within a SQLite database context, ensuring that the context is managed (created, committed, or rolled back) accordingly.
    /// </summary>
    /// <param name="context">Optional existing context.</param>
    /// <param name="action">The action to perform within the context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the method is called with a context that does not have an acquired connection.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="action"/> is null.
    /// </exception>
    protected override ValueTask TryExecuteInDbContextAsync(SQLiteDbContext? context, Action<SQLiteDbContext> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (context == null)
        {
            using var connection = CreateConnection();
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);
                try
                {
                    action(dbContext);
                    dbContext.Commit();
                }
                catch
                {
                    dbContext.Rollback();
                    throw;
                }
            }, cancellationToken);
            return default;
        }
        Debug.Assert(context.Connection.IsAcquired, $"{nameof(context.Connection)} is not acquired.");
        Throw.InvalidOperationExceptionIf(context.Connection.IsAcquired == false, $"{nameof(context.Connection)} is not acquired.");
        return base.TryExecuteInDbContextAsync(context, action, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a function within a SQLite database context, ensuring that the context is managed (created, committed, or rolled back) accordingly.
    /// </summary>
    /// <param name="context">Optional existing context.</param>
    /// <param name="action">The asynchronous function to perform within the context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the method is called with a context that does not have an acquired connection.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="action"/> is null.
    /// </exception>
    protected override async ValueTask TryExecuteInDbContextAsync(SQLiteDbContext? context, Func<SQLiteDbContext, ValueTask> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (context == null)
        {
            using var connection = CreateConnection();
            await connection.AcquireLockAsync(async () =>
            {
                using var dbContext = new SQLiteDbContext(connection);
                try
                {
                    await action(dbContext);
                    dbContext.Commit();
                }
                catch
                {
                    dbContext.Rollback();
                    throw;
                }
            }, cancellationToken);
            return;
        }
        Debug.Assert(context.Connection.IsAcquired, $"{nameof(context.Connection)} is not acquired.");
        Throw.InvalidOperationExceptionIf(context.Connection.IsAcquired == false, $"{nameof(context.Connection)} is not acquired.");
        await base.TryExecuteInDbContextAsync(context, action, cancellationToken);
    }

    /// <summary>
    /// Executes a function within a SQLite database context and returns a result, ensuring that the context is managed (created, committed, or rolled back) accordingly.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the function.</typeparam>
    /// <param name="context">Optional existing context.</param>
    /// <param name="func">The function to perform within the context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{T}"/> containing the result of the function.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the method is called with a context that does not have an acquired connection.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="func"/> is null.
    /// </exception>
    protected override ValueTask<T> TryExecuteInDbContextAsync<T>(SQLiteDbContext? context, Func<SQLiteDbContext, T> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (context == null)
        {
            using var connection = CreateConnection();
            var result = connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);
                try
                {
                    var res = func(dbContext);
                    dbContext.Commit();
                    return res;
                }
                catch
                {
                    dbContext.Rollback();
                    throw;
                }
            }, cancellationToken);
            return new ValueTask<T>(result);
        }
        Debug.Assert(context.Connection.IsAcquired, $"{nameof(context.Connection)} is not acquired.");
        Throw.InvalidOperationExceptionIf(context.Connection.IsAcquired == false, $"{nameof(context.Connection)} is not acquired.");
        return base.TryExecuteInDbContextAsync(context, func, cancellationToken);
    }

    /// <summary>
    /// Executes a function asynchronously within a SQLite database context and returns a result,
    /// ensuring that the context is managed (created, committed, or rolled back) accordingly.
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the function.</typeparam>
    /// <param name="context">An optional existing database context. If null, a new context will be created.</param>
    /// <param name="func">The asynchronous function to execute within the database context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{T}"/> containing the result of the function.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the method is called with a context that does not have an acquired connection.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="func"/> is null.
    /// </exception>
    protected override async ValueTask<T> TryExecuteInDbContextAsync<T>(SQLiteDbContext? context, Func<SQLiteDbContext, ValueTask<T>> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (context == null)
        {
            using var connection = CreateConnection();
            var result = await connection.AcquireLockAsync(async () =>
            {
                using var dbContext = new SQLiteDbContext(connection);
                try
                {
                    var res = await func(dbContext);
                    dbContext.Commit();
                    return res;
                }
                catch
                {
                    dbContext.Rollback();
                    throw;
                }
            }, cancellationToken);
            return result;
        }
        Debug.Assert(context.Connection.IsAcquired, $"{nameof(context.Connection)} is not acquired.");
        Throw.InvalidOperationExceptionIf(context.Connection.IsAcquired == false, $"{nameof(context.Connection)} is not acquired.");
        return await base.TryExecuteInDbContextAsync(context, func, cancellationToken);
    }

    #endregion
}
