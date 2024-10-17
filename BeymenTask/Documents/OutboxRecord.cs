using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeymenTask.Documents
{
    public class OutboxRecord : Document
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public bool Published { get; set; }
    }
}
