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
using Chasser.Logic.Network;
using Microsoft.Extensions.DependencyInjection;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private string _authToken;

        public MainPage()
        {
            InitializeComponent();
        }

        // Cambiar el código para no intentar asignar directamente a la propiedad de solo lectura 'SessionToken'.
        // En su lugar, se puede usar una propiedad auxiliar o un método para almacenar el token en otra parte.

        public void SetAuthToken(string token)
        {
            _authToken = token;

            // Guardar el token en una variable auxiliar o en otra propiedad que permita escritura.
            Properties.Settings.Default["SessionToken"] = token; // Usar el índice para asignar valores.
            Properties.Settings.Default.Save();
        }
        private async void StartGame_Click(object sender, RoutedEventArgs e)
        {
            var request = new RequestMessage
            {
                Command = "START_GAME",
                Data = new Dictionary<string, string>
                {
                    { "token", _authToken } // ¡Envía el token aquí!
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

                if (response.Status == "START_GAME_SUCCESS")
                {
                    NavigationService.Navigate(new Game());
                }
                else if (response.Status.StartsWith("START_GAME_FAIL"))
                {
                    MessageBox.Show($"Error al unirse a la partida: {response.Message}");
                }
                else
                {
                    MessageBox.Show("Respuesta inesperada del servidor.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al unirse a la partida: {ex.Message}");
            }
        }
        private async void JoinGame_Click(object sender, RoutedEventArgs e)
        {
            
        }

    }
}
    


