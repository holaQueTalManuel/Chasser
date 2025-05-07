using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Chasser.Common.Network;
using Chasser.Logic.Board;
using Chasser.Moves;
using Chasser.Utilities;
using Chasser.View;
using Chasser.Common.Model;
using Chasser.Common.Data;
using Chasser.Logic.Network;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para Game.xaml
    /// </summary>
    public partial class Game : Page
    {
        private string gameCod;
        private GameState gameState;
        private readonly Image[,] pieceImages = new Image[7, 7];
        private readonly Rectangle[,] highlights = new Rectangle[7, 7];
        private readonly Dictionary<Position, Move> moveCache = new Dictionary<Position, Move>();
        private Position selectedPos = null;
        private string playerColor;
        private bool isMyTurn = false;

        public Game(string cod, string token, string assignedColor)
        {
            InitializeComponent();
            this.gameCod = cod;
            gameCodeBlock.Text = $"Código de partida: {gameCod}";

            playerColor = assignedColor; // Nuevo parámetro
            isMyTurn = playerColor == "white";

            InitializeBoard();
            gameState = new GameState(Player.White, Board.Initialize());
            DrawBoard(gameState.Board);
            UpdateTurnIndicator();

            _ = ListenForServerMessagesAsync();
        }

        private async Task InitializeGameAsync(string token)
        {
            try
            {
                var joinRequest = new RequestMessage
                {
                    Command = "JOIN_GAME",
                    Data = new Dictionary<string, string>
                    {
                        { "token", token },
                        { "codigo", gameCod }
                    }
                };

                await TCPClient.SendMessageAsync(joinRequest);
                var joinResponse = await TCPClient.ReceiveMessageAsync();

                if (joinResponse.Status == "JOIN_GAME_SUCCESS")
                {
                    playerColor = joinResponse.Data["color"];
                    isMyTurn = playerColor == "white";
                    UpdateTurnIndicator();
                    _ = ListenForServerMessagesAsync();
                }
                else
                {
                    MessageBox.Show(joinResponse.Message);
                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al unirse a la partida: {ex.Message}");
                NavigationService.GoBack();
            }
        }

        private async Task ListenForServerMessagesAsync()
        {
            try
            {
                while (TCPClient.IsConnected)
                {
                    var response = await TCPClient.ReceiveMessageAsync();
                    Application.Current.Dispatcher.Invoke(() => ProcessServerMessage(response));
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Se perdió la conexión: {ex.Message}");
                    NavigationService.GoBack();
                });
            }
        }

        private void ProcessServerMessage(ResponseMessage response)
        {
            switch (response.Status)
            {
                case "OPPONENT_MOVE":
                    var move = new NormalMove(
                        new Position(int.Parse(response.Data["fromRow"]),
                        int.Parse(response.Data["fromCol"])),
                        new Position(int.Parse(response.Data["toRow"]),
                        int.Parse(response.Data["toCol"]))
                    );
                    gameState.ExecuteMove(move);
                    DrawBoard(gameState.Board);
                    isMyTurn = true;
                    UpdateTurnIndicator();
                    break;

                case "GAME_OVER":
                    MessageBox.Show($"Juego terminado! {response.Message}");
                    if (response.Data["winner"] == playerColor)
                    {
                        // Lógica adicional para victoria
                    }
                    NavigationService.GoBack();
                    break;

                case "OPPONENT_DISCONNECTED":
                    MessageBox.Show("El oponente se ha desconectado");
                    NavigationService.GoBack();
                    break;
            }
        }

        private void InitializeBoard()
        {
            for (int r = 0; r <7; r++)
            {
                for (global::System.Int32  c= 0;  c< 7; c++)
                {
                    Image image = new Image();
                    pieceImages[r, c] = image;
                    image.MaxWidth = 400 / 7; // Ancho de celda
                    image.MaxHeight = 400 / 7; // Alto de celda
                    image.Stretch = Stretch.Uniform;
                    pieceGrid.Children.Add(image);

                    Rectangle highlight = new Rectangle();
                    highlights[r, c] = highlight;
                    highlightGrid.Children.Add(highlight);
                }
            }
        }

        private void DrawBoard(Board board)
        {
            for (int r = 0; r < 7; r++)
            {
                for (global::System.Int32 c = 0; c < 7; c++)
                {
                    Piece piece = board[r, c];
                    pieceImages[r, c].Source = Images.GetImage(piece);
                }
            }
        }

        private void UpdateTurnIndicator()
        {
            turnBlock.Text = isMyTurn ? "Tu turno" : "Turno del oponente";
            turnBlock.Foreground = isMyTurn ? Brushes.Green : Brushes.Red;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //infoUser.Visibility = Visibility.Visible;
            
        }

        private void Rules_Click(object sender, RoutedEventArgs e)
        {
            //EJEMPLITO PARA TI MANUEL DEL FUTURO (ULTIMO FINDE DE ABRIL CUANDO TE PONGAS CON EL TCP), EL CONTENIDO DE ESTE BOTON LO TENDRA QUE HACER EL SERVER Y LO QUE SE MANDARA
            //A LO MEJOR UN JSON QUE PONGA REGLAS VER, YO QUE SE, PERO SERA ALGO ASI, IMAGINO.
            RulesWindow rulesWindow = new RulesWindow
            {
                Owner = Window.GetWindow(this)
            };
            rulesWindow.ShowDialog(); 
        }

        //private void LeerUsuarios()
        //{
        //    string savedUser = File.ReadAllText(userPath);

        //    // Realiza una consulta personalizada usando LINQ
        //    var user = _context.Usuarios.FirstOrDefault(u => u.Nombre == savedUser);

        //    if (user != null)
        //    {
        //        nameBlock.Text = user.Nombre;
        //        gamesBlock.Text = user.Partidas_Ganadas?.ToString() ?? "0";
        //    }
        //    else
        //    {
        //        nameBlock.Text = "No se encontró usuario";
        //        gamesBlock.Text = "0";
        //    }
        //}

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists("user.txt"))
            {
                File.Delete("user.txt");
            }
            NavigationService.Navigate(new Login());
        }
        private void Cell_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            // Aquí puedes manejar la lógica para los clics en las casillas
            if (button.Background == Brushes.Green)
            {
                // Lógica cuando se hace clic en la casilla verde
            }
            else
            {
                // Lógica para las demás casillas
            }
        }

        private void highlightGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void onToPositionSelected(Position pos)
        {
            selectedPos = null;
            HideHighlights();

            if (moveCache.TryGetValue(pos, out Move move))
            {
                HandleMove(move);
            }
        }

        private async void HandleMove(Move move)
        {
            var request = new RequestMessage
            {
                Command = "GAME_ACTION",
                Data = new Dictionary<string, string>
                {
                    { "type", "MOVE" },
                    { "fromRow", move.FromPos.Row.ToString() },
                    { "fromCol", move.FromPos.Column.ToString() },
                    { "toRow", move.ToPos.Row.ToString() },
                    { "toCol", move.ToPos.Column.ToString() }
                }
            };

            try
            {
                await TCPClient.SendMessageAsync(request);
                isMyTurn = false;
                UpdateTurnIndicator();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al enviar movimiento: {ex.Message}");
            }
        }

        //private async Task ListenForOpponentAsync()
        //{
        //    while (true)
        //    {
        //        string line = await reader.ReadLineAsync();
        //        if (line == null) break;

        //        var message = JsonSerializer.Deserialize<RequestMessage>(line);
        //        if (message.Command == "GAME_ACTION" && message.Data["type"] == "MOVE")
        //        {
        //            var move = new NormalMove(
        //                new Position(int.Parse(message.Data["fromRow"]), int.Parse(message.Data["fromCol"])),
        //                new Position(int.Parse(message.Data["toRow"]), int.Parse(message.Data["toCol"]))
        //            );

        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                gameState.ExecuteMove(move);
        //                DrawBoard(gameState.Board);
        //            });
        //        }
        //        else if (message.Command == "GAME_OVER")
        //        {
        //            MessageBox.Show("¡Fin del juego!");
        //            Application.Current.Shutdown();
        //        }
        //    }
        //}


        private void onFromPositionSelected(Position pos)
        {
            IEnumerable<Move> moves = gameState.GetLegalMoves(pos);

            if (moves.Any())
            {
                selectedPos = pos;
                CacheMoves(moves);
                ShowHighlights();
            }
        }

        private Position ToSquarePosition(Point point)
        {
            double squareSize = pieceGrid.ActualWidth / 7;
            int row = (int)(point.Y / squareSize);
            int col = (int)(point.X / squareSize);

            return new Position(row, col);
        }

        private void CacheMoves(IEnumerable<Move> moves)
        {
            moveCache.Clear();

            foreach (Move move in moves)
            {
                moveCache[move.ToPos] = move;
            }
        }

        private void ShowHighlights()
        {
            var color = System.Windows.Media.Color.FromArgb(150, 125, 255, 125);
            foreach (Position to in moveCache.Keys)
            {
                highlights[to.Row, to.Column].Fill = new SolidColorBrush(color);
            }
        }
        private void HideHighlights()
        {
            //var color = System.Windows.Media.Color.FromArgb(150, 125, 255, 125);
            foreach (Position to in moveCache.Keys)
            {
                highlights[to.Row, to.Column].Fill = Brushes.Transparent;
            }
        }

        private void boardGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMyTurn) return;

            Point point = e.GetPosition(pieceGrid);
            Position pos = ToSquarePosition(point);

            if (selectedPos == null)
            {
                if (gameState.Board[pos]?.Color.ToString().ToLower() == playerColor)
                {
                    onFromPositionSelected(pos);
                }
            }
            else
            {
                onToPositionSelected(pos);
            }
        }

    }
}
