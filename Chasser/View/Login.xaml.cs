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
using Chasser.Logic;
using Chasser.View;
using Chasser.Common.Logic.Enums;
using System.Windows.Media.Animation;

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
            this.Loaded += Login_Loaded;


        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)this.Resources["FadeInStoryboard"]).Begin();
            ((Storyboard)this.Resources["LogoZoomStoryboard"]).Begin();
        }
        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.ResizeAndCenterWindow(900, 600);
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
                    LoadingOverlay.Visibility = Visibility.Visible;
                    Storyboard spinner = (Storyboard)this.FindResource("WindowsStyleSpinnerAnimation");
                    spinner.Begin();

                    await TCPClient.SendOnlyMessageAsync(request);
                    var response = await TCPClient.ReceiveMessageAsync();

                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    spinner.Stop();

                    if (response.Status == "LOGIN_SUCCESS")
                    {
                        var token = response.Data["token"];
                        AuthHelper.SetToken(token);
                        NavigationService.Navigate(new MainPage(response.Data["username"]));
                    }
                    else
                    {
                        PopUpInfo.ShowMessage($"Inicio de sesión fallido. Motivo: {response.Message}", Window.GetWindow(this), MessageType.Error);
                    }
                }
                catch (Exception ex)
                {
                    PopUpInfo.ShowMessage("Error de conexión: " + ex.Message, Window.GetWindow(this), MessageType.Warning);
                }
            }
            else
            {
                PopUpInfo.ShowMessage("Ambos campos deben estar completos.", Window.GetWindow(this), MessageType.Warning);

            }
        }

        public async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            new RecoveryWindow(){ Owner = Window.GetWindow(this) }.ShowDialog();
        }


        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Register());
        }
        // Username
        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UsernamePlaceholder.Visibility = string.IsNullOrWhiteSpace(UsernameBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UsernameBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UsernamePlaceholder.Visibility = Visibility.Collapsed;
        }

        private void UsernameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
                UsernamePlaceholder.Visibility = Visibility.Visible;
        }

        // Password
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PasswordBox.Password))
                PasswordPlaceholder.Visibility = Visibility.Visible;
        }

        // Para cuando el TextBox muestra la contraseña
        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PasswordTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PasswordTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PasswordTextBox.Text))
                PasswordPlaceholder.Visibility = Visibility.Visible;
        }
        private bool isPasswordVisible = false;

        private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;

            if (isPasswordVisible)
            {
                // Mostrar la contraseña en texto
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "👁️"; // o "Ocultar"
            }
            else
            {
                // Ocultar la contraseña
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "🙈"; // o "Mostrar"
            }

            // Mantener visible o no el placeholder
            if (isPasswordVisible)
            {
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordTextBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

    }


}
