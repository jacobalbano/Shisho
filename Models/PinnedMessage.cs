using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Models
{
    public record class PinnedMessage : ModelBase
    {
        public Guid DeadlineKey { get; init; }

        public ulong MessageDiscordId { get; init; }
    }
}
