using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Logic.Moves;


namespace Chasser.Common.Logic.Board
{
    public abstract class Piece
    {
        public abstract PieceType Type { get; }
        public abstract Player Color { get; }
        public bool HasMoved { get; set; } = false;

        public abstract Piece Copy();

        public abstract IEnumerable<Move> GetMoves(Position from, Board board);

        public IEnumerable<Position> MovePositionsInDir(Position from, Board board, Direction dir)
        {
            for (Position pos = from + dir; Board.isInside(pos); pos += dir)
            {
                if (board.isEmpty(pos))
                {
                    yield return pos;
                    continue;
                }
                Piece piece = board[pos];

                //si es del oponente
                if (piece.Color != Color)
                {
                    yield return pos;
                }
                //si es aliado
                yield break;
            }
        }

        public IEnumerable<Position> MovePositionsInDirs(Position from, Board board, Direction[] dirs)
        {
            return dirs.SelectMany(dir => MovePositionsInDir(from, board, dir));
        }
    }
}
