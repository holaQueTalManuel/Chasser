using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Logic.Enums;

namespace Chasser.Common.Logic.Board
{
    public class Position
    {
        public int Row { get; }
        public int Column { get; }

        public Position(int row, int column)
        {
            Row = row; Column = column;
        }
        public Player SquareColor()
        {
            if ((Row + Column) % 2 == 0) 
            {
                return Player.White;
            }
            return Player.Black;
        }
        public static Position operator +(Position pos, Direction dir)
        {
            return new Position(pos.Row + dir.RowDelta, 
                pos.Column + dir.ColumnDelta);
        }

        public override bool Equals(object? obj)
        {
            return obj is Position position &&
                   Row == position.Row &&
                   Column == position.Column;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Column);
        }

        public static bool operator ==(Position? left, Position? right)
        {
            return EqualityComparer<Position>.Default.Equals(left, right);
        }
        public static bool TryParse(string input, out Position pos)
        {
            pos = null;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var cleaned = input.Trim('(', ')');
            var parts = cleaned.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int row) &&
                int.TryParse(parts[1], out int col))
            {
                pos = new Position(row, col);
                return true;
            }

            return false;
        }
        public static bool operator !=(Position? left, Position? right)
        {
            return !(left == right);
        }
        public override string ToString() => $"({Row},{Column})";
    }
}
