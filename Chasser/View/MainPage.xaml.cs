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
using Chasser.Logic.Network;
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
        private async void JoinGame_Click(object sender, RoutedEventArgs e)
        {
            string message = $"START_GAME|";

            try
            {
                var response = await TCPClient.SendAndParseAsync(message);

                if (response.Status == "START_GAME_SUCCESS")
                {
                    NavigationService.Navigate(new Game());
                }
                else if (response.Status.StartsWith("START_GAME_FAIL"))
                {
                    MessageBox.Show($"Error al unirse a la partida: {response.Reason}");
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al unirse a la partida: {ex.Message}");
            }
        }
    }
}
    


