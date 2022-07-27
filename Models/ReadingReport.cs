using NodaTime;
using Shisho.TypeConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shisho.Models
{
    public record class ReadingReport : ModelBase
    {
        public ulong UserDiscordId { get; init; }

        public ulong MessageDiscordId { get; init; }

        [JsonConverter(typeof(NodaInstantJsonConverter))]
        [SqliteConverter(typeof(NodaInstantSqliteConverter))]
        public Instant ReportMessageInstant { get; init; }

        public Guid DeadlineKey { get; init; }
    }
}
