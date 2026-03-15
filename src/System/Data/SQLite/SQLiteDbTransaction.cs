using System.ComponentModel;
using System.Diagnostics;

namespace System.Data.SQLite;

/// <summary>
/// Represents a SQLite database transaction that provides methods for committing and rolling back transactions.
/// </summary>
public sealed class SQLiteDbTransaction : Disposable, IDbTransaction
{
    #region State
    private const int NONE = 0;
    private const int COMMIT = 1;
    private const int ROLLBACK = 2;
    #endregion

    private readonly SQLiteDbConnection _connection;
    private readonly SQLiteTransaction _transaction;

    private int _state = NONE;

#if DEBUG
    private readonly Stopwatch _watch = Stopwatch.StartNew();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbTransaction"/> class with the specified connection, transaction, and disposal action.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="transaction">The underlying SQLite transaction.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided connection or transaction is null.</exception>
    internal SQLiteDbTransaction(SQLiteDbConnection connection, SQLiteTransaction transaction)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Interlocked.Increment(ref _connection.TransactionCount);
        Debug.Assert(connection.DefaultTimeout == 30);
    }

    #region Propertties

    /// <summary>
    /// Gets the database connection associated with this transaction.
    /// </summary>
    public IDbConnection Connection => _transaction.Connection;

    /// <summary>
    /// Gets the isolation level of this transaction.
    /// </summary>
    public IsolationLevel IsolationLevel => _transaction.IsolationLevel;

    #endregion

    #region Methods

    /// <summary>
    /// Applies the current transaction to the specified SQLite command.
    /// </summary>
    /// <param name="command">The SQLite command to apply the transaction to.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ApplyToCommand(SQLiteCommand command)
    {
        CheckDisposed();
        Debug.Assert(command != null, $"{nameof(command)} is null");
        ArgumentNullException.ThrowIfNull(command);

        command.Transaction = _transaction;
    }

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    public void Commit()
    {
        CheckDisposed();
        var originalState = Interlocked.CompareExchange(ref _state, COMMIT, NONE);
        Debug.Assert(originalState == NONE, $"Trying commit with state: {originalState}");
        Throw.InvalidOperationExceptionIf(originalState != NONE, $"Trying commit with state: {originalState}");
        _transaction.Commit();
    }

    /// <summary>
    /// Disposes the resources used by this transaction.
    /// </summary>
    protected override void DisposeCore()
    {
        try
        {
            _transaction.Dispose();
        }
        finally
        {
            Interlocked.Decrement(ref _connection.TransactionCount);
#if DEBUG
            var elapsed = _watch.Elapsed;
            var timeout = _connection.DefaultTimeout;
            Debug.Assert(elapsed.TotalSeconds < timeout, $"Transaction elapsed: {elapsed}, Expected: {timeout} sec");
#endif
            base.DisposeCore();
        }
    }

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    public void Rollback()
    {
        CheckDisposed();
        var originalState = Interlocked.CompareExchange(ref _state, ROLLBACK, NONE);
        Debug.Assert(originalState == NONE, $"Trying rollback with state: {originalState}");
        Throw.InvalidOperationExceptionIf(originalState != NONE, $"Trying rollback with state: {originalState}");
        _transaction.Rollback();
    }

    #endregion
}
