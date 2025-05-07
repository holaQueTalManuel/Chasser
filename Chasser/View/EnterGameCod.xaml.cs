using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Chasser.Logic;
using Chasser.Logic.Board;
using Chasser.Logic.Network;

namespace Chasser.View
{
    /// <summary>
    /// Lógica de interacción para EnterGameCod.xaml
    /// </summary>
    public partial class EnterGameCod : Window
    {
        private readonly string _token;
        public EnterGameCod(string token)
        {
            InitializeComponent();
            _token = token;
        }
        private async void JoinGame_Click(object sender, RoutedEventArgs e)
        {
            var token = AuthHelper.GetToken();
            string gameCode = GameCodeBox.Text.Trim();

            if (string.IsNullOrEmpty(gameCode))
            {
                MessageBox.Show("Por favor, ingrese un código de juego válido.",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            var request = new RequestMessage
            {
                Command = "JOIN_GAME",
                Data = new Dictionary<string, string>
            {
                { "token", token },
                { "codigo", gameCode }
            }
            };

            try
            {
                var response = await TCPClient.SendMessageAsync(request);

                if (response == null)
                {
                    MessageBox.Show("La respuesta del servidor fue nula.");
                    return;
                }

                // Manejar diferentes respuestas del servidor
                if (response.Status == "JOIN_GAME_SUCCESS" ||
                    response.Status == "GAME_STARTED")
                {
                    // Obtener el color asignado del mensaje
                    var color = response.Data.ContainsKey("color")
                        ? response.Data["color"]
                        : "black"; // Valor por defecto por si acaso

                    var gamePage = new Game(gameCode, AuthHelper.GetToken(), color);
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.MainFrame.Navigate(gamePage);
                    this.Close();
                }
                else
                {
                    // Mostrar el mensaje de error del servidor
                    MessageBox.Show(response.Message, "Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al unirse al juego: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

    }
}
