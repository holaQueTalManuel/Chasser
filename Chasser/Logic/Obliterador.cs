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

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return MovePositionsInDirs(from, board, dirs).Select(
                to => new NormalMove(from, to));
        }
    }
}
