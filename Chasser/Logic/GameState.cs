using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Moves;

namespace Chasser.Logic
{
    public class GameState
    {
        public GameState(Player player, Board board)
        {
            CurrentPlayer = player;
            Board = board;
        }

        public Board Board {  get; }
        public Player CurrentPlayer { get; private set; }

        public IEnumerable<Move> LegalMovesForPiece(Position pos)
        {
            if (Board.isEmpty(pos) || Board[pos].Color != CurrentPlayer)
            {
                return Enumerable.Empty<Move>();
            }
            Piece piece = Board[pos];
            return piece.GetMoves(pos, Board);
        }

        public void MakeMove(Move move)
        {
            move.Execute(Board);
            CurrentPlayer = CurrentPlayer.Opponent();
        }




        //public string JugadorBlancas { get; set; }
        //public string JugadorNegras { get; set; }
        //public string TurnoActual { get; set; } // "Blancas" o "Negras"
        //public List<string> Movimientos { get; set; } // Historial de movimientos
        //public string[,] Tablero { get; set; } // Matriz 8x8 con piezas
    }

}
