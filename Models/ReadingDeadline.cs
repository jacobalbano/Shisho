using NodaTime;
using Shisho.TypeConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shisho.Models;

public record class ReadingDeadline : ModelBase
{
    [JsonConverter(typeof(NodaInstantJsonConverter)), SqliteConverter(typeof(NodaInstantSqliteConverter))]
    public Instant DeadlineInstant { get; init; }
}
