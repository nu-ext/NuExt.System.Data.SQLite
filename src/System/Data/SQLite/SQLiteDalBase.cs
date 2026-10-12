namespace System.Data.SQLite;

/// <summary>
/// Represents an abstract base class for SQLite-specific Data Access Layer (DAL) operations,
/// providing database context execution and SQLite connection creation.
/// </summary>
public abstract class SQLiteDalBase : DalBase<SQLiteDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDalBase"/> class with the specified connection creation function.
    /// </summary>
    /// <param name="createConnection">A function that creates a new <see cref="SQLiteDbConnection"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createConnection"/> is <see langword="null"/>.</exception>
    protected SQLiteDalBase(Func<SQLiteDbConnection> createConnection) : this(createConnection, new SQLiteDbContextExecutor(createConnection))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDalBase"/> class with the specified connection creation function and database context executor.
    /// </summary>
    /// <param name="createConnection">A function that creates a new <see cref="SQLiteDbConnection"/>.</param>
    /// <param name="executor">The database context executor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createConnection"/> or <paramref name="executor"/> is <see langword="null"/>.</exception>
    protected SQLiteDalBase(Func<SQLiteDbConnection> createConnection, IDbContextExecutor<SQLiteDbContext> executor) : base(executor)
    {
        CreateConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
    }

    /// <summary>
    /// Gets the function used to create SQLite database connections.
    /// </summary>
    protected Func<SQLiteDbConnection> CreateConnection { get; }
}