using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.TypeConverters
{
    public class GuidSqliteConverter : SqliteConverter<Guid>
    {
        public override Guid FromDataReader(SqliteDataReader reader, int colIndex)
        {
            return Guid.Parse(reader.GetString(colIndex));
        }

        public override SqliteType GetSqliteType()
        {
            return SqliteType.Text;
        }

        public override object ToParameterValue(Guid obj)
        {
            return obj.ToString();
        }
    }
}
