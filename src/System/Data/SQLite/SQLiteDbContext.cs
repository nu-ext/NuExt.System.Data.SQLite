using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Data.SQLite;

/// <summary>
/// Represents a SQLite database context that provides a connection to the SQLite database
/// and supports transaction management through commit and rollback operations.
/// </summary>
public sealed class SQLiteDbContext : Disposable, IDbContext
{
    private readonly SQLiteDbTransaction _transaction;
    private readonly Lifetime _lifetime = new Lifetime()
#if DEBUG
            .SetDebugInfo()
#endif
        ;
    private readonly bool _checkAcquired;

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbContext"/> class with the specified SQLite database connection.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided connection is null.</exception>
    /// <exception cref="InvalidOperationException">The connection lock is not held or the connection already has an active transaction.</exception>
    public SQLiteDbContext(SQLiteDbConnection connection)
        : this(connection, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbContext"/> class with the specified SQLite database connection.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="checkAcquired">
    /// <see langword="true"/> to require the connection lock to be held for the lifetime of the context; otherwise, <see langword="false"/> when the caller guarantees exclusive connection access.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction, or <paramref name="checkAcquired"/> is <see langword="true"/> and the connection lock is not held.</exception>
    public SQLiteDbContext(SQLiteDbConnection connection, bool checkAcquired)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _checkAcquired = checkAcquired;

        if (checkAcquired)
        {
            CheckAcquired();
        }

        Debug.Assert(!connection.InTransaction, $"{nameof(connection)} is in a transaction.");
        Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} is in a transaction.");

        try
        {
            _lifetime.Add(() => GC.SuppressFinalize(this));
            Connection.Open(out var originalState);
            _lifetime.Add(() => Connection.Close(originalState));
            _transaction = Connection.BeginTransaction();
            _lifetime.Add(_transaction.Dispose);
        }
        catch (Exception ex)
        {
            Debug.Assert(ex is SQLiteException && ex.Message.StartsWith("database is locked", StringComparison.Ordinal), ex.Message);
            _lifetime.Dispose();
            throw;
        }
    }

    #region Properties

    /// <summary>
    /// Gets the SQLite database connection associated with this context.
    /// </summary>
    public SQLiteDbConnection Connection { get; }

    /// <summary>
    /// Gets a value indicating whether the transaction can be committed or rolled back.
    /// </summary>
    public bool IsActive => _transaction.IsActive;

    /// <summary>
    /// Gets the database connection associated with this context.
    /// </summary>
    IDbConnection IDbContext.Connection => Connection;

    #endregion

    #region Methods

    /// <summary>
    /// Ensures that the database connection is acquired and not disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckAcquired()
    {
        bool isAcquired = Connection.IsAcquired;
        Debug.Assert(isAcquired, $"{nameof(Connection)} is not acquired.");
        Throw.InvalidOperationExceptionIf(!isAcquired, $"{nameof(Connection)} is not acquired.");
    }

    /// <summary>
    /// Commits all changes made in this context to the database.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The database context has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The transaction has already been completed, or connection lock checking is enabled and the lock is not held.</exception>
    public void Commit()
    {
        CheckDisposed();
        if (_checkAcquired)
        {
            CheckAcquired();
        }
        _transaction.Commit();
    }

    /// <summary>
    /// Disposes the resources used by this context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Connection lock checking is enabled and the lock is not held.</exception>
    protected override void DisposeCore()
    {
        if (_checkAcquired)
        {
            CheckAcquired();
        }

        try
        {
            _lifetime.Dispose();
        }
        finally
        {
            base.DisposeCore();
        }
    }

    /// <summary>
    /// Rolls back all changes made in this context.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The database context has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The transaction has already been completed, or connection lock checking is enabled and the lock is not held.</exception>
    public void Rollback()
    {
        CheckDisposed();
        if (_checkAcquired)
        {
            CheckAcquired();
        }
        _transaction.Rollback();
    }

    #endregion
}
