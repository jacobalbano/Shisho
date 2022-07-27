using Microsoft.Data.Sqlite;
using Shisho.Models;
using Shisho.TypeConverters;
using Shisho.Utility.Persistence.SqliteSupport;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Utility.Persistence;

public class Database : IPersistable
{
    public Database(string dbFilename)
    {
        this.dbFilename = dbFilename;
    }

    public void LoadPersistentData(string fromDirectory)
    {
        conStr = $"Data Source={Path.Combine(fromDirectory, $"{dbFilename}.db")}";
    }

    public void Insert<T>(T item)
    {
        using var con = OpenDb();
        var mapping = TypeMapping.Get<T>();
        EstablishTable(mapping, con);

        var command = con.CreateCommand();
        command.CommandText = mapping.BuildInsertStatement();
        command.Parameters.AddRange(mapping.BuildInsertParameters(item));
        command.ExecuteNonQuery();
    }

    public IEnumerable<T> Select<T>()
    {
        using var con = OpenDb();
        var mapping = TypeMapping.Get<T>();
        EstablishTable(mapping, con);

        var command = con.CreateCommand();
        command.CommandText = mapping.BuildSelectStatement();
        using var reader = command.ExecuteReader();
        if (!reader.HasRows) yield break;

        while (reader.Read())
            yield return mapping.CreateFromDataRow<T>(reader);
    }

    private SqliteConnection OpenDb()
    {
        var connection = new SqliteConnection(conStr);
        connection.Open();
        return connection;
    }

    public void Persist(string toDirectory)
    {
    }

    private static void EstablishTable(TypeMapping mapping, SqliteConnection con)
    {
        using (var findTable = con.CreateCommand())
        {
            findTable.CommandText = @$"SELECT name FROM sqlite_master WHERE type='table' AND name='{mapping.Name}';";
            using var reader = findTable.ExecuteReader();
            while (reader.Read())
                return; // got one result, table exists
        }

        using (var makeTable = con.CreateCommand())
        {
            makeTable.CommandText = mapping.BuildCreateTableStatement();
            makeTable.ExecuteNonQuery();
        }
    }

    private readonly string dbFilename;
    private string conStr;
}
