using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertTable
{
    interface ITableReader
    {
        string Name { get; }
        IEnumerable<IList<object>> Read(string input);
    }
}
