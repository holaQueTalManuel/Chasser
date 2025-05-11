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
                // Cambiar el tipo de variable a ResponseMessage explícitamente
                ResponseMessage response =  await TCPClient.SendMessageAsync(request);
                

                if (response == null)
                {
                    MessageBox.Show("No se recibió respuesta del servidor", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (response.Status == "START_GAME_SUCCESS" &&
                    response.Data != null &&
                    response.Data.TryGetValue("codigo", out string codigo) &&
                    response.Data.TryGetValue("color", out string color))
                {
                    Debug.WriteLine($"Partida creada - Código: {codigo}, Color: {color}");
                    NavigationService.Navigate(new Game(codigo, token, color));
                }
                else if (response.Status.StartsWith("START_GAME_FAIL"))
                {
                    MessageBox.Show($"Error al crear partida: {response.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Respuesta inesperada del servidor", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    _ = TCPClient.SendMessageAsync(request);
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