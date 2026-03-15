using System.ComponentModel;
using System.Diagnostics;

namespace System.Data.SQLite;

/// <summary>
/// Provides a wrapper for SQLite connections, enabling thread-safe concurrent access
/// to the SQLite database. This class ensures that database operations are executed
/// with proper synchronization, preventing data corruption and ensuring consistency.
/// Implements the <see cref="IDbConnection"/> interface for seamless integration with 
/// existing database code and extends <see cref="Disposable"/> for proper resource management.
/// </summary>
public sealed partial class SQLiteDbConnection : Disposable, IDbConnection
{
    private readonly SQLiteConnection _conn;
    private SQLiteDbConnectionFactory.AsyncLockCounter _syncRoot;
    internal volatile int TransactionCount;

    internal const int StackallocCharBufferSizeLimit = 256;

    /// <summary>
    /// Initializes the connection with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to use.</param>
    public SQLiteDbConnection(string connectionString) : this(connectionString, true)
    {

    }

    /// <summary>
    ///  Initializes the connection with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to use.</param>
    /// <param name="useSharedLock">Use a shared lock to facilitate multi-threading in database access and eliminate "Database is locked" exceptions.</param>
    public SQLiteDbConnection(string connectionString, bool useSharedLock)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        var csb = new SQLiteConnectionStringBuilder(connectionString);//throws if connection string is invalid
        _conn = new SQLiteConnection(connectionString);
        UseSharedLock = useSharedLock;
        if (useSharedLock)
        {
            _syncRoot = SQLiteDbConnectionFactory.Instance.GetSyncObj(csb.DataSource);
        }
        else
        {
            _syncRoot = new SQLiteDbConnectionFactory.AsyncLockCounter() { ReferenceCount = 1 };
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

    public bool InTransaction => TransactionCount > 0;

    /// <summary>
    /// Determines whether the current connection holds the database lock.
    /// </summary>
    public bool IsAcquired
    {
        get
        {
            CheckDisposed();
            return _syncRoot.Mutex.IsHeldByCurrentFlow;
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

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int SharedLockCount => SQLiteDbConnectionFactory.Instance.SharedLockCount;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyCollection<string> SharedLockKeys => SQLiteDbConnectionFactory.Instance.SharedLockKeys;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyDictionary<string, int> SharedLocks => SQLiteDbConnectionFactory.Instance.SharedLocks;

    [EditorBrowsable(EditorBrowsableState.Never)]
    private bool UseSharedLock { get; }

    #endregion

    #region Methods

    public void ClearPool()
    {
        SQLiteConnection.ClearPool(_conn);
        //SQLiteConnection.ClearAllPools();
    }

    public void Close()
    {
        _conn.Close();
    }

    public void Close(ConnectionState originalState)
    {
        QuietClose(originalState);
    }

    protected override void DisposeCore()
    {
        Close();
        _conn.Dispose();

        var syncRoot = Interlocked.Exchange(ref _syncRoot!, null);
        if (syncRoot != null)
        {
            if (UseSharedLock)
            {
                if (SQLiteDbConnectionFactory.Instance.TryRelease(syncRoot.DataSource!))
                {
                    Debug.Assert(syncRoot.IsDisposed);
                }
            }
            else
            {
                syncRoot.Dispose();
            }
        }
        base.DisposeCore();
    }

    public void Open()
    {
        Debug.Assert(!IsOpen, $"Connection is open:{Environment.NewLine}{ConnectionString}");
        _conn.Open();
    }

    public void Open(out ConnectionState originalState)
    {
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
