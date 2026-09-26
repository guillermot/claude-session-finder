using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// Thin ADO helpers so the spike reads as SQL plus assertions rather than as plumbing.
/// </summary>
internal static class SqliteConnectionTestExtensions
{
    /// <summary>Runs a statement that returns no rows.</summary>
    public static void Execute(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Runs a parameterised statement and returns the number of affected rows.</summary>
    public static int Execute(this SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteNonQuery();
    }

    /// <summary>Reads the first column of the first row.</summary>
    public static T Scalar<T>(this SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var scalar = command.ExecuteScalar();

        return (T)Convert.ChangeType(scalar!, typeof(T), CultureInfo.InvariantCulture);
    }

    /// <summary>Reads the first column of every row as text, preserving SQL nulls.</summary>
    public static List<string?> QueryNullableStrings(
        this SqliteConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        using var reader = command.ExecuteReader();
        var results = new List<string?>();

        while (reader.Read())
        {
            results.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        }

        return results;
    }

    /// <summary>Reads the first column of every row as text.</summary>
    public static List<string> QueryStrings(
        this SqliteConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        using var reader = command.ExecuteReader();
        var results = new List<string>();

        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
