using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Chasser.ViewModels;

namespace Chasser.View
{
    public partial class RecoveryWindow : Window
    {
        private bool isNewPasswordVisible = false;
        private bool isConfirmPasswordVisible = false;
        public RecoveryWindow()
        {
            InitializeComponent();
            DataContext = new RecoveryViewModel(this);
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Storyboard sb = (Storyboard)this.FindResource("FadeInStoryboard");
            sb.Begin(this);

            
        }
        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RecoveryViewModel vm)
                vm.NewPassword = NewPasswordBox.Password;
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RecoveryViewModel vm)
                vm.ConfirmPassword = ConfirmPasswordBox.Password;
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
        

        private void NewPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Lógica adicional si es necesaria
        }

        

        private void ConfirmPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Lógica adicional si es necesaria
        }

        
    }
}

