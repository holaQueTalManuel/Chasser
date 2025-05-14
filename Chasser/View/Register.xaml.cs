using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Chasser.Common.Network;
using Chasser.Logic.Network;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para Register.xaml
    /// </summary>
    public partial class Register : Page
    {
        private bool isPasswordVisible = false;

        public Register()
        {
            InitializeComponent();
            Loaded += Login_Loaded;
        }

        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.AjustarTamaño(900, 600);
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Login());
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(EmailBox.Text) &&
                !string.IsNullOrWhiteSpace(UsernameBox.Text) &&
                !string.IsNullOrWhiteSpace(PasswordBox.Password) &&
                !string.IsNullOrWhiteSpace(ConfirmPasswordBox.Password))
            {
                if (PasswordBox.Password == ConfirmPasswordBox.Password)
                {
                    var request = new RequestMessage
                    {
                        Command = "REGISTER",
                        Data = new Dictionary<string, string>
                        {
                            { "username", UsernameBox.Text.Trim() },
                            { "password", PasswordBox.Password.Trim() },
                            { "email", EmailBox.Text.Trim() }
                        }
                    };

                    try
                    {
                         await TCPClient.SendOnlyMessageAsync(request);
                        var response = await TCPClient.ReceiveMessageAsync();

                        if (response.Status == "REGISTER_SUCCESS")
                        {
                            NavigationService.Navigate(new MainPage());
                        }
                        else
                        {
                            MessageBox.Show($"El registro ha fallado. Causa: {response.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error de conexión: " + ex.Message);
                    }
                }
                else
                {
                    MessageBox.Show("Las contraseñas no coinciden.");
                }
            }
            else
            {
                MessageBox.Show("Por favor, rellene todos los campos.");
            }
        }

        private void EmailBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            EmailPlaceholder.Visibility = string.IsNullOrWhiteSpace(EmailBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void EmailBox_GotFocus(object sender, RoutedEventArgs e)
        {
            EmailPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void EmailBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailBox.Text))
                EmailPlaceholder.Visibility = Visibility.Visible;
        }

        // Placeholder para nombre de usuario
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

        // Placeholder para contraseña
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholder();
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePasswordPlaceholder();
        }

        private void PasswordTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PasswordTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholder();
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePasswordPlaceholder();
        }

        private void UpdatePasswordPlaceholder()
        {
            string passwordText = isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(passwordText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Mostrar/ocultar contraseña
        private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;

            if (isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "🙈";
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "👁️";
            }

            UpdatePasswordPlaceholder();
        }

        // Confirmar contraseña: se usa Tag para el binding del placeholder
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ConfirmPasswordBox.Tag = ConfirmPasswordBox.Password;
        }
        private bool isConfirmPasswordVisible = false;

        
    }
}
