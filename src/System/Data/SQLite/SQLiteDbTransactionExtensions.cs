using System.Diagnostics;

namespace System.Data.SQLite;

/// <summary>
/// Provides extension methods for SQLite database transactions.
/// </summary>
public static class SQLiteDbTransactionExtensions
{
    /// <summary>
    /// Attempts to roll back the active transaction.
    /// </summary>
    /// <param name="transaction">The SQLite database transaction.</param>
    /// <param name="originalException">The exception that caused the rollback attempt, if any.</param>
    /// <returns>
    /// <see langword="true"/> if the transaction was rolled back;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <see langword="null"/>.</exception>
    public static bool TryRollback(this SQLiteDbTransaction transaction, Exception? originalException = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!transaction.IsActive)
        {
            return false;
        }

        try
        {
            transaction.Rollback();
            return true;
        }
        catch (Exception rollbackException)
        {
            string message = originalException is not null
                ? $"SQLite transaction rollback failed after '{originalException.GetType()}': {rollbackException}"
                : $"SQLite transaction rollback failed: {rollbackException}";

            Trace.WriteLine(message);
            Debug.Fail(message);
            return false;
        }
    }
}