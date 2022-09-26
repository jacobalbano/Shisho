using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Models
{
    public record class UserParticipation
    {
        public ReadingReport FirstReport { get; init; }
        public ReadingReport LatestReport { get; init; }

        public int BestStreak { get; init; }
        public int CurrentStreak { get; init; }
        public int TotalReports { get; init; }

        public int Consistency { get; init; }
        public Instant RoleExpires { get; init; }
    }
}
