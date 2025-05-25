using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Model;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;
using Chasser.View;
using Newtonsoft.Json; // AÑADIDO

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
            (Window.GetWindow(this) as MainWindow)?.ResizeAndCenterWindow(900, 600);
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var token = AuthHelper.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                PopUpInfo.ShowMessage("No se encontró el token de autenticación", Window.GetWindow(this), MessageType.Warning);
                return;
            }

            var request = new RequestMessage
            {
                Command = "DELETE_ACCOUNT",
                Data = new Dictionary<string, string> { { "token", token } }
            };

            await TCPClient.SendOnlyMessageAsync(request);
            var response = await TCPClient.ReceiveMessageAsync();

            try
            {
                if (response.Status == "DELETE_ACCOUNT_SUCCESS")
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        NavigationService.Navigate(new Login());
                    });
                }
                else
                {
                    PopUpInfo.ShowMessage("No se pudo eliminar tu cuenta", Window.GetWindow(this), MessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                PopUpInfo.ShowMessage($"Error de conexión: {ex.Message}", Window.GetWindow(this), MessageType.Error);
                Debug.WriteLine($"Error en Start_Game_IA_Click: {ex}");
            }
        }

        private async void Start_Game_IA_Click(object sender, RoutedEventArgs e)
        {
            var token = AuthHelper.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                PopUpInfo.ShowMessage("No se encontró el token de autenticación", Window.GetWindow(this), MessageType.Warning);
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
                var response = await TCPClient.ReceiveMessageAsync();

                if (response.Status == "START_GAME_SUCCESS")
                {
                    if (response.Data != null &&
                        response.Data.TryGetValue("codigo", out string codigo) &&
                        response.Data.TryGetValue("color", out string color) &&
                        response.Data.TryGetValue("nombreUsuario", out string user))
                    {
                        Debug.WriteLine($"Partida creada - Código: {codigo}, Color: {color}");
                        response.Data.TryGetValue("partidasGanadas", out string partidasGanadas);
                        response.Data.TryGetValue("racha", out string racha);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NavigationService.Navigate(new Game(codigo, AuthHelper.GetToken(), color, user, partidasGanadas, racha));
                        });
                    }
                }
                else
                {
                    PopUpInfo.ShowMessage("No se pudo iniciar la partida con la IA", Window.GetWindow(this), MessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                PopUpInfo.ShowMessage($"Error de conexión: {ex.Message}", Window.GetWindow(this), MessageType.Error);
                Debug.WriteLine($"Error en Start_Game_IA_Click: {ex}");
            }
        }

        private async void Show_Ranking_Click(object sender, RoutedEventArgs e)
        {
            var token = AuthHelper.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                PopUpInfo.ShowMessage("No se encontró el token de autenticación", Window.GetWindow(this), MessageType.Warning);
                return;
            }

            var request = new RequestMessage
            {
                Command = "GET_RANKING",
                Data = new Dictionary<string, string> { { "token", token } }
            };

            try
            {
                await TCPClient.SendOnlyMessageAsync(request);

                var jsonString = await TCPClient.ReceiveMessageAsyncRaw();
                var response = System.Text.Json.JsonSerializer.Deserialize<ResponseMessageObject>(jsonString);


                if (response.Status == "RANKING_SUCCESS")
                {
                    if (response.Data != null &&
                        response.Data.TryGetValue("ranking", out JsonElement rankingElement) &&
                        rankingElement.ValueKind == JsonValueKind.Array)
                    {
                        var players = System.Text.Json.JsonSerializer.Deserialize<List<PlayerRank>>(
                            rankingElement.GetRawText(),
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        if (response.Data.TryGetValue("current_user_position", out JsonElement posElement) &&
                            posElement.TryGetInt32(out int currentUserPosition))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                NavigationService.Navigate(new RankingPage(players, currentUserPosition));
                            });
                        }
                        else
                        {
                            PopUpInfo.ShowMessage("No se pudo obtener la posición del usuario actual", Window.GetWindow(this), MessageType.Warning);
                        }
                    }
                    else
                    {
                        PopUpInfo.ShowMessage("No se pudo obtener el ranking", Window.GetWindow(this), MessageType.Warning);
                    }
                }
                else
                {
                    PopUpInfo.ShowMessage($"Error al obtener ranking: {response.Message}", Window.GetWindow(this), MessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                PopUpInfo.ShowMessage($"Error de conexión: {ex.Message}", Window.GetWindow(this), MessageType.Error);
                Debug.WriteLine($"Error en Show_Ranking_Click: {ex}");
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

                    await TCPClient.SendOnlyMessageAsync(request);
                    var response = await TCPClient.ReceiveMessageAsync();

                    if (response.Status == "LOGOUT_SUCCESS")
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NavigationService.Navigate(new Login());
                        });
                    }
                }

                NavigationService.Navigate(new Login());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en Exit_Click: {ex}");
                NavigationService.Navigate(new Login());
            }
        }
    }
}
