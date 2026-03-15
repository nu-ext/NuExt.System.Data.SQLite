using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Methods

    public SQLiteDbTransaction BeginTransaction()
    {
        CheckDisposed();
        Debug.Assert(TransactionCount == 0, "Transaction count should be zero before starting a new transaction.");
        Throw.InvalidOperationExceptionIf(InTransaction, "SQLite does not support nested transactions");
        try
        {
            return new SQLiteDbTransaction(this, _conn.BeginTransaction());
        }
        catch (Exception ex)
        {
            Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
            throw;
        }
    }

    public SQLiteDbTransaction BeginTransaction(IsolationLevel isolationLevel)
    {
        CheckDisposed();
        Debug.Assert(TransactionCount == 0, "Transaction count should be zero before starting a new transaction.");
        Throw.InvalidOperationExceptionIf(InTransaction, "SQLite does not support nested transactions");
        try
        {
            return new SQLiteDbTransaction(this, _conn.BeginTransaction(isolationLevel));
        }
        catch (Exception ex)
        {
            Debug.Assert(_assertsDisabled, ex.GetType() + ": " + ex.Message);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SQLiteDbTransaction BeginTransactionDeferred()
    {
        return BeginTransaction(IsolationLevel.ReadCommitted);
    }

    #endregion
}
