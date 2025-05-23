using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Common.Model
{
    public class PlayerRank
    {
        public string Position { get; set; }
        public string Username { get; set; }
        public string Wins { get; set; }
        public double WinRate { get; set; }
    }
}
