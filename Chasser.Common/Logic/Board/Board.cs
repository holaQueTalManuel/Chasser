using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Common.Logic.Board
{
    public class Board
    {
        private readonly Piece[,] pieces = new Piece[7, 7];

        public Piece this[int row, int column]
        {
            get {
                return pieces[row, column];
            }
            set { pieces[row, column] = value; }
        }

        public Piece this[Position pos]
        {
            get { return this[pos.Row, pos.Column]; }
            set {  this[pos.Row, pos.Column] = value; }
        }

        public static Board Initialize()
        {
            Board board = new Board();
            board.AddStartPieces();
            return board;
        }

        private void AddStartPieces()
        {
            this[0, 0] = new Obliterador(Player.Black);
            this[6, 0] = new Tonel(Player.White);
            this[2, 3] = new Sanguijuela(Player.Black);
            this[4, 3] = new Sanguijuela(Player.White);


            this[0, 6] = new Tonel(Player.Black);
            this[6, 6] = new Obliterador(Player.White);
        }
        public IEnumerable<(Position pos, Piece piece)> AllPieces()
        {
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var piece = pieces[row, col];
                    if (piece != null)
                    {
                        yield return (new Position(row, col), piece);
                    }
                }
            }
        }

        public bool SoloQuedanSanguijuelas()
        {
            var sanguijuelas = AllPieces()
                .Where(p => p.piece is Sanguijuela)
                .Select(p => (Sanguijuela)p.piece)
                .ToList();

            return sanguijuelas.Count == 2 &&
                   sanguijuelas[0].Color != sanguijuelas[1].Color &&
                   AllPieces().Count() == 2;
        }
        public static bool isInside(Position pos)
        {
            return pos.Row >= 0 && pos.Row < 7 
                && pos.Column >= 0 && pos.Column < 7; 
        }

        public bool isEmpty(Position pos)
        {
            return this[pos] == null;
        }
    }
}
