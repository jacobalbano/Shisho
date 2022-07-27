using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.TypeConverters
{
    public class ByteArraySqliteConverter : SqliteConverter<byte[]>
    {
        public override byte[] FromDataReader(SqliteDataReader reader, int colIndex)
        {
            throw new NotImplementedException();
        }

        public override SqliteType GetSqliteType()
        {
            return SqliteType.Blob;
        }

        public override object ToParameterValue(byte[] obj)
        {
            throw new NotImplementedException();
        }
    }
}
