using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Moves;

namespace Chasser.Logic
{
    public class Tonel : Piece
    {
        public override PieceType Type => PieceType.Tonel;
        private readonly Direction forward;
        public override Player Color { get; }

        public Tonel(Player color) 
        { 
            Color = color;

            if (color == Player.White)
            {
                forward = Direction.North;
            }
            else if (color == Player.Black)
            {
                forward = Direction.South;
            }

        }

        private static bool CanMoveTo(Position pos, Board board)
        {
            return Board.isInside(pos) && board.isEmpty(pos);
        }

        private bool CanCaptureAt(Position pos, Board board)
        {
            if (!Board.isInside(pos) || board.isEmpty(pos))
            {
                return false;
            }
            return board[pos].Color != Color;
        }

        private IEnumerable<Move> ForwardMoves(Position from, Board board)
        {
            Position oneMovePos = from + forward;

            if (CanMoveTo(oneMovePos, board))
            {
                yield return new NormalMove(from, oneMovePos);
                Position twoMovesPos = oneMovePos + forward;

                if (CanMoveTo(twoMovesPos, board))
                {
                    yield return new NormalMove(from, twoMovesPos);
                }
            }
        }
        //esto va a ser para los diagonales que ira en obliteradores (de momento)
        public IEnumerable<Move> DiagonalMoves(Position from, Board board)
        {
            foreach (Direction dir in new Direction[] {Direction.West, Direction.East})
            {
                Position to = from + forward + dir;
                if (CanCaptureAt(to,  board))
                {
                    yield return new NormalMove(from, to);
                }
            }
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return ForwardMoves(from, board);
        }

        public override Piece Copy()
        {
            Tonel copy = new Tonel(Color);
            copy.HasMoved = HasMoved;
            return copy;
        }

        
    }
}
