using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Moves;

namespace Chasser.Common.Logic
{
    public class LocalAIGameEngine
    {
        private readonly GameState _gameState;
        private readonly Player _humanPlayer;
        private readonly Random _random = new Random();

        public event EventHandler<GameStateUpdatedEventArgs> OnGameStateUpdated;

        public LocalAIGameEngine(Player humanPlayer)
        {
             // Usa tu método para crear el tablero inicial
            _gameState = new GameState(Player.White, Board.Board.Initialize());
            _humanPlayer = humanPlayer;
        }

        public GameState CurrentGameState => _gameState;

        public MoveResult PlayerMove(Move move)
        {
            if (_gameState.CurrentPlayer != _humanPlayer)
                return MoveResult.FromValidation(MoveValidationResult.Invalid("No es tu turno"));

            var result = _gameState.ExecuteMove(move);
            OnGameStateUpdated?.Invoke(this, new GameStateUpdatedEventArgs(_gameState));

            if (result.IsValid && !_gameState.IsGameOver() && _gameState.CurrentPlayer != _humanPlayer)
            {
                DoAIMove();
            }

            return result;
        }

        private void DoAIMove()
        {
            var moves = _gameState.GetLegalMovesForPlayer(_gameState.CurrentPlayer).ToList();
            if (!moves.Any()) return;

            var move = moves[_random.Next(moves.Count)];
            var result = _gameState.ExecuteMove(move);

            OnGameStateUpdated?.Invoke(this, new GameStateUpdatedEventArgs(_gameState));
        }
    }
}
