using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Enums;


namespace Chasser.Utilities
{
    public static class Images
    {
        private static readonly Dictionary<PieceType, ImageSource> whiteSources = new()
        {
            {PieceType.Obliterador, LoadImage("/Imgs/horseSinRelleno.png") },
            { PieceType.Tonel, LoadImage("/Imgs/tonelSinRelleno.png") },
            {PieceType.Sanguijuela, LoadImage("/Imgs/sanguiRealFor.png") }
        };
        
        private static readonly Dictionary<PieceType, ImageSource> blackSources = new()
        {
            {PieceType.Obliterador, LoadImage("/Imgs/horseReal3.png") },
            { PieceType.Tonel, LoadImage("/Imgs/tonelConRelleno.png") },
            {PieceType.Sanguijuela, LoadImage("/Imgs/sangui2.png") }
        };

        public static ImageSource GetImage(Player color, PieceType type)
        {
            return color switch
            {
                Player.White => whiteSources[type],
                Player.Black => blackSources[type],
                _ => null
            };
        }
        public static ImageSource GetImage(Piece piece)
        {
            if (piece == null)
            {
                return null;
            }
            return GetImage(piece.Color, piece.Type);
        }

        private static ImageSource LoadImage(string filePath)
        {
            return new BitmapImage(new Uri(filePath, UriKind.Relative));
        }
    }
}
