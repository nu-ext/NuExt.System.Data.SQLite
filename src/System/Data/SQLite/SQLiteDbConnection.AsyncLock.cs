using System.Runtime.CompilerServices;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    /// <summary>
    /// Acquires a lock and executes the specified action while holding the lock.
    /// </summary>
    /// <param name="action">The action to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public void AcquireLock(Action action, CancellationToken cancellationToken = default)
    {
        GetSyncRoot().Mutex.Acquire(action, cancellationToken);
    }

    /// <summary>
    /// Acquires a lock and executes the specified function while holding the lock.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="func">The function to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <returns>The result of the function executed while holding the lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public T AcquireLock<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        return GetSyncRoot().Mutex.Acquire(func, cancellationToken);
    }

    /// <summary>
    /// Asynchronously acquires a lock and executes the specified asynchronous function while holding the lock.
    /// </summary>
    /// <param name="func">The asynchronous function to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public ValueTask AcquireLockAsync(Func<ValueTask> func, CancellationToken cancellationToken = default)
    {
        return GetSyncRoot().Mutex.AcquireAsync(func, cancellationToken);
    }

    /// <summary>
    /// Asynchronously acquires a lock and executes the specified asynchronous function while holding the lock.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="func">The asynchronous function to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result of the function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public ValueTask<T> AcquireLockAsync<T>(Func<ValueTask<T>> func, CancellationToken cancellationToken = default)
    {
        return GetSyncRoot().Mutex.AcquireAsync(func, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SQLiteDbConnectionFactory.AsyncLockCounter GetSyncRoot()
    {
        CheckDisposed();
        return _syncRoot ?? throw new ObjectDisposedException(GetType().FullName);
    }
}