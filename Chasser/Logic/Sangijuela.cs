using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Moves;

namespace Chasser.Logic
{
    public class Sanguijuela : Piece
    {
        public override PieceType Type => PieceType.Sanguijuela;
        private readonly Direction forward;

        public override Player Color { get; }

        public Sanguijuela(Player color)
        {
            Color = color;
        }

        public override Piece Copy()
        {
            Sanguijuela copy = new Sanguijuela(Color);
            copy.HasMoved = HasMoved;
            return copy;
        }
        private static bool CanMoveTo(Position pos, Board board)
        {
            return Board.isInside(pos) && board.isEmpty(pos);
        }

        //private bool CanCaptureAt(Position pos, Board board)
        //{
        //    if (!Board.isInside(pos) || board.isEmpty(pos))
        //    {
        //        return false;
        //    }
        //}



        private IEnumerable<Move> ForwardMoves(Position from, Board board)
        {
            Position oneMovePos = from + forward;

            if (CanMoveTo(from, board))
            {
                yield return new NormalMove(from, oneMovePos);
                Position twoMovesPos = oneMovePos + forward;

                if (CanMoveTo(twoMovesPos, board))
                {
                    yield return new NormalMove(from, twoMovesPos);
                }
            }
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return ForwardMoves(from, board);
        }
    }
}
