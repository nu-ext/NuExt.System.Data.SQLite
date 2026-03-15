using System.ComponentModel;

namespace System.Data.SQLite;

internal class SQLiteDbConnectionFactory
{
    #region Internal classes

    internal class AsyncLockCounter : Disposable
    {
        internal readonly ReentrantAsyncLock Mutex = new();
        internal volatile int ReferenceCount;
        internal string? DataSource;
#if DEBUG
        internal bool IsExpired => ReferenceCount <= 0;
#endif
        protected override void DisposeCore()
        {
            Mutex.Dispose();
            base.DisposeCore();
        }
    }

    #endregion

    public static readonly SQLiteDbConnectionFactory Instance = new();

    private readonly Dictionary<string, AsyncLockCounter> _sharedLocks = new(StringComparer.CurrentCultureIgnoreCase);
    private SQLiteDbConnectionFactory()
    {

    }

    #region Properties

    public int SharedLockCount { get { lock (_sharedLocks) return _sharedLocks.Count; } }

    public IReadOnlyCollection<string> SharedLockKeys { get { lock (_sharedLocks) return _sharedLocks.Keys; } }

    public IReadOnlyDictionary<string, int> SharedLocks { get { lock (_sharedLocks) return _sharedLocks.ToDictionary(pair => pair.Key, pair => pair.Value.ReferenceCount); } }

    #endregion

    #region Methods

    /// <summary>
    /// Gets an object that can be used to synchronize access to the database.
    /// </summary>
    internal AsyncLockCounter GetSyncObj(string dataSource)
    {
        AsyncLockCounter? syncObject;
        lock (_sharedLocks)
        {
            if (!_sharedLocks.TryGetValue(dataSource, out syncObject))
            {
                _sharedLocks.Add(dataSource, syncObject = new AsyncLockCounter() { DataSource = dataSource });
            }
            Interlocked.Increment(ref syncObject.ReferenceCount);
        }
        return syncObject;
    }

    internal bool TryRelease(string dataSource)
    {
        lock (_sharedLocks)
        {
            if (_sharedLocks.TryGetValue(dataSource, out var syncObject) && 
                Interlocked.Decrement(ref syncObject.ReferenceCount) <= 0)
            {
                _sharedLocks.Remove(dataSource);
                syncObject.Dispose();
                return true;
            }
           
        }
        return false;
    }

    #endregion
}
