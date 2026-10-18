namespace System.Data.SQLite.Tests;

[TestClass]
public sealed class SQLiteDbMigrationTests
{
    private const string DefaultScope = "default";
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

        Assert.ThrowsExactly<DbMigrationException>(() => migrations.Migrate(CreateConnectionLocal, CancellationToken.None));

        Assert.AreEqual(0, connectionCount);
        Assert.AreEqual(0, firstMigration.ExecutionCount);
        Assert.AreEqual(0, duplicateMigration.ExecutionCount);
        return;

        SQLiteDbConnection CreateConnectionLocal()
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

    [TestMethod]
    public void Migrate_InvalidAppliedSequence_ThrowsDbMigrationException()
    {
        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_CreateMovies", 2);
        }

        var migration = new DelegateMigration("0001_CreateMovies", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() => migrations.Migrate(CreateConnection, CancellationToken.None));
        Assert.AreEqual(0, migration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection));
            Assert.AreEqual("0001_CreateMovies", GetAppliedMigrationId(connection, 2));
        }
    }

    [TestMethod]
    public void MigrateScope_IndependentScopes_AppliesMigrationsIndependently()
    {
        const string coreScope = "core";
        const string moduleScope = "module.video";

        var createCoreTable = new DelegateMigration("0001_CreateSchema", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_settings (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));
        var createModuleTable = new DelegateMigration("0001_CreateSchema", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE video_records (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));

        SQLiteDbMigration[] coreMigrations =
        [
            createCoreTable
        ];
        SQLiteDbMigration[] moduleMigrations =
        [
            createModuleTable
        ];

        coreMigrations.MigrateScope(CreateConnection, coreScope, CancellationToken.None);
        moduleMigrations.MigrateScope(CreateConnection, moduleScope, CancellationToken.None);

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, createModuleTable.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsTrue(connection.TableExists("core_settings", CancellationToken.None));
        Assert.IsTrue(connection.TableExists("video_records", CancellationToken.None));
        Assert.AreEqual(2L, GetAppliedMigrationCount(connection));
        Assert.AreEqual(1L, GetAppliedMigrationCount(connection, coreScope));
        Assert.AreEqual(1L, GetAppliedMigrationCount(connection, moduleScope));
        Assert.AreEqual("0001_CreateSchema", GetAppliedMigrationId(connection, 1, coreScope));
        Assert.AreEqual("0001_CreateSchema", GetAppliedMigrationId(connection, 1, moduleScope));
    }

    [TestMethod]
    public void MigrateScope_InvalidHistoryInAnotherScope_DoesNotAffectMigration()
    {
        const string validScope = "core";
        const string invalidScope = "module.video";

        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_UnknownMigration", 1, invalidScope);
        }

        var migration = new DelegateMigration("0001_CreateCoreSchema", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_settings (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        migrations.MigrateScope(CreateConnection, validScope, CancellationToken.None);

        Assert.AreEqual(1, migration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.IsTrue(connection.TableExists("core_settings", CancellationToken.None));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, validScope));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, invalidScope));
            Assert.AreEqual("0001_CreateCoreSchema", GetAppliedMigrationId(connection, 1, validScope));
            Assert.AreEqual("0001_UnknownMigration", GetAppliedMigrationId(connection, 1, invalidScope));
        }
    }

    [TestMethod]
    public void MigrateScope_InvalidHistoryInCurrentScope_ThrowsDbMigrationException()
    {
        const string scope = "module.video";

        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_UnknownMigration", 1, scope);
        }

        var migration = new DelegateMigration("0001_CreateVideoSchema", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() => migrations.MigrateScope(CreateConnection, scope, CancellationToken.None));
        Assert.AreEqual(0, migration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, scope));
            Assert.AreEqual("0001_UnknownMigration", GetAppliedMigrationId(connection, 1, scope));
        }
    }

    [TestMethod]
    public void MigrateScope_MultipleScopes_PreservesGlobalApplicationOrder()
    {
        const string coreScope = "core";
        const string moduleScope = "module.video";

        var createCoreTable = new DelegateMigration("0001_CreateCoreTable", static context => context.ExecuteNonQuery(
            """
        CREATE TABLE core_data (
            id INTEGER PRIMARY KEY NOT NULL
        );
        """));
        var createModuleTable = new DelegateMigration("0001_CreateModuleTable", static context => context.ExecuteNonQuery(
            """
        CREATE TABLE module_data (
            id INTEGER PRIMARY KEY NOT NULL
        );
        """));
        var addCoreValue = new DelegateMigration("0002_AddCoreValue", static context => context.ExecuteNonQuery(
            """
        ALTER TABLE core_data
        ADD COLUMN value TEXT NULL;
        """));
        SQLiteDbMigration[] initialCoreMigrations =
        [
            createCoreTable
        ];
        SQLiteDbMigration[] moduleMigrations =
        [
            createModuleTable
        ];
        SQLiteDbMigration[] updatedCoreMigrations =
        [
            createCoreTable,
        addCoreValue
        ];

        initialCoreMigrations.MigrateScope(CreateConnection, coreScope, CancellationToken.None);
        moduleMigrations.MigrateScope(CreateConnection, moduleScope, CancellationToken.None);
        updatedCoreMigrations.MigrateScope(CreateConnection, coreScope, CancellationToken.None);

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, createModuleTable.ExecutionCount);
        Assert.AreEqual(1, addCoreValue.ExecutionCount);

        using var connection = CreateConnection();
        Assert.AreEqual(3L, GetAppliedMigrationCount(connection));
        Assert.AreEqual(coreScope, GetAppliedMigrationScope(connection, 1));
        Assert.AreEqual(moduleScope, GetAppliedMigrationScope(connection, 2));
        Assert.AreEqual(coreScope, GetAppliedMigrationScope(connection, 3));
        Assert.AreEqual("0001_CreateCoreTable", GetAppliedMigrationId(connection, 1, coreScope));
        Assert.AreEqual("0002_AddCoreValue", GetAppliedMigrationId(connection, 2, coreScope));
        Assert.AreEqual("0001_CreateModuleTable", GetAppliedMigrationId(connection, 1, moduleScope));
    }

    [TestMethod]
    public void MigrateScope_ExistingDbContextCommit_PersistsMultipleScopes()
    {
        const string coreScope = "core";
        const string moduleScope = "module.video";

        var createCoreTable = new DelegateMigration("0001_CreateCoreTable", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));
        var createModuleTable = new DelegateMigration("0001_CreateModuleTable", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE module_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));
        SQLiteDbMigration[] coreMigrations =
        [
            createCoreTable
        ];
        SQLiteDbMigration[] moduleMigrations =
        [
            createModuleTable
        ];

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);
                coreMigrations.MigrateScope(dbContext, coreScope, CancellationToken.None);
                moduleMigrations.MigrateScope(dbContext, moduleScope, CancellationToken.None);
                dbContext.Commit();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, createModuleTable.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.IsTrue(connection.TableExists("core_data", CancellationToken.None));
            Assert.IsTrue(connection.TableExists("module_data", CancellationToken.None));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, coreScope));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, moduleScope));
        }
    }

    [TestMethod]
    public void MigrateScope_ExistingDbContextRollback_RollsBackMultipleScopes()
    {
        var createCoreTable = new DelegateMigration("0001_CreateCoreTable", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));
        var createModuleTable = new DelegateMigration("0001_CreateModuleTable", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE module_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));
        SQLiteDbMigration[] coreMigrations =
        [
            createCoreTable
        ];
        SQLiteDbMigration[] moduleMigrations =
        [
            createModuleTable
        ];

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);
                coreMigrations.MigrateScope(dbContext, "core", CancellationToken.None);
                moduleMigrations.MigrateScope(dbContext, "module.video", CancellationToken.None);
                dbContext.Rollback();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, createModuleTable.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.IsFalse(connection.TableExists("core_data", CancellationToken.None));
            Assert.IsFalse(connection.TableExists("module_data", CancellationToken.None));
            Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
        }
    }

    [TestMethod]
    public void MigrationScopeExists_MissingScope_ReturnsFalse()
    {
        SQLiteDbMigration[] migrations = [];

        migrations.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            Assert.IsFalse(context.MigrationScopeExists("module.video"));

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void IsMigrationApplied_AppliedMigration_ReturnsTrue()
    {
        const string scope = "module.video";

        var migration = new DelegateMigration("0001_CreateSchema", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        migrations.MigrateScope(CreateConnection, scope, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            Assert.IsTrue(context.MigrationScopeExists(scope));
            Assert.IsTrue(context.IsMigrationApplied(scope, migration.Id));
            Assert.IsFalse(context.IsMigrationApplied(scope, "0002_Unknown"));

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void RequireAppliedMigration_MissingMigration_ThrowsDbMigrationException()
    {
        SQLiteDbMigration[] migrations = [];

        migrations.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            Assert.ThrowsExactly<DbMigrationException>(() =>
                context.RequireAppliedMigration("module.video", "0001_CreateSchema"));

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void RequireAppliedMigrationIfScopeExists_MissingScope_DoesNotThrow()
    {
        SQLiteDbMigration[] migrations = [];

        migrations.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            context.RequireAppliedMigrationIfScopeExists("module.video", "0001_CreateSchema");

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void RequireAppliedMigrationIfScopeExists_MissingMigration_ThrowsDbMigrationException()
    {
        const string scope = "module.video";

        var migration = new DelegateMigration("0001_CreateSchema", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        migrations.MigrateScope(CreateConnection, scope, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            Assert.ThrowsExactly<DbMigrationException>(() =>
                context.RequireAppliedMigrationIfScopeExists(scope, "0002_UseNewValue"));

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MigrateScope_InterleavedDependencies_AppliesMigrationsInRequiredOrder()
    {
        const string coreScope = "core";
        const string moduleScope = "module.video";

        var createCoreTable = new DelegateMigration("0001_CreateCoreTable", static context => context.ExecuteNonQuery(
            """
        CREATE TABLE core_data (
            id INTEGER PRIMARY KEY NOT NULL,
            old_value TEXT NULL
        );
        """));

        var addNewValue = new DelegateMigration("0002_AddNewValue", static context => context.ExecuteNonQuery(
            """
        ALTER TABLE core_data
        ADD COLUMN new_value TEXT NULL;
        """));

        var updateModule = new DelegateMigration("0001_UseNewValue", static context =>
        {
            context.RequireAppliedMigration(coreScope, "0002_AddNewValue");

            context.ExecuteNonQuery(
                """
            CREATE TABLE video_data (
                id INTEGER PRIMARY KEY NOT NULL,
                core_id INTEGER NOT NULL,
                FOREIGN KEY (core_id) REFERENCES core_data(id)
            );
            """);
        });

        var removeOldValue = new DelegateMigration("0003_RemoveOldValue", static context =>
        {
            context.RequireAppliedMigrationIfScopeExists(moduleScope, "0001_UseNewValue");

            context.ExecuteNonQuery(
                """
            ALTER TABLE core_data
            DROP COLUMN old_value;
            """);
        });

        SQLiteDbMigration[] coreCompatibilityMigrations =
        [
            createCoreTable,
        addNewValue
        ];

        SQLiteDbMigration[] moduleMigrations =
        [
            updateModule
        ];

        SQLiteDbMigration[] coreMigrations =
        [
            createCoreTable,
        addNewValue,
        removeOldValue
        ];

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);

                coreCompatibilityMigrations.MigrateScope(dbContext, coreScope, CancellationToken.None);
                moduleMigrations.MigrateScope(dbContext, moduleScope, CancellationToken.None);
                coreMigrations.MigrateScope(dbContext, coreScope, CancellationToken.None);

                dbContext.Commit();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, addNewValue.ExecutionCount);
        Assert.AreEqual(1, updateModule.ExecutionCount);
        Assert.AreEqual(1, removeOldValue.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.IsTrue(connection.TableExists("core_data", CancellationToken.None));
            Assert.IsTrue(connection.TableExists("video_data", CancellationToken.None));
            Assert.DoesNotContain("old_value", connection.GetTableColumns("core_data", CancellationToken.None));
            Assert.Contains("new_value", connection.GetTableColumns("core_data", CancellationToken.None));
            Assert.AreEqual("0003_RemoveOldValue", GetAppliedMigrationId(connection, 3, coreScope));
            Assert.AreEqual("0001_UseNewValue", GetAppliedMigrationId(connection, 1, moduleScope));
        }
    }

    [TestMethod]
    public void RequireAppliedMigration_AppliedMigration_DoesNotThrow()
    {
        const string scope = "module.video";

        var migration = new DelegateMigration("0001_CreateSchema", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        migrations.MigrateScope(CreateConnection, scope, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            context.RequireAppliedMigration(scope, migration.Id);

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void RequireAppliedMigrationIfScopeExists_AppliedMigration_DoesNotThrow()
    {
        const string scope = "module.video";

        var migration = new DelegateMigration("0001_CreateSchema", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        migrations.MigrateScope(CreateConnection, scope, CancellationToken.None);

        using var connection = CreateConnection();
        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, CancellationToken.None);

            context.RequireAppliedMigrationIfScopeExists(scope, migration.Id);

            transaction.Rollback();
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MigrateScope_MissingDependency_RollsBackMigrationBatch()
    {
        const string coreScope = "core";
        const string moduleScope = "module.video";

        var createCoreTable = new DelegateMigration("0001_CreateCoreTable", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));

        var dependentMigration = new DelegateMigration("0002_CreateDependentTable", static context =>
        {
            context.RequireAppliedMigration(moduleScope, "0001_CreateModuleSchema");

            context.ExecuteNonQuery(
                """
                CREATE TABLE dependent_data (
                    id INTEGER PRIMARY KEY NOT NULL
                );
                """);
        });

        SQLiteDbMigration[] migrations =
        [
            createCoreTable,
            dependentMigration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() =>
            migrations.MigrateScope(CreateConnection, coreScope, CancellationToken.None));

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, dependentMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("core_data", CancellationToken.None));
        Assert.IsFalse(connection.TableExists("dependent_data", CancellationToken.None));
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public void MigrateScope_MissingDependency_DoesNotRecordPendingMigration()
    {
        const string coreScope = "core";
        const string moduleScope = "module.video";

        var createCoreTable = new DelegateMigration("0001_CreateCoreTable", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL,
                old_value TEXT NULL
            );
            """));

        SQLiteDbMigration[] compatibilityMigrations =
        [
            createCoreTable
        ];

        compatibilityMigrations.MigrateScope(CreateConnection, coreScope, CancellationToken.None);

        var removeOldValue = new DelegateMigration("0002_RemoveOldValue", static context =>
        {
            context.RequireAppliedMigration(moduleScope, "0001_UseNewValue");

            context.ExecuteNonQuery(
                """
                ALTER TABLE core_data
                DROP COLUMN old_value;
                """);
        });

        SQLiteDbMigration[] migrations =
        [
            createCoreTable,
            removeOldValue
        ];

        Assert.ThrowsExactly<DbMigrationException>(() =>
            migrations.MigrateScope(CreateConnection, coreScope, CancellationToken.None));

        Assert.AreEqual(1, createCoreTable.ExecutionCount);
        Assert.AreEqual(1, removeOldValue.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsTrue(connection.TableExists("core_data", CancellationToken.None));
        Assert.Contains("old_value", connection.GetTableColumns("core_data", CancellationToken.None));
        Assert.AreEqual(1L, GetAppliedMigrationCount(connection, coreScope));
        Assert.AreEqual("0001_CreateCoreTable", GetAppliedMigrationId(connection, 1, coreScope));
    }

    [TestMethod]
    public void MigrationPlan_InterleavedDependencies_AppliesMigrationsInDependencyOrder()
    {
        var executionOrder = new List<string>();

        var coreMigration1 = new DelegateMigration("0001_CoreExpand", _ => executionOrder.Add("core/1"));

        var videoMigration1 = new DelegateMigration(
            "0001_VideoUpdate",
            _ => executionOrder.Add("module.video/1"),
            [
                new SQLiteDbMigrationDependency("core", "0001_CoreExpand")
            ]);

        var coreMigration2 = new DelegateMigration(
            "0002_CoreContract",
            _ => executionOrder.Add("core/2"),
            [
                new SQLiteDbMigrationDependency("module.video", "0001_VideoUpdate")
            ]);

        var videoMigration2 = new DelegateMigration(
            "0002_VideoFinalize",
            _ => executionOrder.Add("module.video/2"),
            [
                new SQLiteDbMigrationDependency("core", "0002_CoreContract")
            ]);

        SQLiteDbMigration[] coreMigrations =
        [
            coreMigration1,
        coreMigration2
        ];

        SQLiteDbMigration[] videoMigrations =
        [
            videoMigration1,
        videoMigration2
        ];

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", coreMigrations)
            .AddScope("module.video", videoMigrations);

        plan.Migrate(CreateConnection, CancellationToken.None);

        Assert.HasCount(4, executionOrder);
        Assert.AreEqual("core/1", executionOrder[0]);
        Assert.AreEqual("module.video/1", executionOrder[1]);
        Assert.AreEqual("core/2", executionOrder[2]);
        Assert.AreEqual("module.video/2", executionOrder[3]);

        using var connection = CreateConnection();
        Assert.AreEqual("0001_CoreExpand", GetAppliedMigrationId(connection, 1, "core"));
        Assert.AreEqual("0002_CoreContract", GetAppliedMigrationId(connection, 2, "core"));
        Assert.AreEqual("0001_VideoUpdate", GetAppliedMigrationId(connection, 1, "module.video"));
        Assert.AreEqual("0002_VideoFinalize", GetAppliedMigrationId(connection, 2, "module.video"));
    }

    [TestMethod]
    public void MigrationPlan_CyclicDependencies_ThrowsDbMigrationException()
    {
        var coreMigration = new DelegateMigration(
            "0001_Core",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("module.video", "0001_Video")
            ]);

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [videoMigration]);

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, coreMigration.ExecutionCount);
        Assert.AreEqual(0, videoMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public void MigrationPlan_UnknownRegisteredMigrationDependency_ThrowsDbMigrationException()
    {
        var coreMigration = new DelegateMigration(
            "0001_Core",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("module.video", "0002_Unknown")
            ]);

        var videoMigration = new DelegateMigration("0001_Video", static _ => { });

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [videoMigration]);

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, coreMigration.ExecutionCount);
        Assert.AreEqual(0, videoMigration.ExecutionCount);
    }

    [TestMethod]
    public void MigrationPlan_MissingOptionalScope_IgnoresDependency()
    {
        var coreMigration = new DelegateMigration(
            "0001_Core",
            static context => context.ExecuteNonQuery(
                """
                CREATE TABLE core_data (
                    id INTEGER PRIMARY KEY NOT NULL
                );
                """),
            [
                new SQLiteDbMigrationDependency("module.video", "0001_Video", optionalScope: true)
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration]);

        plan.Migrate(CreateConnection, CancellationToken.None);

        Assert.AreEqual(1, coreMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsTrue(connection.TableExists("core_data", CancellationToken.None));
        Assert.AreEqual("0001_Core", GetAppliedMigrationId(connection, 1, "core"));
    }

    [TestMethod]
    public void MigrationPlan_UnregisteredOptionalScopeWithHistory_RequiresDependency()
    {
        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_OldVideoSchema", 1, "module.video");
        }

        var coreMigration = new DelegateMigration(
            "0001_Core",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("module.video", "0002_UseNewSchema", optionalScope: true)
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration]);

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, coreMigration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.AreEqual(0L, GetAppliedMigrationCount(connection, "core"));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, "module.video"));
        }
    }

    [TestMethod]
    public void MigrationPlan_RepeatedExecution_DoesNotReapplyMigrations()
    {
        var coreMigration = new DelegateMigration("0001_Core", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static context => context.ExecuteNonQuery(
                """
                CREATE TABLE video_data (
                    id INTEGER PRIMARY KEY NOT NULL
                );
                """),
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [videoMigration]);

        plan.Migrate(CreateConnection, CancellationToken.None);
        plan.Migrate(CreateConnection, CancellationToken.None);

        Assert.AreEqual(1, coreMigration.ExecutionCount);
        Assert.AreEqual(1, videoMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.AreEqual(1L, GetAppliedMigrationCount(connection, "core"));
        Assert.AreEqual(1L, GetAppliedMigrationCount(connection, "module.video"));
    }

    [TestMethod]
    public void MigrationPlan_ReverseScopeRegistration_AppliesMigrationsInDependencyOrder()
    {
        var executionOrder = new List<string>();

        var coreMigration = new DelegateMigration("0001_Core", _ => executionOrder.Add("core"));

        var videoMigration = new DelegateMigration(
            "0001_Video",
            _ => executionOrder.Add("module.video"),
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("module.video", [videoMigration])
            .AddScope("core", [coreMigration]);

        plan.Migrate(CreateConnection, CancellationToken.None);

        Assert.HasCount(2, executionOrder);
        Assert.AreEqual("core", executionOrder[0]);
        Assert.AreEqual("module.video", executionOrder[1]);
    }

    [TestMethod]
    public void MigrationPlan_ExistingDbContextCommit_PersistsAllScopes()
    {
        var coreMigration = new DelegateMigration("0001_Core", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static context => context.ExecuteNonQuery(
                """
                CREATE TABLE video_data (
                    id INTEGER PRIMARY KEY NOT NULL
                );
                """),
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [videoMigration]);

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);

                plan.Migrate(dbContext, CancellationToken.None);

                dbContext.Commit();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, coreMigration.ExecutionCount);
        Assert.AreEqual(1, videoMigration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.IsTrue(connection.TableExists("core_data", CancellationToken.None));
            Assert.IsTrue(connection.TableExists("video_data", CancellationToken.None));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, "core"));
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, "module.video"));
        }
    }

    [TestMethod]
    public void MigrationPlan_ExistingDbContextRollback_RollsBackAllScopes()
    {
        var coreMigration = new DelegateMigration("0001_Core", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static context => context.ExecuteNonQuery(
                """
                CREATE TABLE video_data (
                    id INTEGER PRIMARY KEY NOT NULL
                );
                """),
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [videoMigration]);

        using (var connection = CreateConnection())
        {
            connection.AcquireLock(() =>
            {
                using var dbContext = new SQLiteDbContext(connection);

                plan.Migrate(dbContext, CancellationToken.None);

                dbContext.Rollback();
            }, CancellationToken.None);
        }

        Assert.AreEqual(1, coreMigration.ExecutionCount);
        Assert.AreEqual(1, videoMigration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.IsFalse(connection.TableExists("core_data", CancellationToken.None));
            Assert.IsFalse(connection.TableExists("video_data", CancellationToken.None));
            Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
        }
    }

    [TestMethod]
    public void MigrationPlan_MigrationFailure_RollsBackAppliedMigrations()
    {
        var coreMigration = new DelegateMigration("0001_Core", static context => context.ExecuteNonQuery(
            """
            CREATE TABLE core_data (
                id INTEGER PRIMARY KEY NOT NULL
            );
            """));

        var failingMigration = new DelegateMigration(
            "0001_Video",
            static _ => throw new InvalidOperationException("Migration failed."),
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [failingMigration]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            plan.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(1, coreMigration.ExecutionCount);
        Assert.AreEqual(1, failingMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("core_data", CancellationToken.None));
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public void MigrationPlan_ExistingHistory_AppliesOnlyPendingMigrations()
    {
        var coreMigration1 = new DelegateMigration("0001_Core", static _ => { });

        SQLiteDbMigration[] initialCoreMigrations =
        [
            coreMigration1
        ];

        initialCoreMigrations.MigrateScope(CreateConnection, "core", CancellationToken.None);

        var coreMigration2 = new DelegateMigration("0002_Core", static _ => { });

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("core", "0002_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration1, coreMigration2])
            .AddScope("module.video", [videoMigration]);

        plan.Migrate(CreateConnection, CancellationToken.None);

        Assert.AreEqual(1, coreMigration1.ExecutionCount);
        Assert.AreEqual(1, coreMigration2.ExecutionCount);
        Assert.AreEqual(1, videoMigration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.AreEqual("0001_Core", GetAppliedMigrationId(connection, 1, "core"));
        Assert.AreEqual("0002_Core", GetAppliedMigrationId(connection, 2, "core"));
        Assert.AreEqual("0001_Video", GetAppliedMigrationId(connection, 1, "module.video"));
    }

    [TestMethod]
    public void MigrationPlan_InvalidAppliedHistory_ThrowsDbMigrationException()
    {
        using (var connection = CreateConnection())
        {
            CreateMigrationHistory(connection);
            AddAppliedMigration(connection, "0001_Unknown", 1, "core");
        }

        var coreMigration = new DelegateMigration("0001_Core", static _ => { });

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration]);

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, coreMigration.ExecutionCount);

        using (var connection = CreateConnection())
        {
            Assert.AreEqual(1L, GetAppliedMigrationCount(connection, "core"));
            Assert.AreEqual("0001_Unknown", GetAppliedMigrationId(connection, 1, "core"));
        }
    }

    [TestMethod]
    public void MigrationPlan_DuplicateScope_ThrowsInvalidOperationException()
    {
        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", []);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            plan.AddScope("core", []));
    }

    [TestMethod]
    public void MigrationPlan_DuplicateMigrationId_ThrowsDbMigrationException()
    {
        var migration1 = new DelegateMigration("0001_CreateSchema", static _ => { });
        var migration2 = new DelegateMigration("0001_CreateSchema", static _ => { });

        var plan = new SQLiteDbMigrationPlan();

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.AddScope("core", [migration1, migration2]));
    }

    [TestMethod]
    public void MigrationPlan_SelfDependency_ThrowsDbMigrationException()
    {
        var migration = new DelegateMigration(
            "0001_CreateSchema",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("core", "0001_CreateSchema")
            ]);

        var plan = new SQLiteDbMigrationPlan();

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.AddScope("core", [migration]));
    }

    [TestMethod]
    public void MigrationPlan_DuplicateDependency_ThrowsDbMigrationException()
    {
        var migration = new DelegateMigration(
            "0001_CreateSchema",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("module.video", "0001_CreateVideoSchema"),
                new SQLiteDbMigrationDependency("module.video", "0001_CreateVideoSchema")
            ]);

        var plan = new SQLiteDbMigrationPlan();

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.AddScope("core", [migration]));
    }

    [TestMethod]
    public void MigrationPlan_MissingRequiredScope_ThrowsDbMigrationException()
    {
        var migration = new DelegateMigration(
            "0001_Core",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("module.video", "0001_Video")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [migration]);

        Assert.ThrowsExactly<DbMigrationException>(() =>
            plan.Migrate(CreateConnection, CancellationToken.None));

        Assert.AreEqual(0, migration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public void MigrationPlan_EmptyPlan_DoesNotCreateMigrationHistory()
    {
        var plan = new SQLiteDbMigrationPlan();

        plan.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
    }

    [TestMethod]
    public void MigrationPlan_ScopeVersions_RecordsConfiguredVersions()
    {
        var coreMigration = new DelegateMigration("0001_Core", static _ => { });

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration], "2.0.0")
            .AddScope("module.video", [videoMigration], "1.5.0");

        plan.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        Assert.AreEqual("2.0.0", GetAppliedMigrationVersion(connection, 1, "core"));
        Assert.AreEqual("1.5.0", GetAppliedMigrationVersion(connection, 1, "module.video"));
    }

    [TestMethod]
    public void MigrationPlan_Dependencies_RecordsDiagnosticInformation()
    {
        var coreMigration = new DelegateMigration("0001_Core", static _ => { });

        var videoMigration = new DelegateMigration(
            "0001_Video",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("core", "0001_Core"),
                new SQLiteDbMigrationDependency("module.storage", "0002_Storage", optionalScope: true)
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [coreMigration])
            .AddScope("module.video", [videoMigration]);

        plan.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        Assert.IsNull(GetAppliedMigrationDependencies(connection, 1, "core"));
        Assert.AreEqual("core/0001_Core; module.storage/0002_Storage?", GetAppliedMigrationDependencies(connection, 1, "module.video"));
    }

    [TestMethod]
    public void Migrate_RecordsNullDependencies()
    {
        var migration = new DelegateMigration("0001_CreateSchema", static _ => { });
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        migrations.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        Assert.IsNull(GetAppliedMigrationDependencies(connection, 1));
    }

    [TestMethod]
    public void MigrationPlan_Dependencies_PreservesConfiguredOrder()
    {
        var migration1 = new DelegateMigration("0001_Core", static _ => { });
        var migration2 = new DelegateMigration("0001_Storage", static _ => { });
        var migration3 = new DelegateMigration(
            "0001_Video",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("module.storage", "0001_Storage"),
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);

        var plan = new SQLiteDbMigrationPlan()
            .AddScope("core", [migration1])
            .AddScope("module.storage", [migration2])
            .AddScope("module.video", [migration3]);

        plan.Migrate(CreateConnection, CancellationToken.None);

        using var connection = CreateConnection();
        Assert.AreEqual("module.storage/0001_Storage; core/0001_Core", GetAppliedMigrationDependencies(connection, 1, "module.video"));
    }

    [TestMethod]
    public void MigrateScope_MigrationWithDependencies_ThrowsDbMigrationException()
    {
        var migration = new DelegateMigration(
            "0001_Video",
            static _ => { },
            [
                new SQLiteDbMigrationDependency("core", "0001_Core")
            ]);
        SQLiteDbMigration[] migrations =
        [
            migration
        ];

        Assert.ThrowsExactly<DbMigrationException>(() =>
            migrations.MigrateScope(CreateConnection, "module.video", CancellationToken.None));

        Assert.AreEqual(0, migration.ExecutionCount);

        using var connection = CreateConnection();
        Assert.IsFalse(connection.TableExists("__nuext_migrations", CancellationToken.None));
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

    private static long GetAppliedMigrationCount(SQLiteDbConnection connection, string scope)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT COUNT(*)
            FROM __nuext_migrations
            WHERE scope = @scope;
            """,
            DbType.String.CreateInputParam("@scope", scope));
        Assert.IsNotNull(result);
        return Convert.ToInt64(result);
    }

    private static string GetAppliedMigrationScope(SQLiteDbConnection connection, long id)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT scope
            FROM __nuext_migrations
            WHERE id = @id;
            """,
            DbType.Int64.CreateInputParam("@id", id));
        Assert.IsNotNull(result);
        return Convert.ToString(result)!;
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
                id INTEGER PRIMARY KEY,
                scope TEXT NOT NULL CHECK(scope <> ''),
                sequence INTEGER NOT NULL CHECK(sequence > 0),
                migration_id TEXT NOT NULL CHECK(migration_id <> ''),
                dependencies TEXT,
                version TEXT,
                applied_at INTEGER NOT NULL,
                UNIQUE(scope, sequence),
                UNIQUE(scope, migration_id)
            );
            """);
    }

    private static void AddAppliedMigration(SQLiteDbConnection connection, string migrationId, int sequence, string scope = DefaultScope, string? version = null)
    {
        connection.ExecuteNonQuery(
            """
            INSERT INTO __nuext_migrations (scope, sequence, migration_id, version, applied_at)
            VALUES (@scope, @sequence, @migration_id, @version, @applied_at);
            """,
            DbType.String.CreateInputParam("@scope", scope),
            DbType.Int32.CreateInputParam("@sequence", sequence),
            DbType.String.CreateInputParam("@migration_id", migrationId),
            DbType.String.CreateInputParam("@version", version),
            DbType.Int64.CreateInputParam("@applied_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

    private static string GetAppliedMigrationId(SQLiteDbConnection connection, int sequence, string scope = DefaultScope)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT migration_id
            FROM __nuext_migrations
            WHERE scope = @scope AND sequence = @sequence;
            """,
            DbType.String.CreateInputParam("@scope", scope),
            DbType.Int32.CreateInputParam("@sequence", sequence));
        Assert.IsNotNull(result);
        return Convert.ToString(result)!;
    }

    private static string? GetAppliedMigrationDependencies(SQLiteDbConnection connection, int sequence, string scope = DefaultScope)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT dependencies
            FROM __nuext_migrations
            WHERE scope = @scope AND sequence = @sequence;
            """,
            DbType.String.CreateInputParam("@scope", scope),
            DbType.Int32.CreateInputParam("@sequence", sequence));
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    private static string? GetAppliedMigrationVersion(SQLiteDbConnection connection, int sequence, string scope = DefaultScope)
    {
        object? result = connection.ExecuteScalar(
            """
            SELECT version
            FROM __nuext_migrations
            WHERE scope = @scope AND sequence = @sequence;
            """,
            DbType.String.CreateInputParam("@scope", scope),
            DbType.Int32.CreateInputParam("@sequence", sequence));
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    private sealed class DelegateMigration : SQLiteDbMigration
    {
        private readonly Action<SQLiteDbMigrationContext> _up;
        private int _executionCount;

        private readonly IReadOnlyList<SQLiteDbMigrationDependency> _dependencies;

        public DelegateMigration(string id, Action<SQLiteDbMigrationContext> up, IReadOnlyList<SQLiteDbMigrationDependency>? dependencies = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(up);

            Id = id;
            _up = up;
            _dependencies = dependencies ?? [];
        }

        public override string Id { get; }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public override IReadOnlyList<SQLiteDbMigrationDependency> Dependencies => _dependencies;

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