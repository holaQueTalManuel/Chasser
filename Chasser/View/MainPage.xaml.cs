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

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private readonly ChasserContext _context;
        public MainPage()
        {
            InitializeComponent();
            _context = App.ServiceProvider.GetRequiredService<ChasserContext>();
        }
        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Game());
        }
        private void JoinGame_Click(object sender, RoutedEventArgs e)
        {
            EnterGameWindow window = new EnterGameWindow();
            bool? result = window.ShowDialog();

            if (result == true)
            {
                NavigationService.Navigate(new Game());
            }
        }
    }
}

