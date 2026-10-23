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
    private const int COMMITTING = 1;
    private const int COMMITTED = 2;
    private const int ROLLING_BACK = 3;
    private const int ROLLED_BACK = 4;
    private const int FAULTED = 5;

    #endregion

    private readonly SQLiteDbConnection _connection;
    private readonly SQLiteTransaction _transaction;

    private int _state = NONE;

#if DEBUG
    private readonly Stopwatch _watch = Stopwatch.StartNew();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbTransaction"/> class with the specified connection and underlying transaction.
    /// </summary>
    /// <param name="connection">The SQLite database connection.</param>
    /// <param name="transaction">The underlying SQLite transaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="transaction"/> is <see langword="null"/>.</exception>
    internal SQLiteDbTransaction(SQLiteDbConnection connection, SQLiteTransaction transaction)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));

        Interlocked.Increment(ref _connection.TransactionCount);
    }

    #region Properties

    /// <summary>
    /// Gets the database connection associated with this transaction.
    /// </summary>
    public IDbConnection Connection => _transaction.Connection;

    /// <summary>
    /// Gets the isolation level of this transaction.
    /// </summary>
    public IsolationLevel IsolationLevel => _transaction.IsolationLevel;

    /// <summary>
    /// Gets a value indicating whether the transaction can be committed or rolled back.
    /// </summary>
    public bool IsActive => !IsDisposingOrDisposed && Volatile.Read(ref _state) == NONE;

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
        Debug.Assert(ReferenceEquals(command.Connection, _transaction.Connection), "The command belongs to a different connection.");
        Throw.InvalidOperationExceptionIf(!ReferenceEquals(command.Connection, _transaction.Connection), "The command belongs to a different connection.");
        command.Transaction = _transaction;
    }

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">The transaction has already been committed, rolled back, or is being completed.</exception>
    public void Commit()
    {
        CheckDisposed();

        int originalState = Interlocked.CompareExchange(ref _state, COMMITTING, NONE);
        Debug.Assert(originalState == NONE, $"Trying to commit with state: {originalState}.");
        Throw.InvalidOperationExceptionIf(originalState != NONE, $"Trying to commit with state: {originalState}.");

        try
        {
            _transaction.Commit();
            Volatile.Write(ref _state, COMMITTED);
        }
        catch
        {
            Volatile.Write(ref _state, FAULTED);
            throw;
        }
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
            if (elapsed.TotalSeconds >= _connection.DefaultTimeout)
            {
                Trace.WriteLine($"Long-running SQLite transaction: {elapsed}.");
                Debug.Fail($"Long-running SQLite transaction: {elapsed}.");
            }
#endif
            base.DisposeCore();
        }
    }

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">The transaction has already been committed, rolled back, or is being completed.</exception>
    public void Rollback()
    {
        CheckDisposed();

        int originalState = Interlocked.CompareExchange(ref _state, ROLLING_BACK, NONE);
        Debug.Assert(originalState == NONE, $"Trying to roll back with state: {originalState}.");
        Throw.InvalidOperationExceptionIf(originalState != NONE, $"Trying to roll back with state: {originalState}.");

        try
        {
            _transaction.Rollback();
            Volatile.Write(ref _state, ROLLED_BACK);
        }
        catch
        {
            Volatile.Write(ref _state, FAULTED);
            throw;
        }
    }

    #endregion
}
