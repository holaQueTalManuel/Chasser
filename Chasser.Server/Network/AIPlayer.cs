using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Logic.Moves;

namespace Chasser.Server.Network
{
    public class AIPlayer
    {
        public Player Color { get; }
        private readonly Position _center = new Position(3, 3);

        public AIPlayer(Player color)
        {
            Color = color;
        }

        // Método principal que genera el movimiento de la IA
        public Move GenerateMove(GameState gameState)
        {
            var validMoves = GetAllValidMoves(gameState, Color).ToList();

            // 1. Priorizar movimientos que capturan piezas
            var captureMoves = GetCaptureMoves(gameState, validMoves);
            if (captureMoves.Count > 0)
            {
                return SelectBestCaptureMove(gameState, captureMoves);
            }

            // 2. Movimientos estratégicos según tipo de pieza
            var strategicMoves = GetStrategicMoves(gameState, validMoves);
            if (strategicMoves.Count > 0)
            {
                return SelectBestStrategicMove(gameState, strategicMoves);
            }

            // 3. Si no hay estrategia específica, mover hacia el centro
            return GetCenterMove(gameState, validMoves);
        }

        #region Métodos principales de generación de movimientos

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

                // Reglas específicas por tipo de pieza
                return piece.Type switch
                {
                    PieceType.Sanguijuela => target.Type == PieceType.Tonel,
                    PieceType.Obliterador => true, // Puede capturar cualquier pieza
                    PieceType.Tonel => false,      // Los toneles no pueden capturar
                    _ => false
                };
            }).ToList();
        }

        private Move SelectBestCaptureMove(GameState gameState, List<Move> captureMoves)
        {
            // Priorizar capturas de piezas más valiosas
            return captureMoves.OrderByDescending(move =>
            {
                var target = gameState.Board[move.ToPos];
                return target.Type switch
                {
                    PieceType.Obliterador => 3,
                    PieceType.Sanguijuela => 2,
                    PieceType.Tonel => 1,
                    _ => 0
                };
            }).First();
        }

        private List<Move> GetStrategicMoves(GameState gameState, List<Move> moves)
        {
            var strategicMoves = new List<Move>();

            foreach (var move in moves)
            {
                var piece = gameState.Board[move.FromPos];

                switch (piece.Type)
                {
                    case PieceType.Tonel:
                        // Toneles se mueven hacia el centro
                        if (IsMovingTowardCenter(move.FromPos, move.ToPos))
                            strategicMoves.Add(move);
                        break;

                    case PieceType.Sanguijuela:
                        // Sanguijuelas se mueven hacia toneles enemigos
                        if (IsMovingTowardTarget(move, gameState, PieceType.Tonel))
                            strategicMoves.Add(move);
                        break;

                    case PieceType.Obliterador:
                        // Obliteradores se mueven hacia cualquier pieza enemiga
                        if (IsMovingTowardAnyEnemy(move, gameState))
                            strategicMoves.Add(move);
                        break;
                }
            }

            return strategicMoves;
        }

        private Move SelectBestStrategicMove(GameState gameState, List<Move> strategicMoves)
        {
            return strategicMoves.OrderBy(move =>
            {
                var piece = gameState.Board[move.FromPos];

                return piece.Type switch
                {
                    PieceType.Tonel => DistanceToCenter(move.ToPos),
                    PieceType.Sanguijuela => DistanceToNearestTarget(move.ToPos,
                                      FindEnemyPieces(gameState, PieceType.Tonel, Color.Opponent())),
                    PieceType.Obliterador => DistanceToNearestTarget(move.ToPos,
                                      FindAllEnemyPieces(gameState, Color.Opponent())),
                    _ => 0
                };
            }).First();
        }

        private Move GetCenterMove(GameState gameState, List<Move> moves)
        {
            return moves.OrderBy(move => DistanceToCenter(move.ToPos)).First();
        }

        #endregion

        #region Métodos auxiliares de lógica

        private bool IsMovingTowardCenter(Position from, Position to)
        {
            return DistanceToCenter(to) < DistanceToCenter(from);
        }

        private bool IsMovingTowardTarget(Move move, GameState gameState, PieceType targetType)
        {
            var targets = FindEnemyPieces(gameState, targetType, Color.Opponent());
            if (!targets.Any()) return false;

            var nearestTarget = targets.OrderBy(t => DistanceBetween(move.FromPos, t)).First();
            return DistanceBetween(move.ToPos, nearestTarget) < DistanceBetween(move.FromPos, nearestTarget);
        }

        private bool IsMovingTowardAnyEnemy(Move move, GameState gameState)
        {
            var enemies = FindAllEnemyPieces(gameState, Color.Opponent());
            if (!enemies.Any()) return false;

            var nearestEnemy = enemies.OrderBy(e => DistanceBetween(move.FromPos, e)).First();
            return DistanceBetween(move.ToPos, nearestEnemy) < DistanceBetween(move.FromPos, nearestEnemy);
        }

        #endregion

        #region Métodos auxiliares de cálculo de distancias y búsqueda

        private int DistanceToCenter(Position pos)
        {
            return Math.Abs(pos.Row - _center.Row) + Math.Abs(pos.Column - _center.Column);
        }

        private int DistanceToNearestTarget(Position pos, List<Position> targets)
        {
            return targets.Any() ? targets.Min(t => DistanceBetween(pos, t)) : int.MaxValue;
        }

        private int DistanceBetween(Position a, Position b)
        {
            return Math.Abs(a.Row - b.Row) + Math.Abs(a.Column - b.Column);
        }

        private List<Position> FindEnemyPieces(GameState gameState, PieceType pieceType, Player enemyColor)
        {
            var pieces = new List<Position>();

            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var pos = new Position(row, col);
                    var piece = gameState.Board[pos];
                    if (piece?.Color == enemyColor && piece.Type == pieceType)
                    {
                        pieces.Add(pos);
                    }
                }
            }

            return pieces;
        }

        private List<Position> FindAllEnemyPieces(GameState gameState, Player enemyColor)
        {
            var pieces = new List<Position>();

            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var pos = new Position(row, col);
                    var piece = gameState.Board[pos];
                    if (piece?.Color == enemyColor)
                    {
                        pieces.Add(pos);
                    }
                }
            }

            return pieces;
        }

        #endregion
    }
}
