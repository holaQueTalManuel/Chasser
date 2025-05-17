using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;
using Chasser.View;

namespace Chasser
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            this.Loaded += Login_Loaded;
        }
        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.AjustarTamaño(900, 600);
        }

        private async void Start_Game_IA_Click(object sender, RoutedEventArgs e)
        {
            var token = AuthHelper.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("No se encontró el token de autenticación", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var request = new RequestMessage
            {
                Command = "START_GAME_IA",
                Data = new Dictionary<string, string> { { "token", token } }
            };

            try
            {
                await TCPClient.SendOnlyMessageAsync(request);
                var response = await TCPClient.ReceiveMessageAsync(); // ⬅️ IMPORTANTE

                if (response.Status == "START_GAME_SUCCESS")
                {
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
                            NavigationService.Navigate(new Game(codigo, AuthHelper.GetToken(), color,user, partidasGanadas, racha));
                        });
                    }
                }
                else
                {
                    MessageBox.Show("No se pudo iniciar la partida con la IA", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de conexión: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error en Start_Game_IA_Click: {ex}");
            }
        }


        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var token = AuthHelper.GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    var request = new RequestMessage
                    {
                        Command = "LOGOUT",
                        Data = new Dictionary<string, string> { { "token", token } }
                    };

                    // No esperamos respuesta para no bloquear la salida
                    //_ = TCPClient.SendMessageAsync(request);
                }

                // Limpiar credenciales
                //AuthHelper.ClearToken();

                // Navegar al login
                NavigationService.Navigate(new Login());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en Exit_Click: {ex}");
                // Aun así navegar al login aunque falle el logout
                NavigationService.Navigate(new Login());
            }
        }
    }
}