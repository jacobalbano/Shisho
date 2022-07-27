using Microsoft.Data.Sqlite;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.TypeConverters
{
    public class NodaInstantSqliteConverter : SqliteConverter<Instant>
    {
        public override Instant FromDataReader(SqliteDataReader reader, int colIndex)
        {
            return Instant.FromUnixTimeTicks(reader.GetInt64(colIndex));
        }

        public override SqliteType GetSqliteType()
        {
            return SqliteType.Integer;
        }

        public override object ToParameterValue(Instant obj)
        {
            return obj.ToUnixTimeTicks();
        }
    }
}
