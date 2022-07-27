using Microsoft.Data.Sqlite;
using Shisho.TypeConverters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Utility.Persistence.SqliteSupport;

public class TypeMapping
{
    public static TypeMapping Get<T>() => Cache<T>.Mapping;

    public string Name { get; init; }

    public IReadOnlyList<IPropertyMapping> Properties { get; init; }

    public IReadOnlyList<SqliteParameter> BuildInsertParameters<T>(T item)
    {
        return Properties.Select(x => new SqliteParameter($"${x.Name}", x.TypeConverter.ToParameterValue(x.Accessor.GetValue(item))))
            .ToList();
    }

    public string BuildInsertStatement()
    {
        return $"INSERT INTO {Name} ({ string.Join(',', Properties.Select(x => x.Name))}) VALUES ({ string.Join(',', Properties.Select(x => $"${x.Name}")) })";
    }

    public string BuildSelectStatement()
    {
        return $"SELECT * FROM {Name}";
    }

    public string BuildCreateTableStatement()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {Name} (");
        sb.AppendLine($"   {Name}_pk INTEGER PRIMARY KEY");

        foreach (var p in Properties)
            sb.AppendLine(@$"   ,{p.Name} { p.TypeConverter.GetSqliteType() }");

        sb.AppendLine(@");");
        return sb.ToString();
    }

    public T CreateFromDataRow<T>(SqliteDataReader reader)
    {
        var columns = EstablishColumnMapping(reader);
        var result = Activator.CreateInstance<T>();
        foreach (var prop in Properties)
        {
            if (columns.TryGetValue(prop.Name, out var colIndex))
                prop.Accessor.SetValue(result, prop.TypeConverter.FromDataReader(reader, colIndex));
        }

        return result;
    }

    private Dictionary<string, int> EstablishColumnMapping(SqliteDataReader reader)
    {
        if (columnMapping == null)
        {
            var schema = reader.GetSchemaTable();
            columnMapping = schema.Rows.Cast<DataRow>()
                .Select((row, i) => (row, i))
                .ToDictionary(x => (string) x.row[SchemaTableColumn.ColumnName], x => x.i);
        }
        
        return columnMapping;
    }

    public interface IPropertyMapping
    {
        public Type Type { get; }
        public string Name { get; }
        public ISqliteConverter TypeConverter { get; }
        public FastAccessor Accessor { get; init; }
    }

    private TypeMapping() { }

    private static class Cache<T>
    {
        public static readonly TypeMapping Mapping = Construct();

        private static TypeMapping Construct()
        {
            var props = new List<IPropertyMapping>();
            foreach (var p in typeof(T).GetProperties())
            {
                ISqliteConverter converter = null;
                if (p.GetCustomAttribute<SqliteConverterAttribute>() is SqliteConverterAttribute attr)
                    converter = Activator.CreateInstance(attr.ConverterType) as ISqliteConverter;
                else if (sqlTypeConverters.TryGetValue(p.PropertyType, out var builtin))
                    converter = builtin;

                props.Add(new PropertyMapping
                {
                    Name = p.Name,
                    Type = p.PropertyType,
                    TypeConverter = converter ?? new DefaultSqliteConverter { Type = p.PropertyType },
                    Accessor = new FastAccessor(p)
                });
            }

            return new TypeMapping
            {
                Name = typeof(T).Name,
                Properties = props,
            };
        }
    }

    private class DefaultSqliteConverter : ISqliteConverter
    {
        public Type Type { get; init; }

        public object FromDataReader(SqliteDataReader reader, int colIndex)
        {
            switch (Type.GetTypeCode(Type))
            {
                case TypeCode.Boolean:  return reader.GetBoolean(colIndex);
                case TypeCode.SByte:    return (sbyte) reader.GetByte(colIndex);
                case TypeCode.Byte:     return reader.GetByte(colIndex);
                case TypeCode.Int16:    return reader.GetInt16(colIndex);
                case TypeCode.UInt16:   return (ushort) reader.GetInt16(colIndex);
                case TypeCode.Int32:    return reader.GetInt32(colIndex);
                case TypeCode.UInt32:   return (uint) reader.GetInt32(colIndex);
                case TypeCode.Int64:    return reader.GetInt64(colIndex);
                case TypeCode.UInt64:   return (ulong) reader.GetInt64(colIndex);
                case TypeCode.Single:   return reader.GetFloat(colIndex);
                case TypeCode.Double:   return reader.GetDouble(colIndex);
                case TypeCode.Char:     return reader.GetChar(colIndex);
                case TypeCode.Decimal:  return reader.GetDecimal(colIndex);
                case TypeCode.String:   return reader.GetString(colIndex);
                default: throw new Exception($"Unhandled property type {Type}");
            }
        }

        public SqliteType GetSqliteType()
        {
            switch (Type.GetTypeCode(Type))
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return SqliteType.Integer;
                case TypeCode.Single:
                case TypeCode.Double:
                    return SqliteType.Real;
                case TypeCode.Char:
                case TypeCode.Decimal:
                case TypeCode.String:
                    return SqliteType.Text;
                default:
                    throw new Exception($"Unhandled property type {Type}");
            }
        }

        public object ToParameterValue(object obj)
        {
            switch (Type.GetTypeCode(Type))
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Char:
                case TypeCode.Decimal:
                case TypeCode.String:
                    return obj;
                default:
                    throw new Exception($"Unhandled property type {Type}");
            }
        }
    }

    private Dictionary<string, int> columnMapping;

    private static readonly Dictionary<Type, ISqliteConverter> sqlTypeConverters = new()
    {
        { typeof(Guid), new GuidSqliteConverter() },
        { typeof(byte[]), new ByteArraySqliteConverter() },
    };

    private class PropertyMapping : IPropertyMapping
    {
        public Type Type { get; init;  }
        public string Name { get; init; }
        public ISqliteConverter TypeConverter { get; init; }
        public FastAccessor Accessor { get; init; }
    }
}
