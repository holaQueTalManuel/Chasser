using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Common.Network
{
    public class RequestMessage
    {
        public string Command { get; set; }
        public Dictionary<string, string> Data { get; set; }
    }
}
