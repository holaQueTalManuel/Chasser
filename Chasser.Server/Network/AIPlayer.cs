using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Logic.Moves;

namespace Chasser.Server.Network
{
    public class AIPlayer
    {
        public Player Color { get; }
        private readonly Position _center = new Position(3, 3);
        private Position _lastEnemyLeechPos;

        public AIPlayer(Player color)
        {
            Color = color;
        }

        public Move GenerateMove(GameState gameState)
        {
            var validMoves = GetAllValidMoves(gameState, Color).ToList();

            var enemyPieces = FindAllEnemyPieces(gameState, Color.Opponent());
            if (enemyPieces.Count == 1 && gameState.Board[enemyPieces[0]] is Sanguijuela)
            {
                _lastEnemyLeechPos = enemyPieces[0];
                var focusedMove = GetFocusedLeechCaptureMove(gameState, validMoves, _lastEnemyLeechPos);
                if (focusedMove != null) return focusedMove;
            }
            else if (_lastEnemyLeechPos != null && gameState.Board[_lastEnemyLeechPos] is Sanguijuela)
            {
                var persistentMove = GetFocusedLeechCaptureMove(gameState, validMoves, _lastEnemyLeechPos);
                if (persistentMove != null) return persistentMove;
            }

            var adjacentCapture = GetAdjacentCaptureMove(gameState, validMoves);
            if (adjacentCapture != null) return adjacentCapture;

            var captureMoves = GetCaptureMoves(gameState, validMoves);
            if (captureMoves.Count > 0)
            {
                var bestCapture = SelectBestCaptureMove(gameState, captureMoves);
                return bestCapture;
            }

            return GetStrategicMove(gameState, validMoves);
        }

        private Move GetFocusedLeechCaptureMove(GameState gameState, List<Move> moves, Position enemyLeechPos)
        {
            var directCapture = moves.FirstOrDefault(m =>
                m.ToPos.Equals(enemyLeechPos) &&
                gameState.Board[m.FromPos] is Sanguijuela);

            if (directCapture != null) return directCapture;

            var myLeeches = FindMyPieces(gameState, PieceType.Sanguijuela);
            if (!myLeeches.Any()) return null;

            var closestLeech = myLeeches
                .OrderBy(l => DistanceBetween(l, enemyLeechPos))
                .First();

            var leechMoves = moves.Where(m =>
                m.FromPos.Equals(closestLeech) &&
                gameState.Board[m.FromPos] is Sanguijuela).ToList();

            return leechMoves
                .OrderBy(m => DistanceBetween(m.ToPos, enemyLeechPos))
                .FirstOrDefault();
        }

        private Move GetAdjacentCaptureMove(GameState gameState, List<Move> moves)
        {
            return moves
                .Where(move =>
                {
                    var from = move.FromPos;
                    var to = move.ToPos;
                    var piece = gameState.Board[from];
                    var target = gameState.Board[to];

                    return target != null &&
                           target.Color != piece.Color &&
                           IsAdjacent(from, to) &&
                           IsCaptureAllowed(piece, target);
                })
                .Select(move => new { Move = move, Value = GetPieceValue(gameState.Board[move.ToPos]) })
                .OrderByDescending(x => x.Value)
                .FirstOrDefault()?.Move;
        }

        private int GetPieceValue(Piece piece)
        {
            return piece.Type switch
            {
                PieceType.Obliterador => 3,
                PieceType.Sanguijuela => 2,
                PieceType.Tonel => 1,
                _ => 0
            };
        }

        private bool IsAdjacent(Position a, Position b)
        {
            int dr = Math.Abs(a.Row - b.Row);
            int dc = Math.Abs(a.Column - b.Column);
            return (dr + dc == 1);
        }

        private bool IsCaptureAllowed(Piece attacker, Piece target)
        {
            return attacker.Type switch
            {
                PieceType.Sanguijuela => target.Type == PieceType.Tonel || target.Type == PieceType.Sanguijuela,
                PieceType.Obliterador => true,
                _ => false
            };
        }

        private Move GetStrategicMove(GameState gameState, List<Move> moves)
        {
            return moves
                .Select(move =>
                {
                    var piece = gameState.Board[move.FromPos];
                    int score = 0;

                    if (piece.Type == PieceType.Obliterador)
                    {
                        var enemies = FindAllEnemyPieces(gameState, Color.Opponent());
                        score = 100 - DistanceToNearestTarget(move.ToPos, enemies);
                    }
                    else if (piece.Type == PieceType.Sanguijuela)
                    {
                        var toneles = FindEnemyPieces(gameState, PieceType.Tonel, Color.Opponent());
                        score = 80 - DistanceToNearestTarget(move.ToPos, toneles);
                    }
                    else if (piece.Type == PieceType.Tonel)
                    {
                        var enemies = FindAllEnemyPieces(gameState, Color.Opponent());
                        int distToEnemy = DistanceToNearestTarget(move.ToPos, enemies);
                        score = 50 - DistanceToCenter(move.ToPos) + distToEnemy;
                    }

                    return new { move, score };
                })
                .OrderByDescending(x => x.score)
                .First().move;
        }

        private List<Move> GetAllValidMoves(GameState gameState, Player player)
        {
            return gameState.GetLegalMovesForPlayer(player).ToList();
        }

        private List<Move> GetCaptureMoves(GameState gameState, List<Move> moves)
        {
            return moves.Where(move =>
            {
                var target = gameState.Board[move.ToPos];
                if (target == null) return false;

                var piece = gameState.Board[move.FromPos];
                if (target.Color == piece.Color) return false;

                return IsCaptureAllowed(piece, target);
            }).ToList();
        }

        private Move SelectBestCaptureMove(GameState gameState, List<Move> captureMoves)
        {
            return captureMoves.OrderByDescending(move =>
            {
                var target = gameState.Board[move.ToPos];
                return GetPieceValue(target);
            }).First();
        }

        private List<Position> FindAllEnemyPieces(GameState gameState, Player enemyColor)
        {
            return gameState.Board.AllPieces()
                .Where(p => p.piece.Color == enemyColor)
                .Select(p => p.pos)
                .ToList();
        }

        private List<Position> FindEnemyPieces(GameState gameState, PieceType type, Player color)
        {
            return gameState.Board.AllPieces()
                .Where(p => p.piece.Color == color && p.piece.Type == type)
                .Select(p => p.pos)
                .ToList();
        }

        private List<Position> FindMyPieces(GameState gameState, PieceType type)
        {
            return gameState.Board.AllPieces()
                .Where(p => p.piece.Color == Color && p.piece.Type == type)
                .Select(p => p.pos)
                .ToList();
        }

        private int DistanceToCenter(Position pos)
        {
            return Math.Abs(pos.Row - _center.Row) + Math.Abs(pos.Column - _center.Column);
        }

        private int DistanceBetween(Position a, Position b)
        {
            return Math.Abs(a.Row - b.Row) + Math.Abs(a.Column - b.Column);
        }

        private int DistanceToNearestTarget(Position pos, List<Position> targets)
        {
            return targets.Any() ? targets.Min(t => DistanceBetween(pos, t)) : int.MaxValue;
        }
    }
}
