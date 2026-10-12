using System.ComponentModel;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
#if DEBUG
    #region Internal classes

    private sealed class AssertsSuspender : IDisposable
    {
        private SQLiteDbConnection? _connection;

        public AssertsSuspender(SQLiteDbConnection connection)
        {
            _connection = connection;
            Interlocked.Increment(ref connection._assertsSuspendCount);
        }

        public void Dispose()
        {
            SQLiteDbConnection? connection = Interlocked.Exchange(ref _connection, null);
            if (connection is not null)
            {
                int count = Interlocked.Decrement(ref connection._assertsSuspendCount);
                System.Diagnostics.Debug.Assert(count >= 0, $"{nameof(AssertsSuspender)} was disposed without a matching suspension.");
            }
        }
    }

    #endregion

    private int _assertsSuspendCount;
#endif

    #region Properties

    /// <summary>
    /// Gets a value indicating whether diagnostic assertions are currently disabled.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if DEBUG
    public bool AssertsDisabled => Volatile.Read(ref _assertsSuspendCount) > 0;
#else
    public bool AssertsDisabled => false;
#endif

    #endregion

    #region Methods

    /// <summary>
    /// Temporarily disables diagnostic assertions until the returned object is disposed.
    /// </summary>
    /// <returns>
    /// An object that restores diagnostic assertions when disposed, or <see langword="null"/> in release builds.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IDisposable? SuspendAsserts()
    {
#if DEBUG
        return new AssertsSuspender(this);
#else
        return null;
#endif
    }

    #endregion
}