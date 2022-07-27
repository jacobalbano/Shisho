using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Models
{
    public class DataExport
    {
        public IReadOnlyList<Item> Items { get; init; }

        public class Item
        {
            public ReadingDeadline Deadline { get; init; }

            public IReadOnlyList<ReadingReport> Reports { get; init; }
        }
    }
}
