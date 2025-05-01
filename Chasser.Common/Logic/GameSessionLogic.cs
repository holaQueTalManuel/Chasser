using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Logic.Board;
using Chasser.Moves;

namespace Chasser.Common.Logic
{
    public class GameSessionLogic
    {
        public GameState GameState { get; private set; }

        public GameSessionLogic()
        {
            GameState = new GameState(Player.White, Board.Initialize());
        }

        public bool TryMakeMove(Move move, out string error)
        {
            var legalMoves = GameState.LegalMovesForPiece(move.FromPos);
            if (legalMoves.Any(m => m.Equals(move)))
            {
                GameState.MakeMove(move);
                error = null;
                return true;
            }

            error = "Movimiento no válido";
            return false;
        }

        public bool IsGameOver() => GameState.IsGameOver();
    }
}
