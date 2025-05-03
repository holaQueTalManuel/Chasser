using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Chasser.Logic.Network;
using Chasser.Common.Network;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para Login.xaml
    /// </summary>
    public partial class Login : Page
    {
        private string userPath = "user.txt";
        private bool _isPasswordVisible = false;

        public Login()
        {
            InitializeComponent();
            
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var request = new RequestMessage
                {
                    Command = "LOGIN",
                    Data = new Dictionary<string, string>
                    {
                        { "username", username },
                        { "password", password }
                    }
                };

                try
                {
                    var response = await TCPClient.SendJsonAsync(request);

                    if (response.Status == "LOGIN_SUCCESS")
                    {
                        var token = response.Data["token"];
                        AuthHelper.SetToken(token);
                        NavigationService.Navigate(new MainPage());
                    }
                    else
                    {
                        MessageBox.Show($"Inicio de sesión fallido. Motivo: {response.Message}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error de conexión: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Ambos campos deben estar completos.");
            }
        }


        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Register());
        }
        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // No necesita lógica, solo fuerza actualización del binding
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox.Tag = PasswordBox.Password;
        }
        private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            // Cambiar el tipo de visualización de la contraseña
            if (_isPasswordVisible)
            {
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordTextBox.Text = PasswordBox.Password;
            }
            else
            {
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
            }
        }
        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
            }
        }

    }


}
