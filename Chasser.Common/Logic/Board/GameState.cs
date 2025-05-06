using System;
using System.Collections.Generic;
using System.Linq;
using Chasser.Moves;

namespace Chasser.Logic.Board
{
    public class GameState
    {
        public Board Board { get; }
        public Player CurrentPlayer { get; private set; }
        public GameResult Result { get; private set; }
        public DateTime StartTime { get; }
        public DateTime? EndTime { get; private set; }

        private Dictionary<Player, int> eliminations = new Dictionary<Player, int>
        {
            { Player.White, 0 },
            { Player.Black, 0 }
        };

        public Dictionary<Player, int> Players { get; } = new Dictionary<Player, int>();

        public GameState(Player startingPlayer, Board board)
        {
            CurrentPlayer = startingPlayer;
            Board = board;
            StartTime = DateTime.UtcNow;
        }

        public void RegisterPlayer(Player color, int userId)
        {
            Players[color] = userId;
        }

        public IEnumerable<Move> GetLegalMoves(Position pos)
        {
            if (Board.isEmpty(pos) || Board[pos].Color != CurrentPlayer)
            {
                return Enumerable.Empty<Move>();
            }

            Piece piece = Board[pos];
            return piece.GetMoves(pos, Board);
        }

        public MoveValidationResult ValidateMove(Move move)
        {
            // 1. Validar que hay pieza en la posición de origen
            if (Board.isEmpty(move.FromPos))
                return MoveValidationResult.Invalid("No hay pieza en la posición de origen");

            // 2. Validar que es el turno del jugador
            if (Board[move.FromPos].Color != CurrentPlayer)
                return MoveValidationResult.Invalid("No es tu turno");

            // 3. Validar que el movimiento es legal
            var legalMoves = GetLegalMoves(move.FromPos);
            if (!legalMoves.Any(m => m.Equals(move)))
                return MoveValidationResult.Invalid("Movimiento no permitido");

            return MoveValidationResult.Valid();
        }

        public MoveResult ExecuteMove(Move move)
        {
            if (IsGameOver())
                return MoveResult.GameAlreadyOver;

            var validation = ValidateMove(move);
            if (!validation.IsValid)
                return MoveResult.FromValidation(validation);

            Piece capturedPiece = Board[move.ToPos];
            move.Execute(Board);

            if (capturedPiece != null)
            {
                eliminations[CurrentPlayer]++;
            }

            CurrentPlayer = CurrentPlayer.Opponent();
            CheckGameEndConditions();

            string reason = null;
            if (IsGameOver())
            {
                if (Result.IsDraw) reason = "Draw";
                else if (Result.VictoryType.HasValue) reason = Result.VictoryType.ToString();
            }

            return MoveResult.CreateValidResult(
                capturedPiece: capturedPiece,
                gameOver: IsGameOver(),
                winner: Result?.Winner,
                currentPlayer: CurrentPlayer,
                duration: IsGameOver() ? DateTime.UtcNow - StartTime : null,
                reason: reason
            );
        }

        private void CheckGameEndConditions()
        {
            // Victoria por control del centro (posición 3,3)
            Position center = new Position(3, 3);
            if (!Board.isEmpty(center) && Board[center].Color == CurrentPlayer.Opponent())
            {
                Result = GameResult.Victory(CurrentPlayer.Opponent(), VictoryType.CenterControl);
                return;
            }

            // Victoria por eliminaciones (3 piezas capturadas)
            if (eliminations[CurrentPlayer.Opponent()] >= 3)
            {
                Result = GameResult.Victory(CurrentPlayer.Opponent(), VictoryType.Elimination);
                return;
            }

            // Empate (no hay movimientos legales)
            if (!GetLegalMovesForPlayer(CurrentPlayer).Any())
            {
                Result = GameResult.Draw();
            }
        }

        private IEnumerable<Move> GetLegalMovesForPlayer(Player player)
        {
            // Buscar todas las posiciones con piezas del jugador
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var pos = new Position(row, col);
                    if (!Board.isEmpty(pos) && Board[pos].Color == player)
                    {
                        foreach (var move in GetLegalMoves(pos))
                        {
                            yield return move;
                        }
                    }
                }
            }
        }

        public bool IsGameOver() => Result != null;

        public TimeSpan? GetGameDuration() =>
            IsGameOver() ? EndTime - StartTime : null;
    }

    public class MoveValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        private MoveValidationResult(bool isValid, string errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static MoveValidationResult Valid() => new MoveValidationResult(true);
        public static MoveValidationResult Invalid(string message) => new MoveValidationResult(false, message);
    }

    public class MoveResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public Piece CapturedPiece { get; set; }
        public bool GameOver { get; set; }
        public Player? Winner { get; set; }
        public Player CurrentPlayer { get; set; }
        public TimeSpan? GameDuration { get; set; }
        public string GameOverReason { get; set; }

        public static MoveResult FromValidation(MoveValidationResult validation) =>
            new MoveResult
            {
                IsValid = false,
                ErrorMessage = validation.ErrorMessage
            };

        public static MoveResult GameAlreadyOver =>
            new MoveResult
            {
                IsValid = false,
                ErrorMessage = "El juego ya ha terminado",
                GameOverReason = "Game already ended"
            };

        public static MoveResult CreateValidResult(
            Piece capturedPiece,
            bool gameOver,
            Player? winner,
            Player currentPlayer,
            TimeSpan? duration,
            string reason)
        {
            return new MoveResult
            {
                IsValid = true,
                CapturedPiece = capturedPiece,
                GameOver = gameOver,
                Winner = winner,
                CurrentPlayer = currentPlayer,
                GameDuration = duration,
                GameOverReason = reason
            };
        }
    }

    public class GameResult
    {
        public Player? Winner { get; }
        public VictoryType? VictoryType { get; }
        public bool IsDraw { get; }

        private GameResult(Player? winner, VictoryType? victoryType, bool isDraw)
        {
            Winner = winner;
            VictoryType = victoryType;
            IsDraw = isDraw;
        }

        public static GameResult Victory(Player winner, VictoryType victoryType) =>
            new GameResult(winner, victoryType, false);

        public static GameResult Draw() =>
            new GameResult(null, null, true);
    }

    public enum VictoryType
    {
        Elimination,
        CenterControl,
        Timeout,
        Resignation
    }
}