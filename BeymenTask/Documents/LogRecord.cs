using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeymenTask.Documents
{
    public class LogRecord : Document
    {
        public string Message { get; set; }
        public string ExceptionDetails { get; set; }
    }
}
