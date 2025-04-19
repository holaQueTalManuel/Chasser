using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Logic
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
