using System.ComponentModel;
using System.Diagnostics;

namespace System.Data.SQLite;

/// <summary>
/// Provides a wrapper for SQLite connections with synchronized database access.
/// Database operations can use a shared process-local lock to coordinate concurrent
/// access to the same database.
/// Implements the <see cref="IDbConnection"/> interface and extends
/// <see cref="Disposable"/> for resource management.
/// </summary>
public sealed partial class SQLiteDbConnection : Disposable, IDbConnection
{
    private readonly SQLiteConnection _conn;
    private SQLiteDbConnectionFactory.AsyncLockCounter? _syncRoot;
    internal volatile int TransactionCount;

    internal const int StackallocCharBufferSizeLimit = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbConnection"/> class with the specified connection string and shared locking enabled.
    /// </summary>
    /// <param name="connectionString">The connection string to use.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or empty.</exception>
    public SQLiteDbConnection(string connectionString) : this(connectionString, true)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbConnection"/> class with the specified connection string and locking mode.
    /// </summary>
    /// <param name="connectionString">The connection string to use.</param>
    /// <param name="useSharedLock"><see langword="true"/> to use a shared process-local lock for connections to the same database; otherwise, <see langword="false"/> to use a connection-specific lock.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or empty.</exception>
    public SQLiteDbConnection(string connectionString, bool useSharedLock)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        var csb = new SQLiteConnectionStringBuilder(connectionString); // Throws if the connection string is invalid.

        _conn = new SQLiteConnection(connectionString);
        UseSharedLock = useSharedLock;
        if (useSharedLock)
        {
            _syncRoot = SQLiteDbConnectionFactory.Instance.GetOrCreateSyncObj(csb.DataSource);
        }
        else
        {
            _syncRoot = new SQLiteDbConnectionFactory.AsyncLockCounter(csb.DataSource) { ReferenceCount = 1 };
        }
    }

    #region Properties

    /// <summary>
    /// The connection string containing the parameters for the connection.
    /// </summary>
    public string ConnectionString => _conn.ConnectionString;

    /// <summary>
    /// Gets/sets the default command timeout for newly-created commands.
    /// </summary>
    public int DefaultTimeout
    {
        get => _conn.DefaultTimeout;
        set => _conn.DefaultTimeout = value;
    }

    /// <summary>
    /// Gets a value indicating whether the connection has an active transaction.
    /// </summary>
    public bool InTransaction => TransactionCount > 0;

    /// <summary>
    /// Determines whether the current connection holds the database lock.
    /// </summary>
    public bool IsAcquired
    {
        get
        {
            return GetSyncRoot().Mutex.IsHeldByCurrentFlow;
        }
    }

    /// <summary>
    /// Gets a value indicating the open or closed status of the connection.
    /// </summary>
    public bool IsOpen => _conn.State == ConnectionState.Open;

    /// <summary>
    /// Returns the rowid of the most recent successful INSERT into the database from this connection.
    /// </summary>
    public long LastInsertRowId => _conn.LastInsertRowId;

    /// <summary>
    /// Gets the number of shared database locks currently maintained by the connection factory.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int SharedLockCount => SQLiteDbConnectionFactory.Instance.SharedLockCount;

    /// <summary>
    /// Gets the database identifiers for which shared locks are currently maintained.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyCollection<string> SharedLockKeys => SQLiteDbConnectionFactory.Instance.SharedLockKeys;

    /// <summary>
    /// Gets the reference count for each shared database lock currently maintained by the connection factory.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyDictionary<string, int> SharedLocks => SQLiteDbConnectionFactory.Instance.SharedLocks;

    private bool UseSharedLock { get; }

    #endregion

    #region Methods

    /// <inheritdoc cref="SQLiteConnection.ClearPool(SQLiteConnection)"/>
    public void ClearPool()
    {
        CheckDisposed();
        SQLiteConnection.ClearPool(_conn);
    }

    /// <inheritdoc cref="SQLiteConnection.ClearAllPools"/>
    public static void ClearAllPools()
    {
        SQLiteConnection.ClearAllPools();
    }

    /// <summary>
    /// Closes the database connection.
    /// </summary>
    public void Close()
    {
        CheckDisposed();
        _conn.Close();
    }

    /// <summary>
    /// Restores the connection to its original state.
    /// </summary>
    /// <param name="originalState">The connection state captured before it was opened.</param>
    public void Close(ConnectionState originalState)
    {
        CheckDisposed();
        QuietClose(originalState);
    }

    protected override void DisposeCore()
    {
        try
        {
            try
            {
                Close();
            }
            finally
            {
                _conn.Dispose();
            }
        }
        finally
        {
            var syncRoot = Interlocked.Exchange(ref _syncRoot, null);
            try
            {
                if (syncRoot != null)
                {
                    if (UseSharedLock)
                    {
                        if (SQLiteDbConnectionFactory.Instance.TryRelease(syncRoot.DataSource))
                        {
                            Debug.Assert(syncRoot.IsDisposed);
                        }
                    }
                    else
                    {
                        syncRoot.Dispose();
                    }
                }
            }
            finally
            {
                base.DisposeCore();
            }
        }
    }

    /// <summary>
    /// Opens the database connection.
    /// </summary>
    public void Open()
    {
        CheckDisposed();
        Debug.Assert(!IsOpen, $"Connection is open:{Environment.NewLine}{ConnectionString}");
        _conn.Open();
    }

    /// <summary>
    /// Opens the database connection if it is closed and returns its original state.
    /// </summary>
    /// <param name="originalState">When this method returns, contains the connection state before the operation.</param>
    public void Open(out ConnectionState originalState)
    {
        CheckDisposed();
        QuietOpen(out originalState);
    }

    internal void QuietClose(ConnectionState originalState)
    {
        if (originalState == ConnectionState.Closed)
        {
            _conn.Close();
        }
    }

    internal void QuietOpen(out ConnectionState originalState)
    {
        originalState = _conn.State;
        if (originalState == ConnectionState.Closed)
        {
            _conn.Open();
        }
    }

    #endregion
}