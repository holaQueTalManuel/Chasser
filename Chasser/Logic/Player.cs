using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Logic
{
    public enum Player
    {
        None, White, Black
    }
    public static class PlayerExtensions
    {
        public static int CantEaten { get; set; } = 0;
        public static Player Opponent(this Player player)
        {
            return player switch
            {
                Player.White => Player.Black,
                Player.Black => Player.White,
                _ => Player.None,
            };
        }
    }
}
