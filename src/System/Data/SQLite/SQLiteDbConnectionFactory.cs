using System.ComponentModel;
using System.Diagnostics;

namespace System.Data.SQLite;

/// <summary>
/// Manages shared process-local locks for SQLite database connections.
/// </summary>
internal sealed class SQLiteDbConnectionFactory
{
    #region Internal classes

    internal class AsyncLockCounter : Disposable
    {
        internal readonly ReentrantAsyncLock Mutex = new();
        internal int ReferenceCount;
        internal string DataSource;
#if DEBUG
        internal bool IsExpired => Volatile.Read(ref ReferenceCount) <= 0;
#endif
        internal AsyncLockCounter(string dataSource)
        {
            DataSource = dataSource;
        }

        protected override void DisposeCore()
        {
            Mutex.Dispose();
            base.DisposeCore();
        }
    }

    #endregion

    public static readonly SQLiteDbConnectionFactory Instance = new();

    private static readonly StringComparer s_dataSourceComparer = PlatformInformation.IsWindows || PlatformInformation.IsMacOS
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly Dictionary<string, AsyncLockCounter> _sharedLocks = new(s_dataSourceComparer);
    private SQLiteDbConnectionFactory()
    {

    }

    #region Properties

    /// <summary>
    /// Gets the number of shared locks currently maintained by the factory.
    /// </summary>
    public int SharedLockCount { get { lock (_sharedLocks) return _sharedLocks.Count; } }

    /// <summary>
    /// Gets a snapshot of the database identifiers for which shared locks are currently maintained.
    /// </summary>
    public IReadOnlyCollection<string> SharedLockKeys { get { lock (_sharedLocks) return _sharedLocks.Keys.ToArray(); } }

    /// <summary>
    /// Gets a snapshot containing the reference count for each shared lock.
    /// </summary>
    public IReadOnlyDictionary<string, int> SharedLocks { get { lock (_sharedLocks) return _sharedLocks.ToDictionary(pair => pair.Key, pair => Volatile.Read(ref pair.Value.ReferenceCount), _sharedLocks.Comparer); } }

    #endregion

    #region Methods

    /// <summary>
    /// Gets or creates a shared lock for the specified database identifier and increments its reference count.
    /// </summary>
    /// <param name="dataSource">The database identifier.</param>
    /// <returns>The shared lock entry.</returns>
    internal AsyncLockCounter GetOrCreateSyncObj(string dataSource)
    {
        AsyncLockCounter? syncObject;
        lock (_sharedLocks)
        {
            if (!_sharedLocks.TryGetValue(dataSource, out syncObject))
            {
                _sharedLocks.Add(dataSource, syncObject = new AsyncLockCounter(dataSource));
            }
            Interlocked.Increment(ref syncObject.ReferenceCount);
        }
        return syncObject;
    }

    /// <summary>
    /// Decrements the reference count of the shared lock for the specified database identifier and removes it when no references remain.
    /// </summary>
    /// <param name="dataSource">The database identifier.</param>
    /// <returns><see langword="true"/> if the shared lock was removed; otherwise, <see langword="false"/>.</returns>
    internal bool TryRelease(string dataSource)
    {
        lock (_sharedLocks)
        {
            if (!_sharedLocks.TryGetValue(dataSource, out var syncObject))
            {
                Debug.Fail($"Shared lock not found: {dataSource}");
                return false;
            }

            int referenceCount = Interlocked.Decrement(ref syncObject.ReferenceCount);
            Debug.Assert(referenceCount >= 0, $"Invalid shared lock reference count: {referenceCount}.");

            if (referenceCount == 0)
            {
                _sharedLocks.Remove(dataSource);
                syncObject.Dispose();
                return true;
            }

            return false;
        }
    }

    #endregion
}
