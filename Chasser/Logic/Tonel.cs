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
        private Player CurrentPlayer;
        private static readonly Direction[] Directions = new[]
        {
            Direction.North, Direction.South, Direction.East, Direction.West
        };
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
            //tonel no podra comerse a nadie, solo podra entrar a la meta
            foreach (var dir in Directions)
            {
                Position oneStep = from + dir;

                if (CanMoveTo(oneStep, board))
                {
                    yield return new NormalMove(from, oneStep);

                    Position twoSteps = oneStep + dir;
                    if (CanMoveTo(twoSteps, board))
                    {
                        yield return new NormalMove(from, twoSteps);
                        
                    }

                    
                }
                

            }
        }
        //esto va a ser para los diagonales que ira en obliteradores (de momento)
        

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
