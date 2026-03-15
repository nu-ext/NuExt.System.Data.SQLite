namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    /// <summary>
    /// Acquires a lock and executes the specified action while holding the lock.
    /// </summary>
    /// <param name="action">The action to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public void AcquireLock(Action action, CancellationToken cancellationToken)
    {
        CheckDisposed();
        _syncRoot.Mutex.Acquire(action, cancellationToken);
    }

    /// <summary>
    /// Acquires a lock and executes the specified function while holding the lock.
    /// Returns the result of the function.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="func">The function to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <returns>The result of the function executed while holding the lock.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public T AcquireLock<T>(Func<T> func, CancellationToken cancellationToken)
    {
        CheckDisposed();
        return _syncRoot.Mutex.Acquire(func, cancellationToken);
    }

    /// <summary>
    /// Asynchronously acquires a lock and executes the specified asynchronous function while holding the lock.
    /// </summary>
    /// <param name="func">The asynchronous function to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public async ValueTask AcquireLockAsync(Func<ValueTask> func, CancellationToken cancellationToken)
    {
        CheckDisposed();
        await _syncRoot.Mutex.AcquireAsync(func, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously acquires a lock and executes the specified asynchronous function while holding the lock.
    /// Returns the result of the function.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the function.</typeparam>
    /// <param name="func">The asynchronous function to execute while holding the lock.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lock.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the function.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    public async ValueTask<T> AcquireLockAsync<T>(Func<ValueTask<T>> func, CancellationToken cancellationToken)
    {
        CheckDisposed();
        return await _syncRoot.Mutex.AcquireAsync(func, cancellationToken).ConfigureAwait(false);
    }
}
