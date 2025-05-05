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
using Chasser.Logic.Network;

namespace Chasser.View
{
    /// <summary>
    /// Lógica de interacción para EnterGameCod.xaml
    /// </summary>
    public partial class EnterGameCod : Window
    {
        public EnterGameCod()
        {
            InitializeComponent();
        }
        private async void JoinGame_Click(object sender, RoutedEventArgs e)
        {
            string gameCode = GameCodeBox.Text.Trim();
            if (!string.IsNullOrEmpty(gameCode))
            {
                var request = new RequestMessage
                {
                    Command = "JOIN_GAME",
                    Data = new Dictionary<string, string>
                    {
                        { "token", AuthHelper.GetToken() },
                        { "codigo", gameCode }
                    }
                };

                try
                {
                    var response = await TCPClient.SendJsonAsync(request);

                    if (response == null)
                    {
                        MessageBox.Show("La respuesta del servidor fue nula.");
                        return;
                    }

                    if (response.Status == "JOIN_GAME_SUCCESS")
                    {
                        var gamePage = new Game(gameCode);

                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.MainFrame.Navigate(gamePage);

                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Error: {response.Message}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al unirse al juego: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Por favor, ingrese un código de juego válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

    }
}
