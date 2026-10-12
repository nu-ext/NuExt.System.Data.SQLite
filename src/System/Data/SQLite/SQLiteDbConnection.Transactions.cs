using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Methods

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for preventing concurrent use of the connection for the lifetime of the transaction. Use <see cref="AcquireLock(Action, CancellationToken)"/> or <c>ExecuteInTransaction</c> when coordinated access is required.
    /// </remarks>
    /// <returns>The created SQLite transaction.</returns>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public SQLiteDbTransaction BeginTransaction()
    {
        CheckDisposed();
        Debug.Assert(TransactionCount == 0, "Transaction count should be zero before starting a new transaction.");
        Throw.InvalidOperationExceptionIf(InTransaction, "SQLite does not support nested transactions.");

        try
        {
            return new SQLiteDbTransaction(this, _conn.BeginTransaction());
        }
        catch (Exception ex)
        {
            Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Begins a database transaction with the specified isolation level.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for preventing concurrent use of the connection for the lifetime of the transaction. Use <see cref="AcquireLock(Action, CancellationToken)"/> or <c>ExecuteInTransaction</c> when coordinated access is required.
    /// </remarks>
    /// <param name="isolationLevel">The isolation level of the transaction.</param>
    /// <returns>The created SQLite transaction.</returns>
    /// <exception cref="InvalidOperationException">The connection already has an active transaction.</exception>
    public SQLiteDbTransaction BeginTransaction(IsolationLevel isolationLevel)
    {
        CheckDisposed();
        Debug.Assert(TransactionCount == 0, "Transaction count should be zero before starting a new transaction.");
        Throw.InvalidOperationExceptionIf(InTransaction, "SQLite does not support nested transactions.");

        try
        {
            return new SQLiteDbTransaction(this, _conn.BeginTransaction(isolationLevel));
        }
        catch (Exception ex)
        {
            Debug.Assert(AssertsDisabled, ex.GetType() + ": " + ex.Message);
            throw;
        }
    }

    /// <inheritdoc cref="BeginTransaction(IsolationLevel)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteDbTransaction BeginTransactionDeferred()
    {
        return BeginTransaction(IsolationLevel.ReadCommitted);
    }

    #endregion
}
