using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Chasser.Common.Model;
using Chasser.ViewModels;

namespace Chasser.View
{
    /// <summary>
    /// Lógica de interacción para Ranking.xaml
    /// </summary>
    public partial class RankingPage : Page
    {
        public RankingPage(object rankingData, object currentUserPosition = null)
        {
            InitializeComponent();

            var rankingList = rankingData is List<PlayerRank> list
                    ? new ObservableCollection<PlayerRank>(list)
                    : new ObservableCollection<PlayerRank>(); var userPosition = currentUserPosition != null ? int.Parse(currentUserPosition.ToString()) : 0;

            
            // Asignar el ViewModel con los datos
            this.DataContext = new RankingViewModel(rankingList, userPosition);
        }

        
        private void OnBackButtonClicked(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }
    }
}
