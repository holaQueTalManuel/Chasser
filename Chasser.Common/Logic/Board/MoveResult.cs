using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Logic.Board;

namespace Chasser.Common.Logic.Board
{
    public class MoveResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public Piece CapturedPiece { get; set; }
        public bool GameOver { get; set; }
        public Player? Winner { get; set; }
        public TimeSpan? GameDuration { get; set; }

        public static MoveResult FromValidation(MoveValidationResult validation) =>
            new MoveResult { IsValid = false, ErrorMessage = validation.ErrorMessage };

        public static MoveResult GameAlreadyOver =>
            new MoveResult { IsValid = false, ErrorMessage = "El juego ya ha terminado" };
    }
}
