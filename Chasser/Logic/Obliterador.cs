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
        public bool HasCaptured { get; set; } = false;


        public Obliterador(Player color)
        {
            Color = color;

            if (color == Player.White)
                forward = Direction.NorthWest;
            else if (color == Player.Black)
                forward = Direction.SouthEast;
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
            HasCaptured = true;
            return board[pos].Color != Color;
        }

        private bool CanMoveTo(Position pos, Board board)
        {
            // Si intenta moverse al centro y no ha capturado antes: prohibido
            if (pos.Row == 3 && pos.Column == 3 && !HasCaptured)
                return false;

            return Board.isInside(pos) && board.isEmpty(pos);
        }


        public IEnumerable<Move> DiagonalMoves(Position from, Board board)
        {
            foreach (Direction dir in dirs)
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
                        if (CanMoveTo(to, board))
                            yield return new NormalMove(from, to);
                    }
                    else
                    {
                        if (board[to].Color != Color)
                        {
                            HasCaptured = true;
                            yield return new NormalMove(from, to); // captura
                        }
                   
                    }
                }
            }
        }


    }
}

