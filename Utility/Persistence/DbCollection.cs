using Shisho.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Utility.Persistence
{
    public class DbCollection<TItem>// : IList<TItem> where TItem : ModelBase
    {

        private readonly List<TItem> storage;
    }
}
