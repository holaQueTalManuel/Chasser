using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Moves;

namespace Chasser.Logic
{
    public class Obliterador : Piece
    {
        private readonly Direction[] dirs = new Direction[]
        {
            Direction.NorthEast,
            Direction.SouthWest,
            Direction.SouthEast,
            Direction.NorthWest
        };
        public override PieceType Type => PieceType.Obliterador;

        public override Player Color { get; }
        private readonly Direction forward;


        public Obliterador(Player color)
        {
            Color = color;
        }

        public override Piece Copy()
        {
            Obliterador copy = new Obliterador(Color);
            copy.HasMoved = HasMoved;
            return copy;
        }

        private bool CanCaptureAt(Position pos, Board board)
        {
            if (!Board.isInside(pos) || board.isEmpty(pos))
            {
                return false;
            }
            return board[pos].Color != Color;
        }

        public IEnumerable<Move> DiagonalMoves(Position from, Board board)
        {
            foreach (Direction dir in new Direction[] { Direction.West, Direction.East })
            {
                Position to = from + forward + dir;
                if (CanCaptureAt(to, board))
                {
                    yield return new NormalMove(from, to);
                }
            }
        }

        
            public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            foreach (Direction dir in dirs)
            {
                for (int step = 1; step <= 2; step++)
                {
                    Position to = from + step * dir;

                    if (!Board.isInside(to))
                        break;

                    if (board.isEmpty(to))
                    {
                        yield return new NormalMove(from, to);
                    }
                    else
                    {
                        if (board[to].Color != Color)
                        {
                            yield return new NormalMove(from, to); // captura
                        }
                        break; // no puede seguir más allá
                    }
                }
            }
        }

    }
}

