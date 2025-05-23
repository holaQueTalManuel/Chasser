using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Chasser.ViewModels
{
    public class NavigationService : INavigationService
    {
        private readonly Frame _frame;
        private readonly IServiceProvider _serviceProvider;

        public NavigationService(Frame frame, IServiceProvider serviceProvider)
        {
            _frame = frame;
            _serviceProvider = serviceProvider;
        }

        public void NavigateTo(string pageKey)
        {
            switch (pageKey)
            {
                case "MainPage":
                    _frame.Navigate(_serviceProvider.GetService<MainPage>());
                    break;
                case "Register":
                    _frame.Navigate(_serviceProvider.GetService<Register>());
                    break;
                case "Recovery":
                    //_frame.Navigate(_serviceProvider.GetService<Recovery>());
                    break;
                case "Login":
                    _frame.Navigate(_serviceProvider.GetService<Login>());
                    break;
                case "Game":
                    _frame.Navigate(_serviceProvider.GetService<Game>());
                    break;
                    // Agrega más páginas según sea necesario
            }
        }
    }
}