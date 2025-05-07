using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Azure.Core;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;
using Chasser.View;
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
            //_authToken = Auth;

            // Guardar el token en una variable auxiliar o en otra propiedad que permita escritura.
            Properties.Settings.Default["SessionToken"] = token; // Usar el índice para asignar valores.
            Properties.Settings.Default.Save();
        }
        private async void StartGame_Click(object sender, RoutedEventArgs e)
        {
            var token = AuthHelper.GetToken(); // Obtener el token primero
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("No se encontró el token de autenticación");
                return;
            }

            var request = new RequestMessage
            {
                Command = "START_GAME",
                Data = new Dictionary<string, string>
        {
            { "token", token }
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

                if (response.Status == "START_GAME_WAITING")
                {
                    if (response.Data != null && response.Data.TryGetValue("codigo", out string codigo))
                    {
                        // Pasar tanto el código como el token al constructor de Game
                        NavigationService.Navigate(new Game(codigo, token, response.Data["color"]));
                    }
                    else
                    {
                        MessageBox.Show("El código de la partida no se encontró en la respuesta.");
                    }
                }
                else if (response.Status.StartsWith("START_GAME_FAIL"))
                {
                    MessageBox.Show($"Error al crear la partida: {response.Message}");
                }
                else if (response.Status == "GAME_STARTED" && response.Data.TryGetValue("codigo", out string codigo))
                {
                    NavigationService.Navigate(new Game(codigo, token, response.Data["color"]));

                }
                else
                {
                    MessageBox.Show("Respuesta inesperada del servidor.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear la partida: {ex.Message}");
            }
        }
        private async void JoinGame_Click(object sender, RoutedEventArgs e)
        {
            EnterGameCod enterGameWindow = new EnterGameCod(_authToken)
            {
                Owner = Window.GetWindow(this)
            };
            enterGameWindow.ShowDialog();
        }

        

        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Notificar al servidor (opcional pero recomendado en aplicaciones multi-usuario)
                var request = new RequestMessage
                {
                    Command = "LOGOUT",  // Mejor que "EXIT_APP" para claridad
                    Data = new Dictionary<string, string> { { "token", _authToken } }
                };

                var response = await TCPClient.SendMessageAsync(request); // No esperamos respuesta crítica
                if (response.Status == "LOGOUT_SUCCESS")
                {
                    _authToken = null; // Limpiar el token
                    Properties.Settings.Default["SessionToken"] = null; // Limpiar el token en la configuración
                    Properties.Settings.Default.Save(); // Guardar cambios
                    NavigationService.Navigate(new Login());
                }

            }
            catch (Exception ex)
            {
                // Log opcional (no mostrar al usuario si es un cierre)
                Console.WriteLine($"Error al notificar cierre al servidor: {ex.Message}");
            }
            finally
            {
                
            }
        }
    }
}
    


