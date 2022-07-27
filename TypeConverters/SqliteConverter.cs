using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.TypeConverters
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SqliteConverterAttribute : Attribute
    {
        public Type ConverterType { get; }

        public SqliteConverterAttribute(Type converterType)
        {
            ConverterType = converterType;
        }
    }

    public interface ISqliteConverter
    {
        object FromDataReader(SqliteDataReader reader, int colIndex);
        SqliteType GetSqliteType();
        object ToParameterValue(object obj);
    }

    public abstract class SqliteConverter<T> : ISqliteConverter
    {
        public abstract T FromDataReader(SqliteDataReader reader, int colIndex);
        public abstract object ToParameterValue(T obj);
        public abstract SqliteType GetSqliteType();

        object ISqliteConverter.FromDataReader(SqliteDataReader reader, int colIndex) => FromDataReader(reader, colIndex)!;
        SqliteType ISqliteConverter.GetSqliteType() => GetSqliteType();
        object ISqliteConverter.ToParameterValue(object obj) => ToParameterValue((T) obj);
    }
}
