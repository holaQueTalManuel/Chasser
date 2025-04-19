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
using Chasser.Logic;

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para Login.xaml
    /// </summary>
    public partial class Login : Page
    {
        private readonly ChasserContext _context;
        private string userPath = "user.txt";
        private bool _isPasswordVisible = false;

        public Login()
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetRequiredService<ChasserContext>();

            
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            //string storedHash = "$2a$12$n1oLTvrPMq1K4.XKBVGDm.rspyD5yhpPAmndYcwx/8KQd8bpyX2ui";
            //bool ok = BCrypt.Net.BCrypt.Verify(password, storedHash);
            //MessageBox.Show(ok.ToString());

            if (!username.IsNullOrEmpty() && !password.IsNullOrEmpty())
            {

                var user = _context.Usuarios.FirstOrDefault(x => x.Nombre == username);

                if (user != null && BCryptPasswordHasher.VerifyPassword(password, user.Contrasenia.Trim()))
                {
                    File.WriteAllText(userPath, user.Nombre);
                    NavigationService.Navigate(new MainPage());
                }
                else
                {
                    MessageBox.Show("Nombre de usuario o contraseña incorrectos.");
                }



            }
            else
            {
                MessageBox.Show("Ambos campos deben de estar rellenos como minimo");
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
