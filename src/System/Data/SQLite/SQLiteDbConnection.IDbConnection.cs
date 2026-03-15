namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Properties

    string IDbConnection.ConnectionString
    {
        get => _conn.ConnectionString;
#pragma warning disable CS8769
        set => throw new NotSupportedException();
#pragma warning restore CS8769
    }

    int IDbConnection.ConnectionTimeout => _conn.ConnectionTimeout;

    string IDbConnection.Database => _conn.Database;

    ConnectionState IDbConnection.State => _conn.State;

    #endregion

    #region Methods

    IDbTransaction IDbConnection.BeginTransaction()
    {
        return BeginTransaction();
    }

    IDbTransaction IDbConnection.BeginTransaction(IsolationLevel isolationLevel)
    {
        return BeginTransaction(isolationLevel);
    }

    void IDbConnection.ChangeDatabase(string databaseName)
    {
        throw new NotSupportedException();
    }

    void IDbConnection.Close()
    {
        Close();
    }

    IDbCommand IDbConnection.CreateCommand()
    {
        return CreateCommand();
    }

    void IDbConnection.Open()
    {
        Open();
    }

    #endregion
}
