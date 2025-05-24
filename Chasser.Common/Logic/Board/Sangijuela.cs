using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Logic.Moves;


namespace Chasser.Common.Logic.Board
{
    public class Sanguijuela : Piece
    {
        public override PieceType Type => PieceType.Sanguijuela;
        private readonly Direction forward;

        private static readonly Direction[] Directions = new[]
        {
            Direction.North, Direction.South, Direction.East, Direction.West
        };

        public override Player Color { get; }

        public Sanguijuela(Player color)
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

        public override Piece Copy()
        {
            Sanguijuela copy = new Sanguijuela(Color);
            copy.HasMoved = HasMoved;
            return copy;
        }
        private static bool CanMoveTo(Position pos, Board board)
        {
            // no permitir moverse a la casilla central
            if (pos.Row == 3 && pos.Column == 3)
                return false;

            return Board.isInside(pos) && board.isEmpty(pos);

        }

        private bool CanCaptureAt(Position pos, Board board)
        {
            if (!Board.isInside(pos) || board.isEmpty(pos))
                return false;

            var target = board[pos];

            // No puede capturar a piezas de su mismo color
            if (target.Color == Color)
                return false;

            // No puede capturar a otra Sanguijuela




            // Solo puede capturar a Tonel u Obliterador
            return target.Type == PieceType.Tonel || target.Type == PieceType.Obliterador;
        }



        private IEnumerable<Move> ForwardMoves(Position from, Board board)
        {
            foreach (var dir in Directions)
            {
                Position oneStep = from + dir;

                if (CanMoveTo(oneStep, board))
                {
                    yield return new NormalMove(from, oneStep);
                }
                else if (CanCaptureAt(oneStep, board))
                {
                    yield return new NormalMove(from, oneStep);
                }
            }
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return ForwardMoves(from, board);
        }
    }
}