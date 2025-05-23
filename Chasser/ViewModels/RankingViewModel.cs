using System.Collections.Generic;
using System.Windows.Input;
using Chasser.Common.Network;
using System.Threading.Tasks;
using System.Windows;
using Chasser.Logic.Network;
using Chasser.Common.Model;
using System.Collections.ObjectModel;
using Chasser.Logic;

namespace Chasser.ViewModels
{
    public class RankingViewModel : BaseViewModel
    {
        public ObservableCollection<PlayerRank> Players { get; }
        public int CurrentUserPosition { get; }

        public ICommand VolverCommand { get; }

        public RankingViewModel(ObservableCollection<PlayerRank> players, int currentUserPosition)
        {
            Players = players;
            CurrentUserPosition = currentUserPosition;
            VolverCommand = new RelayCommand(() => /* lógica para volver */{ });
        }
    }
}