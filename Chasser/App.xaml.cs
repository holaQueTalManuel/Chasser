using System.Configuration;
using System.Data;
using System.Windows;
using Chasser.Logic.Network;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chasser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        private readonly string IP = "192.168.56.1";
        private readonly int PORT = 5000;

        public App()
        {
            var serviceCollection = new ServiceCollection();

            // Registra tu DbContext con la cadena de conexión
            serviceCollection.AddDbContext<ChasserContext>(options =>
                options.UseSqlServer("Server=DESKTOP-MCGMEA7\\SQLEXPRESS;Database=Chasser_DB;Integrated Security=True;TrustServerCertificate=True;"));

            // Registra la ventana principal
            serviceCollection.AddTransient<MainWindow>();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Obtén la instancia de MainWindow desde DI
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // Intentar conexión TCP (con manejo de errores)
            try
            {
                await TCPClient.ConnectAsync(IP, PORT);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo conectar al servidor: " + ex.Message,
                              "Error crítico",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);

                // Cierra la aplicación si no hay conexión
                Shutdown();
                return; // Importante para evitar que continúe
            }
        }
    }

}
