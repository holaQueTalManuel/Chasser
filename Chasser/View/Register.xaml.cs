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
using Microsoft.Extensions.DependencyInjection;
using Chasser.Model;
using Chasser.Logic.Network;
using Chasser.Common.Network;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para Register.xaml
    /// </summary>
    public partial class Register : Page
    {
        public Register()
        {
            InitializeComponent();
        }
        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Login());
        }

        //await solo se puede utilzar dentro de un metodo asincrono
        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PasswordBox.Password) &&
                !string.IsNullOrWhiteSpace(ConfirmPasswordBox.Password) &&
                !string.IsNullOrWhiteSpace(EmailBox.Text) &&
                !string.IsNullOrWhiteSpace(UsernameBox.Text))
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
                        var response = await TCPClient.SendJsonAsync(request);

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




        private void EmailBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox.Tag = PasswordBox.Password;
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ConfirmPasswordBox.Tag = ConfirmPasswordBox.Password;
        }

    }




}
