using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;
using Chasser.View;

namespace Chasser
{
    public partial class Register : Page
    {
        private bool isPasswordVisible = false;
        private bool isConfirmPasswordVisible = false;

        public Register()
        {
            InitializeComponent();
            Loaded += Login_Loaded;
        }

        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.ResizeAndCenterWindow(900, 600);
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Login());
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);

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

                        if (response.Status == "REGISTER_SUCCESS" && response.Data != null)
                        {
                            if (response.Data["token"] != null)
                            {
                                var token = response.Data["token"];
                                AuthHelper.SetToken(token);
                                NavigationService.Navigate(new MainPage());
                            }
                        }
                        else
                        {
                            PopUpInfo.ShowMessage(
                                $"El registro ha fallado. Causa: {response.Message}",
                                owner,
                                MessageType.Error
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        PopUpInfo.ShowMessage(
                            "Error de conexión: " + ex.Message,
                            owner,
                            MessageType.Error
                        );
                    }
                }
                else
                {
                    PopUpInfo.ShowMessage(
                        "Las contraseñas no coinciden.",
                        owner,
                        MessageType.Warning
                    );
                }
            }
            else
            {
                PopUpInfo.ShowMessage(
                    "Por favor, rellene todos los campos.",
                    owner,
                    MessageType.Warning
                );
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

        private void UpdateConfirmPasswordPlaceholder()
        {
            string passwordText = isConfirmPasswordVisible ? ConfirmPasswordTextBox.Text : ConfirmPasswordBox.Password;
            ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(passwordText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            string target = button.Tag.ToString();

            if (target == "Password")
            {
                isPasswordVisible = !isPasswordVisible;
                ToggleVisibility(
                    PasswordBox,
                    PasswordTextBox,
                    isPasswordVisible,
                    button
                );
                UpdatePasswordPlaceholder();
            }
            else if (target == "ConfirmPassword")
            {
                isConfirmPasswordVisible = !isConfirmPasswordVisible;
                ToggleVisibility(
                    ConfirmPasswordBox,
                    ConfirmPasswordTextBox,
                    isConfirmPasswordVisible,
                    button
                );
                UpdateConfirmPasswordPlaceholder();
            }
        }

        private void ToggleVisibility(
            PasswordBox passwordBox,
            TextBox textBox,
            bool isVisible,
            Button button)
        {
            if (isVisible)
            {
                textBox.Text = passwordBox.Password;
                passwordBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                button.Content = "🙈";
            }
            else
            {
                passwordBox.Password = textBox.Text;
                passwordBox.Visibility = Visibility.Visible;
                textBox.Visibility = Visibility.Collapsed;
                button.Content = "👁️";
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateConfirmPasswordPlaceholder();
        }

        private void ConfirmPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateConfirmPasswordPlaceholder();
        }

        private void ConfirmPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ConfirmPasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void ConfirmPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateConfirmPasswordPlaceholder();
        }

        private void ConfirmPasswordTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ConfirmPasswordPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void ConfirmPasswordTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateConfirmPasswordPlaceholder();
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Storyboard sb = (Storyboard)this.FindResource("FadeInStoryboard");
            sb.Begin(this);
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Login());
        }
    }
}