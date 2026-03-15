using System.Diagnostics;

namespace System.Data.SQLite;

public static class SQLiteParameterExtensions
{
    /// <summary>
    /// Creates an input parameter for a SQLite command with the specified database type, parameter name, and optional value.
    /// </summary>
    /// <param name="dbType">The database type of the parameter.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="objValue">The value of the parameter. Defaults to null.</param>
    /// <returns>A new instance of <see cref="SQLiteParameter"/> configured as an input parameter.</returns>
    /// <exception cref="ArgumentException">Thrown when the parameter name is null, empty, or does not start with '@'.</exception>
    public static SQLiteParameter CreateInputParam(this DbType dbType, string parameterName, object? objValue = null)
    {
        Debug.Assert(!string.IsNullOrEmpty(parameterName), $"{nameof(parameterName)} is null or empty");
        Debug.Assert(parameterName?.StartsWith('@') == true, $"Parameter name '{parameterName}' should starts with '@'");
        ArgumentException.ThrowIfNullOrEmpty(parameterName);
        Throw.ArgumentExceptionIf(parameterName.StartsWith('@') != true, $"Parameter '{parameterName}' should starts with '@'");

        var param = new SQLiteParameter(parameterName, dbType);
        if (objValue is null)
        {
            param.IsNullable = true;
            param.Value = DBNull.Value;
        }
        else
        {
            param.Value = objValue;
        }
        return param;
    }

    /// <summary>
    /// Creates a source parameter for a SQLite command with the specified database type, parameter name, and source column.
    /// </summary>
    /// <param name="dbType">The database type of the parameter.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="sourceColumn">The name of the source column.</param>
    /// <returns>A new instance of <see cref="SQLiteParameter"/> configured as a source parameter.</returns>
    /// <exception cref="ArgumentException">Thrown when the parameter name or source column is null, empty, or the parameter name does not start with '@'.</exception>
    public static SQLiteParameter CreateSourceParam(this DbType dbType, string parameterName, string sourceColumn)
    {
        Debug.Assert(!string.IsNullOrEmpty(parameterName), $"{nameof(parameterName)} is null or empty");
        Debug.Assert(parameterName?.StartsWith('@') == true, $"Parameter name '{parameterName}' should starts with '@'");
        ArgumentException.ThrowIfNullOrEmpty(parameterName);
        ArgumentException.ThrowIfNullOrEmpty(sourceColumn);
        Throw.ArgumentExceptionIf(parameterName.StartsWith('@') != true, $"Parameter '{parameterName}' should starts with '@'");

        return new SQLiteParameter(parameterName, dbType, sourceColumn);
    }

    /// <summary>
    /// Creates a source parameter for a SQLite command with the specified database type, parameter name, source column, and data row version.
    /// </summary>
    /// <param name="dbType">The database type of the parameter.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="sourceColumn">The name of the source column.</param>
    /// <param name="rowVersion">The version of the data row.</param>
    /// <returns>A new instance of <see cref="SQLiteParameter"/> configured as a source parameter.</returns>
    /// <exception cref="ArgumentException">Thrown when the parameter name or source column is null, empty, or the parameter name does not start with '@'.</exception>
    public static SQLiteParameter CreateSourceParam(this DbType dbType, string parameterName, string sourceColumn, DataRowVersion rowVersion)
    {
        Debug.Assert(!string.IsNullOrEmpty(parameterName), $"{nameof(parameterName)} is null or empty");
        Debug.Assert(parameterName?.StartsWith('@') == true, $"Parameter name '{parameterName}' should starts with '@'");
        ArgumentException.ThrowIfNullOrEmpty(parameterName);
        ArgumentException.ThrowIfNullOrEmpty(sourceColumn);
        Throw.ArgumentExceptionIf(parameterName.StartsWith('@') != true, $"Parameter '{parameterName}' should starts with '@'");

        return new SQLiteParameter(parameterName, dbType, sourceColumn, rowVersion);
    }
}
