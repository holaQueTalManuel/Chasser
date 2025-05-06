using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Logic.Board;

namespace Chasser.Common.Logic.Board
{
    public class GameResult
    {
        public Player? Winner { get; }
        public VictoryType? VictoryType { get; }
        public bool IsDraw { get; }

        private GameResult(Player? winner, VictoryType? victoryType, bool isDraw)
        {
            Winner = winner;
            VictoryType = victoryType;
            IsDraw = isDraw;
        }

        public static GameResult Victory(Player winner, VictoryType victoryType) =>
            new GameResult(winner, victoryType, false);

        public static GameResult Draw() =>
            new GameResult(null, null, true);
    }

}
