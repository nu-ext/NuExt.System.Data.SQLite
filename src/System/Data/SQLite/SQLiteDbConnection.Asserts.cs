using System.ComponentModel;
using System.Diagnostics;

namespace System.Data.SQLite;

partial class SQLiteDbConnection
{
    #region Internal classes

    private class AssertsSuspender : IDisposable
    {
        private readonly SQLiteDbConnection _conn;
        private readonly bool _assertsDisabled;

        public AssertsSuspender(SQLiteDbConnection conn)
        {
            _conn = conn;
            _conn.DisableAsserts(ref _assertsDisabled);
        }

        public void Dispose()
        {
            _conn.RestoreAsserts(_assertsDisabled);
            GC.SuppressFinalize(this);
        }
    }

    #endregion

    private bool _assertsDisabled;

    #region Properties

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool AssertsDisabled => _assertsDisabled;

    #endregion

    #region Methods

    [Conditional("DEBUG")]
    private void DisableAsserts(ref bool originalState)
    {
        originalState = _assertsDisabled;
        if (originalState == false)
        {
            _assertsDisabled = true;
        }
    }

    [Conditional("DEBUG")]
    private void RestoreAsserts(bool originalState)
    {
        if (originalState == false)
        {
            _assertsDisabled = false;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public IDisposable SuspendAsserts()
    {
        return new AssertsSuspender(this);
    }

    #endregion
}
