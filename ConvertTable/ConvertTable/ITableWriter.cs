using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertTable
{
    interface ITableWriter
    {
        string Name { get; }
        void Write(string output, IEnumerable<IList<object>> items);
    }
}
