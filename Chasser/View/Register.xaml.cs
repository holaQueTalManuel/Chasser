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
using Chasser.Logic;
using Chasser.Model;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para Register.xaml
    /// </summary>
    public partial class Register : Page
    {
        private readonly ChasserContext _context;
        public Register()
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetRequiredService<ChasserContext>();
        }
        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Login());
        }

        //await solo se puede utilzar dentro de un metodo asincrono
        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password != null && ConfirmPasswordBox.Password != null && EmailBox.Text != null
                && UsernameBox.Text != null)
            {
                if (PasswordBox.Password == ConfirmPasswordBox.Password)
                {
                    string message = $"REGISTER|{UsernameBox.Text}|{PasswordBox.Password}|{EmailBox.Text}";

                    try
                    {
                        string response = await TCPClient.SendMessageAsync(message);
                        if (response == "REGISTER_SUCCESS")
                        {
                            NavigationService.Navigate(new MainPage());
                        }
                        else
                        {
                            MessageBox.Show("El registro ha fallado. Por favor, inténtelo de nuevo.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error de conexión: " + ex.Message);
                    }
                }

                NavigationService.Navigate(new Game());
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
