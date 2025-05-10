using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Moves;
using Chasser.Common.Network;
using Chasser.Logic.Network;
using Chasser.Utilities;
using Chasser.View;

namespace Chasser
{
    public partial class Game : Page
    {
        private readonly string gameCode;
        private readonly GameState gameState;
        private readonly Image[,] pieceImages = new Image[7, 7];
        private readonly Rectangle[,] highlights = new Rectangle[7, 7];
        private readonly Dictionary<Position, Move> moveCache = new Dictionary<Position, Move>();

        private Position selectedPos = null;
        private string playerColor;
        private bool isMyTurn;

        public Game(string code, string token, string assignedColor)
        {
            InitializeComponent();
            gameCode = code;
            playerColor = assignedColor.ToLower().Trim();
            isMyTurn = playerColor == "white";

            Debug.WriteLine($"Iniciando juego - Código: {gameCode}, Color: {playerColor}, Mi turno: {isMyTurn}");

            InitializeBoard();
            gameState = new GameState(Player.White, Board.Initialize());
            UpdateDisplay();

            _ = ListenForServerMessagesAsync();
        }

        private async Task ListenForServerMessagesAsync()
        {
            try
            {
                while (TCPClient.IsConnected)
                {
                    var response = await TCPClient.ReceiveMessageAsync();
                    Application.Current.Dispatcher.Invoke(() => ProcessServerResponse(response));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en ListenForServerMessagesAsync: {ex}");
                ShowDisconnectMessage();
            }
        }

        private void ProcessServerResponse(ResponseMessage response)
        {
            Debug.WriteLine($"Procesando respuesta: {response.Status}");

            try
            {
                switch (response.Status)
                {
                    case "OPPONENT_MOVE":
                        ProcessOpponentMove(response.Data);
                        break;
                    case "MOVE_ACCEPTED":
                        ProcessMoveAccepted();
                        break;
                    case "MOVE_INVALID":
                        ProcessInvalidMove(response.Message);
                        break;
                    case "GAME_OVER":
                        ProcessGameOver(response);
                        break;
                    case "OPPONENT_DISCONNECTED":
                        ProcessOpponentDisconnected();
                        break;
                    default:
                        Debug.WriteLine($"Respuesta no manejada: {response.Status}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error procesando respuesta: {ex}");
            }
        }

        private void ProcessOpponentMove(Dictionary<string, string> moveData)
        {
            var move = new NormalMove(
                new Position(int.Parse(moveData["fromRow"]), int.Parse(moveData["fromCol"])),
                new Position(int.Parse(moveData["toRow"]), int.Parse(moveData["toCol"]))
            );

            gameState.ExecuteMove(move);
            isMyTurn = true;
            UpdateDisplay();
        }

        private void ProcessMoveAccepted()
        {
            // Actualizaciones adicionales si son necesarias
            UpdateDisplay();
        }

        private void ProcessInvalidMove(string message)
        {
            MessageBox.Show(message, "Movimiento inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            isMyTurn = true;
            UpdateDisplay();
        }

        private void ProcessGameOver(ResponseMessage response)
        {
            string message = response.Message;
            if (response.Data.TryGetValue("winner", out var winner) && winner == playerColor)
            {
                message = "¡Has ganado la partida!";
            }

            MessageBox.Show(message, "Juego terminado", MessageBoxButton.OK, MessageBoxImage.Information);
            NavigationService.GoBack();
        }

        private void ProcessOpponentDisconnected()
        {
            MessageBox.Show("El oponente se ha desconectado", "Fin de la partida", MessageBoxButton.OK, MessageBoxImage.Information);
            NavigationService.GoBack();
        }

        private void ShowDisconnectMessage()
        {
            MessageBox.Show("Se perdió la conexión con el servidor", "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            NavigationService.GoBack();
        }

        private void InitializeBoard()
        {
            double cellSize = 400 / 7.0;

            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    // Configurar imágenes de piezas
                    pieceImages[r, c] = new Image
                    {
                        MaxWidth = cellSize,
                        MaxHeight = cellSize,
                        Stretch = Stretch.Uniform
                    };
                    pieceGrid.Children.Add(pieceImages[r, c]);

                    // Configurar highlights
                    highlights[r, c] = new Rectangle();
                    highlightGrid.Children.Add(highlights[r, c]);
                }
            }
        }

        private void UpdateDisplay()
        {
            DrawBoard();
            UpdateTurnIndicator();
        }

        private void DrawBoard()
        {
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    pieceImages[r, c].Source = Images.GetImage(gameState.Board[r, c]);
                }
            }
        }

        private void UpdateTurnIndicator()
        {
            turnBlock.Text = isMyTurn ? "Tu turno" : "Turno del oponente";
            turnBlock.Foreground = isMyTurn ? Brushes.Green : Brushes.Red;
        }

        private void boardGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMyTurn) return;

            Point point = e.GetPosition(pieceGrid);
            Position pos = ToSquarePosition(point);

            Debug.WriteLine($"Clic en posición: {pos.Row},{pos.Column}");
            Debug.WriteLine($"Pieza en posición: {gameState.Board[pos]?.ToString() ?? "vacía"}");

            if (selectedPos == null)
            {
                SelectPiece(pos);
            }
            else
            {
                TryMakeMove(pos);
            }
        }

        private void SelectPiece(Position pos)
        {
            var piece = gameState.Board[pos];
            if (piece == null || piece.Color.ToString().ToLower() != playerColor) return;

            selectedPos = pos;
            CacheMoves(gameState.GetLegalMoves(pos));
            ShowHighlights();
        }

        private async void TryMakeMove(Position pos)
        {
            if (!moveCache.TryGetValue(pos, out Move move))
            {
                Debug.WriteLine("Movimiento no válido");
                return;
            }

            Debug.WriteLine($"Intentando mover de {selectedPos} a {pos}");

            try
            {
                isMyTurn = false;
                UpdateDisplay();

                var request = new RequestMessage
                {
                    Command = "GAME_ACTION_MOVE",
                    Data = new Dictionary<string, string>
                {
                    { "fromRow", move.FromPos.Row.ToString() },
                    { "fromCol", move.FromPos.Column.ToString() },
                    { "toRow", move.ToPos.Row.ToString() },
                    { "toCol", move.ToPos.Column.ToString() }
                }
                };

                await TCPClient.SendMessageAsync(request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al enviar movimiento: {ex}");
                isMyTurn = true;
                UpdateDisplay();
                MessageBox.Show("Error al enviar movimiento", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                selectedPos = null;
                HideHighlights();
            }
        }

        private async void HandleMove(Move move)
        {
            Debug.WriteLine($"Enviando movimiento: {move.FromPos} a {move.ToPos}");

            var request = new RequestMessage
            {
                Command = "GAME_ACTION_MOVE",
                Data = new Dictionary<string, string>
                {
                    { "fromRow", move.FromPos.Row.ToString() },
                    { "fromCol", move.FromPos.Column.ToString() },
                    { "toRow", move.ToPos.Row.ToString() },
                    { "toCol", move.ToPos.Column.ToString() }
                }
            };

            try
            {
                isMyTurn = false;
                UpdateTurnIndicator();

                await TCPClient.SendMessageAsync(request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al enviar movimiento: {ex}");
                isMyTurn = true;
                UpdateTurnIndicator();
                MessageBox.Show("Error al enviar movimiento", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Position ToSquarePosition(Point point)
        {
            double squareSize = pieceGrid.ActualWidth / 7;
            return new Position((int)(point.Y / squareSize), (int)(point.X / squareSize));
        }

        private void CacheMoves(IEnumerable<Move> moves)
        {
            moveCache.Clear();
            foreach (var move in moves)
            {
                moveCache[move.ToPos] = move;
            }
        }

        private void ShowHighlights()
        {
            var highlightColor = new SolidColorBrush(Color.FromArgb(150, 125, 255, 125));
            foreach (var pos in moveCache.Keys)
            {
                highlights[pos.Row, pos.Column].Fill = highlightColor;
            }
        }

        private void HideHighlights()
        {
            foreach (var pos in moveCache.Keys)
            {
                highlights[pos.Row, pos.Column].Fill = Brushes.Transparent;
            }
        }

        private void Rules_Click(object sender, RoutedEventArgs e)
        {
            new RulesWindow { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Login());
        }
    }
}