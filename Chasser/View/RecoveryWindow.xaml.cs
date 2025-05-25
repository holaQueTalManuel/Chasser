using System.Windows;
using System.Windows.Media.Animation;
using Chasser.ViewModels;

namespace Chasser.View
{
    public partial class RecoveryWindow : Window
    {
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
    }
}
