using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Azure;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Logic.Moves;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;
using Chasser.Utilities;
using Chasser.View;

namespace Chasser
{
    public partial class Game : Page
    {
        private readonly string gameCode;
        private GameState gameState;
        private readonly Image[,] pieceImages = new Image[7, 7];
        private readonly Rectangle[,] highlights = new Rectangle[7, 7];
        private readonly Dictionary<Position, Move> moveCache = new Dictionary<Position, Move>();
        private List<Position> possibleMoves = new();
        private CancellationTokenSource _cts = new();

        private Position selectedPos = null;
        private string playerColor;
        private bool isMyTurn;
        private DateTime _lastClickTime;

        public Game(string code, string token, string assignedColor, string userName, string partidasGanadas, string racha)
        {
            InitializeComponent();

            this.Loaded += Login_Loaded;

            gameCode = code;
            playerColor = assignedColor.ToLower().Trim();
            isMyTurn = playerColor == "white";

            nameBlock.Text = userName;
            gamesBlock.Text = $"Partidas ganadas: {partidasGanadas}";
            streakBlock.Text = $"Racha de victorias: {racha}";
            gameCodeBlock.Text = $"Código de la partida: {gameCode}";

            Debug.WriteLine($"Iniciando juego - Código: {gameCode}, Color: {playerColor}, Mi turno: {isMyTurn}");

            InitializeBoard();
            gameState = new GameState(Player.White, Board.Initialize());
            UpdateDisplay();

            _ = ListenForServerMessagesAsync();
        }

        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.ResizeAndCenterWindow(1200, 600);
        }

        private async Task ListenForServerMessagesAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var response = await TCPClient.ReceiveMessageAsync();
                    if (response == null)
                    {
                        Debug.WriteLine("Respuesta nula recibida. Posible desconexión.");
                        ShowDisconnectMessage();
                        break;
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ProcessServerResponse(response);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en ListenForServerMessagesAsync: {ex}");
                //ShowDisconnectMessage();
            }
        }
        


        private async void ProcessServerResponse(ResponseMessage response)
        {
            if (response == null) return;

            switch (response.Status)
            {
                case "START_GAME_SUCCESS":
                    if (response.Data != null &&
                        response.Data.TryGetValue("codigo", out string codigo) &&
                        response.Data.TryGetValue("color", out string color) &&
                        response.Data.TryGetValue("nombreUsuario", out string user)

                        )
                    {


                        Debug.WriteLine($"Partida creada - Código: {codigo}, Color: {color}");
                        response.Data.TryGetValue("partidasGanadas", out string partidasGanadas);
                        response.Data.TryGetValue("racha", out string racha);

                        // Aquí haces la navegación desde el hilo principal
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Cerrar partida actual
                            this.Dispose(); // solo si has implementado lo anterior

                            // Ir a nueva partida
                            NavigationService.Navigate(new Game(codigo, AuthHelper.GetToken(), color, user, partidasGanadas, racha));
                        });
                    }
                    break;
                case "START_GAME_FAIL":
                case string fail when fail.StartsWith("START_GAME_FAIL"):
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PopUpInfo.ShowMessage($"Error al crear partida {response.Message}", Window.GetWindow(this), MessageType.Error);
                    });
                    break;

                case "MOVE_ACCEPTED":
                    if (TryExtractAndApplyMove(response, out var from, out var to))
                    {
                        UpdateDisplay();
                        _ = ProcessMoveWithConfirmation(from, to);
                    }
                    break;

                case "AI_MOVE":
                    if (TryExtractAndApplyMove(response, out var aiFrom, out var aiTo))
                    {
                        UpdateDisplay();
                        isMyTurn = true;
                        UpdateTurnIndicator();
                    }
                    break;

                case "GAME_OVER":
                    if (TryExtractAndApplyMove(response, out var finalFrom, out var finalTo))
                    {
                        UpdateDisplay();
                        turnBlock.Visibility = Visibility.Collapsed;
                    }

                    ProcessGameOver(response);
                    break;
                case "RESTART_ACCEPTED":
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ResetGame();
                    });
                    break;
                case "EXIT_GAME_SUCCESS":
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ExitGame();
                    });
                    break;

                // Puedes agregar más casos según el protocolo
                default:
                    Debug.WriteLine($"Respuesta desconocida del servidor: {response.Status}");
                    break;
            }
        }
        public void Dispose()
        {
            TCPClient.Disconnect();
        }
       

        private bool TryExtractAndApplyMove(ResponseMessage response, out Position from, out Position to)
        {
            from = to = null;

            if (response?.Data == null)
                return false;

            // 1. Intentar extraer movimiento por "fromPos"/"toPos" (formato MOVE_ACCEPTED)
            if (Position.TryParse(response.Data.GetValueOrDefault("fromPos"), out from) &&
                Position.TryParse(response.Data.GetValueOrDefault("toPos"), out to))
            {
                MovePieceOnClient(from, to);
                return true;
            }

            // 2. Intentar extraer por "fromRow"/"fromCol" (formato AI_MOVE o GAME_OVER con coords sueltas)
            if (int.TryParse(response.Data.GetValueOrDefault("fromRow"), out int fromRow) &&
                int.TryParse(response.Data.GetValueOrDefault("fromCol"), out int fromCol) &&
                int.TryParse(response.Data.GetValueOrDefault("toRow"), out int toRow) &&
                int.TryParse(response.Data.GetValueOrDefault("toCol"), out int toCol))
            {
                from = new Position(fromRow, fromCol);
                to = new Position(toRow, toCol);
                MovePieceOnClient(from, to);
                return true;
            }

            return false;
        }

        

        private void MovePieceOnClient(Position from, Position to)
        {
            var piece = gameState.Board[from];

            if (piece == null)
            {
                Debug.WriteLine("No hay pieza en la posición de origen.");
                return;
            }

            gameState.Board[to] = piece;
            gameState.Board[from] = null;

            DrawBoard(); // Redibuja el tablero en pantalla
            UpdateTurnIndicator();
            SoundManager.PlayMoveSound();
        }


        private async Task ProcessMoveWithConfirmation(Position from, Position to)
        {
            try
            {
                // 1. Enviar confirmación al servidor
                await TCPClient.SendOnlyMessageAsync(
                    new RequestMessage { Command = "MOVE_PROCESSED" });

                Debug.WriteLine("Confirmación MOVE_PROCESSED enviada al servidor");

                // 2. Cambiar estado inmediatamente (no esperar respuesta aquí)
                isMyTurn = false;
                UpdateTurnIndicator();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en confirmación de movimiento: {ex}");
                // Considera reintentar o notificar al usuario
            }
        }


        private void ProcessInvalidMove(string message)
        {
            PopUpInfo.ShowMessage($"Movimiento inválido {message}", Window.GetWindow(this), MessageType.Warning);
            isMyTurn = true;
            UpdateDisplay();
        }

        private void ProcessGameOver(ResponseMessage response)
        {
            string message = response.Message;
            if (response.Data.TryGetValue("winner", out var winnerData))
            {
                var winner = winnerData.ToString().ToLower();
                Debug.WriteLine($"GANADOR: {winner}");

                if (winner == "white")
                {
                    message = "¡Has ganado la partida!";
                }
                else
                {
                    message = "Ha ganado la IA";
                }
            }
            WinnerTextBlock.Text = message;
            btnRestart.Visibility = Visibility.Collapsed;
            GameOverPopup.IsOpen = true;
        }

        private void ShowDisconnectMessage()
        {
            PopUpInfo.ShowMessage($"Se perdió la conexión con el servidor. Error de conexion", Window.GetWindow(this), MessageType.Error);

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

            // Si ya hay una posición seleccionada
            if (selectedPos != null)
            {
                // Si se hace clic en la misma pieza seleccionada, deseleccionar
                if (selectedPos == pos)
                {
                    DeselectPiece();
                    return;
                }

                // Si no, intentar hacer el movimiento
                TryMakeMove(pos);
            }
            else
            {
                SelectPiece(pos);
            }
        }

        private void DeselectPiece()
        {
            selectedPos = null;
            HideHighlights(); // Método para limpiar los resaltados de movimientos posibles
            Debug.WriteLine("Pieza deseleccionada");
        }


        private void SelectPiece(Position pos)
        {
            var piece = gameState.Board[pos];
            if (piece == null || piece.Color.ToString().ToLower() != playerColor) return;

            SoundManager.PlayMoveSound();
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

                await TCPClient.SendOnlyMessageAsync(request);
                Debug.WriteLine($"MENSAJE CON LOS MOVIMIENTOS MANDADOS AL SERVIDOR");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al enviar movimiento: {ex}");
                isMyTurn = true;
                UpdateDisplay();
                PopUpInfo.ShowMessage($"Error al enviar movimiento", Window.GetWindow(this), MessageType.Error);

            }
            finally
            {
                selectedPos = null;
                HideHighlights();
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
            var highlightColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 135, 206, 250));
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
        private async void OnRestartClicked(object sender, RoutedEventArgs e)
        {
            GameOverPopup.IsOpen = false;
            var restartMessage = new RequestMessage
            {
                Command = "RESTART_REQUEST",
                Data = new Dictionary<string, string>
                {
                    { "token", AuthHelper.GetToken() },
                    { "gameCode", gameCode }
                }
            };
            await TCPClient.SendOnlyMessageAsync(restartMessage);
            

        }

        private async void OnRestartNewGameClicked(object sender, RoutedEventArgs e)
        {
            GameOverPopup.IsOpen = false;
            var restartMessage = new RequestMessage
            {
                Command = "START_GAME_IA",
                Data = new Dictionary<string, string>
                {
                    { "token", AuthHelper.GetToken() }
                }
            };
            await TCPClient.SendOnlyMessageAsync(restartMessage);
        }

        private async void OnExitClicked(object sender, RoutedEventArgs e)
        {
            var exitMessage = new RequestMessage
            {
                Command = "EXIT_GAME",
                Data = new Dictionary<string, string>
                {
                    { "token", AuthHelper.GetToken() },
                    { "gameCode", gameCode }
                }
            };
            await TCPClient.SendOnlyMessageAsync(exitMessage);


        }
        private void ResetGame()
        {
            // Limpiar el tablero
            //pieceGrid.Children.Clear();
            //highlightGrid.Children.Clear();

            gameState = new GameState(Player.White, Board.Initialize());
            UpdateDisplay();
        }
        private void ExitGame()
        {
            gameState = null;
            _cts.Cancel();

            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}