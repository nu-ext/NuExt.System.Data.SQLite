namespace System.Data.SQLite.Tests;

[TestClass]
public sealed class SQLiteDbMigrationTests
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

    [TestMethod]
    public void Migrate_EmptyDatabase_AppliesAllMigrations()
    {
        var createMovies = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        var addRating = new DelegateMigration("0002_AddRating", static context => context.ExecuteNonQuery(
            """
            ALTER TABLE movies
            ADD COLUMN rating INTEGER NULL;
            """));

        SQLiteDbMigration[] migrations =
        [
            createMovies,
            addRating
        ];

        migrations.Migrate(CreateConnection, CancellationToken.None);

        Assert.AreEqual(1, createMovies.ExecutionCount);
        Assert.AreEqual(1, addRating.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsTrue(connection.TableExists("movies", CancellationToken.None));
        Assert.IsTrue(connection.TableExists("__nuext_migrations", CancellationToken.None));
        Assert.AreEqual(2L, GetAppliedMigrationCount(connection));
    }

    [TestMethod]
    public void Migrate_AlreadyMigratedDatabase_DoesNotApplyMigrationsAgain()
    {
        var createMovies = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        var addRating = new DelegateMigration("0002_AddRating", static context => context.ExecuteNonQuery(
            """
            ALTER TABLE movies
            ADD COLUMN rating INTEGER NULL;
            """));

        SQLiteDbMigration[] migrations =
        [
            createMovies,
            addRating
        ];

        migrations.Migrate(CreateConnection, CancellationToken.None);
        migrations.Migrate(CreateConnection, CancellationToken.None);

        Assert.AreEqual(1, createMovies.ExecutionCount);
        Assert.AreEqual(1, addRating.ExecutionCount);

        using var connection = CreateConnection();
        Assert.AreEqual(2L, GetAppliedMigrationCount(connection));
    }

    [TestMethod]
    public void Migrate_DatabaseWithAppliedPrefix_AppliesOnlyPendingMigrations()
    {
        var createMovies = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        var addRating = new DelegateMigration("0002_AddRating", static context => context.ExecuteNonQuery(
            """
            ALTER TABLE movies
            ADD COLUMN rating INTEGER NULL;
            """));

        var createTitleIndex = new DelegateMigration("0003_CreateTitleIndex", static context => context.ExecuteNonQuery(
            """
            CREATE INDEX ix_movies_title
            ON movies (title);
            """));

        SQLiteDbMigration[] initialMigrations =
        [
            createMovies,
            addRating
        ];

        initialMigrations.Migrate(CreateConnection, CancellationToken.None);

        SQLiteDbMigration[] updatedMigrations =
        [
            createMovies,
            addRating,
            createTitleIndex
        ];

        updatedMigrations.Migrate(CreateConnection, CancellationToken.None);

        Assert.AreEqual(1, createMovies.ExecutionCount);
        Assert.AreEqual(1, addRating.ExecutionCount);
        Assert.AreEqual(1, createTitleIndex.ExecutionCount);

        using var connection = CreateConnection();
        Assert.AreEqual(3L, GetAppliedMigrationCount(connection));
        Assert.IsTrue(connection.TableIndexExists("movies", "ix_movies_title", CancellationToken.None));
    }

    [TestMethod]
    public void Migrate_MigrationThrows_RollsBackEntireBatch()
    {
        var createMovies = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        var failingMigration = new DelegateMigration("0002_FailingMigration", static context =>
        {
            context.ExecuteNonQuery(
                """
                CREATE TABLE temporary_data (
                    id INTEGER PRIMARY KEY NOT NULL
                );
                """);

            throw new TestMigrationException();
        });

        SQLiteDbMigration[] migrations =
        [
            createMovies,
            failingMigration
        ];

        Assert.ThrowsExactly<TestMigrationException>(() => migrations.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(1, createMovies.ExecutionCount);
        Assert.AreEqual(1, failingMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("movies", CancellationToken.None));
        Assert.IsFalse(connection.TableExists("temporary_data", CancellationToken.None));
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public void Migrate_DuplicateMigrationId_ThrowsBeforeCreatingConnection()
    {
        var firstMigration = new DelegateMigration("0001_CreateMovies", static _ => { });
        var duplicateMigration = new DelegateMigration("0001_CreateMovies", static _ => { });
        int connectionCount = 0;

        SQLiteDbMigration[] migrations =
        [
            firstMigration,
            duplicateMigration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() => migrations.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, connectionCount);
        Assert.AreEqual(0, firstMigration.ExecutionCount);
        Assert.AreEqual(0, duplicateMigration.ExecutionCount);

        SQLiteDbConnection CreateConnection()
        {
            connectionCount++;
            return new SQLiteDbConnection(_connectionString);
        }
    }

    [TestMethod]
    public void Migrate_ChangedMigrationOrder_ThrowsDbMigrationException()
    {
        var firstMigration = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        var secondMigration = new DelegateMigration("0002_AddRating", static context => context.ExecuteNonQuery(
            """
            ALTER TABLE movies
            ADD COLUMN rating INTEGER NULL;
            """));

        SQLiteDbMigration[] initialMigrations =
        [
            firstMigration,
            secondMigration
        ];

        initialMigrations.Migrate(CreateConnection, CancellationToken.None);

        SQLiteDbMigration[] reorderedMigrations =
        [
            secondMigration,
            firstMigration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() => reorderedMigrations.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(1, firstMigration.ExecutionCount);
        Assert.AreEqual(1, secondMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.AreEqual(2L, GetAppliedMigrationCount(connection));
        Assert.AreEqual("0001_CreateMovies", GetAppliedMigrationId(connection, 1));
        Assert.AreEqual("0002_AddRating", GetAppliedMigrationId(connection, 2));
    }

    [TestMethod]
    public void Migrate_UnknownAppliedMigration_ThrowsDbMigrationException()
    {
        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_UnknownMigration", 1);
        }

        var configuredMigration = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        SQLiteDbMigration[] migrations =
        [
            configuredMigration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() => migrations.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, configuredMigration.ExecutionCount);

        using var verificationConnection = CreateConnection();
        Assert.IsFalse(verificationConnection.TableExists("movies", CancellationToken.None));
        Assert.AreEqual(1L, GetAppliedMigrationCount(verificationConnection));
        Assert.AreEqual("0001_UnknownMigration", GetAppliedMigrationId(verificationConnection, 1));
    }

    [TestMethod]
    public void Migrate_HistoryLongerThanConfiguration_ThrowsDbMigrationException()
    {
        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_CreateMovies", 1);
            AddAppliedMigration(connection, "0002_AddRating", 2);
        }

        var configuredMigration = new DelegateMigration("0001_CreateMovies", static _ => { });

        SQLiteDbMigration[] migrations =
        [
            configuredMigration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() => migrations.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, configuredMigration.ExecutionCount);

        using var verificationConnection = CreateConnection();
        Assert.AreEqual(2L, GetAppliedMigrationCount(verificationConnection));
    }

    [TestMethod]
    public async Task Migrate_CanceledWhileWaitingForLock_ThrowsOperationCanceledException()
    {
        using var lockAcquired = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();
        using var lockingConnection = CreateConnection();
        using var cancellationTokenSource = new CancellationTokenSource();

        Task lockingTask = Task.Run(() => lockingConnection.AcquireLock(() =>
        {
            lockAcquired.Set();
            releaseLock.Wait(CancellationToken.None);
        }, CancellationToken.None), CancellationToken.None);

        Assert.IsTrue(lockAcquired.Wait(TimeSpan.FromSeconds(10), CancellationToken.None));

        var migration = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        Task migrationTask = Task.Run(() => migrations.Migrate(CreateConnection, cancellationTokenSource.Token), cancellationTokenSource.Token);

        try
        {
            await Task.Delay(100, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await migrationTask);

            Assert.AreEqual(0, migration.ExecutionCount);
        }
        finally
        {
            releaseLock.Set();
            await lockingTask;
        }

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("movies", CancellationToken.None));
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public async Task Migrate_ConcurrentExecution_AppliesMigrationOnce()
    {
        using var migrationStarted = new ManualResetEventSlim();
        using var continueMigration = new ManualResetEventSlim();

        var migration = new DelegateMigration("0001_CreateMovies", context =>
        {
            migrationStarted.Set();

            Assert.IsTrue(continueMigration.Wait(TimeSpan.FromSeconds(10), CancellationToken.None), "Timed out waiting to continue the migration.");

            context.ExecuteNonQuery(
                """
                CREATE TABLE movies (
                    id INTEGER PRIMARY KEY NOT NULL,
                    title TEXT NOT NULL
                );
                """);
        });

        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        Task firstTask = Task.Run(() => migrations.Migrate(CreateConnection, CancellationToken.None), CancellationToken.None);

        Assert.IsTrue(
            migrationStarted.Wait(TimeSpan.FromSeconds(10), CancellationToken.None),
            "Timed out waiting for the first migration to start.");

        Task secondTask = Task.Run(() => migrations.Migrate(CreateConnection, CancellationToken.None), CancellationToken.None);

        try
        {
            await Task.Delay(100, CancellationToken.None);
            Assert.IsFalse(secondTask.IsCompleted);
        }
        finally
        {
            continueMigration.Set();
        }

        await Task.WhenAll(firstTask, secondTask);

        Assert.AreEqual(1, migration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsTrue(connection.TableExists("movies", CancellationToken.None));
        Assert.AreEqual(1L, GetAppliedMigrationCount(connection));
        Assert.AreEqual("0001_CreateMovies", GetAppliedMigrationId(connection, 1));
    }

    [TestMethod]
    public void Migrate_ExistingDbContextCommit_PersistsMigrationsAndHistory()
    {
        const string version = "1.2.3";

        var migration = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);

                migrations.Migrate(dbContext, version, CancellationToken.None);

                Assert.IsTrue(connection.TableExists("movies", CancellationToken.None));
                Assert.IsTrue(connection.TableExists("__nuext_migrations", CancellationToken.None));

                dbContext.Commit();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, migration.ExecutionCount);

        using var verificationConnection = CreateConnection();
        Assert.IsTrue(verificationConnection.TableExists("movies", CancellationToken.None));
        Assert.IsTrue(verificationConnection.TableExists("__nuext_migrations", CancellationToken.None));
        Assert.AreEqual(1L, GetAppliedMigrationCount(verificationConnection));
        Assert.AreEqual("0001_CreateMovies", GetAppliedMigrationId(verificationConnection, 1));
        Assert.AreEqual(version, GetAppliedMigrationVersion(verificationConnection, 1));
    }

    [TestMethod]
    public void Migrate_ExistingDbContextRollback_RollsBackMigrationsAndHistory()
    {
        var migration = new DelegateMigration("0001_CreateMovies", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE movies (
                id INTEGER PRIMARY KEY NOT NULL,
                title TEXT NOT NULL
            );
            """));

        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);

                migrations.Migrate(dbContext, CancellationToken.None);

                Assert.IsTrue(connection.TableExists("movies", CancellationToken.None));
                Assert.IsTrue(connection.TableExists("__nuext_migrations", CancellationToken.None));

                dbContext.Rollback();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, migration.ExecutionCount);

        using var verificationConnection = CreateConnection();
        Assert.IsFalse(verificationConnection.TableExists("movies", CancellationToken.None));
        Assert.IsFalse(verificationConnection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    private SQLiteDbConnection CreateConnection()
    {
        return new SQLiteDbConnection(_connectionString);
    }

    private static long GetAppliedMigrationCount(SQLiteDbConnection connection)
    {
        object? result = connection.ExecuteScalar("SELECT COUNT(*) FROM __nuext_migrations;", CommandBehavior.Default, CancellationToken.None);
        Assert.IsNotNull(result);
        return Convert.ToInt64(result);
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void CreateMigrationHistory(SQLiteDbConnection connection)
    {
        connection.ExecuteNonQuery(
            """
            CREATE TABLE __nuext_migrations (
                sequence INTEGER PRIMARY KEY,
                migration_id TEXT NOT NULL UNIQUE CHECK(migration_id <> ''),
                version TEXT,
                applied_at INTEGER NOT NULL
            );
            """);
    }

    private static void AddAppliedMigration(SQLiteDbConnection connection, string migrationId, long sequence, string? version = null)
    {
        connection.ExecuteNonQuery(
            """
            INSERT INTO __nuext_migrations (sequence, migration_id, version, applied_at)
            VALUES (@sequence, @migration_id, @version, @applied_at);
            """,
            CommandBehavior.Default,
            default,
            DbType.Int64.CreateInputParam("@sequence", sequence),
            DbType.String.CreateInputParam("@migration_id", migrationId),
            DbType.String.CreateInputParam("@version", version),
            DbType.Int64.CreateInputParam("@applied_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

    private static string GetAppliedMigrationId(SQLiteDbConnection connection, long sequence)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT migration_id
            FROM __nuext_migrations
            WHERE sequence = @sequence;
            """,
            CommandBehavior.Default,
            default,
            DbType.Int64.CreateInputParam("@sequence", sequence));

        Assert.IsNotNull(result);
        return Convert.ToString(result)!;
    }

    private static string? GetAppliedMigrationVersion(SQLiteDbConnection connection, long sequence)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT version
            FROM __nuext_migrations
            WHERE sequence = @sequence;
            """,
            CommandBehavior.Default,
            default,
            DbType.Int64.CreateInputParam("@sequence", sequence));

        return result is null or DBNull ? null : Convert.ToString(result);
    }

    private sealed class DelegateMigration : SQLiteDbMigration
    {
        private readonly Action<SQLiteDbMigrationContext> _up;
        private int _executionCount;

        public DelegateMigration(string id, Action<SQLiteDbMigrationContext> up)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(up);

            Id = id;
            _up = up;
        }

        public override string Id { get; }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public override void Up(SQLiteDbMigrationContext context)
        {
            Interlocked.Increment(ref _executionCount);
            _up(context);
        }
    }

    private sealed class TestMigrationException : Exception
    {
    }
}