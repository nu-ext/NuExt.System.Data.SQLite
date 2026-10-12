namespace System.Data.SQLite.Tests;

[TestClass]
public sealed class SQLiteDalBaseTests
{
    private string _databasePath = null!;
    private string _connectionString = null!;

    [TestInitialize]
    public void Initialize()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        _connectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ConnectionString;
    }

    [TestCleanup]
    public void Cleanup()
    {
        SQLiteDbConnection.ClearAllPools();
        DeleteFile(_databasePath);
        DeleteFile(_databasePath + "-shm");
        DeleteFile(_databasePath + "-wal");
    }

    private SQLiteDbConnection CreateConnection()
    {
        return new SQLiteDbConnection(_connectionString);
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ExecuteInDbContextAsync_OwnedContext_CommitsTransaction()
    {
        using var dal = new TestDal(CreateConnection);

        await dal.ExecuteAsync(null, context =>
        {
            context.Connection.ExecuteNonQuery(
                """
                CREATE TABLE TestData (
                    Id INTEGER PRIMARY KEY NOT NULL
                );
                """, CommandBehavior.Default, CancellationToken.None);
        }, CancellationToken.None);

        using var connection = CreateConnection();
        Assert.IsTrue(connection.TableExists("TestData", CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteInDbContextAsync_OwnedContext_RollsBackTransactionOnFailure()
    {
        using var dal = new TestDal(CreateConnection);

        await Assert.ThrowsExactlyAsync<TestOperationException>(async () =>
        {
            await dal.ExecuteAsync(null, context =>
            {
                context.Connection.ExecuteNonQuery(
                    """
                    CREATE TABLE TestData (
                        Id INTEGER PRIMARY KEY NOT NULL
                    );
                    """, CommandBehavior.Default, CancellationToken.None);

                throw new TestOperationException();
            }, CancellationToken.None);
        });

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("TestData", CancellationToken.None));
    }

    [TestMethod]
    public void ExecuteInDbContextAsync_BorrowedContext_DoesNotCompleteTransaction()
    {
        using var dal = new TestDal(CreateConnection);
        using var connection = CreateConnection();

        connection.AcquireLock(() =>
        {
            using var context = new SQLiteDbContext(connection);

            ValueTask operation = dal.ExecuteAsync(context, dbContext =>
            {
                dbContext.Connection.ExecuteNonQuery(
                    """
                    CREATE TABLE TestData (
                        Id INTEGER PRIMARY KEY NOT NULL
                    );
                    """, CommandBehavior.Default, CancellationToken.None);
            }, CancellationToken.None);

            Assert.IsTrue(operation.IsCompletedSuccessfully);
            Assert.IsTrue(context.IsActive);

            context.Rollback();
        }, CancellationToken.None);

        using var verificationConnection = CreateConnection();
        Assert.IsFalse(verificationConnection.TableExists("TestData", CancellationToken.None));
    }

    private sealed class TestDal(Func<SQLiteDbConnection> createConnection) : SQLiteDalBase(createConnection)
    {
        public ValueTask ExecuteAsync(SQLiteDbContext? context, Action<SQLiteDbContext> action, CancellationToken cancellationToken = default)
        {
            return ExecuteInDbContextAsync(context, action, cancellationToken);
        }
    }

    private sealed class TestOperationException : Exception
    {
    }
}