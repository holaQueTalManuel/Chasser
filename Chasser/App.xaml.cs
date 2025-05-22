using System;
using System.Configuration;
using System.Data;
using System.Windows;
using Chasser.Logic.Network;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Chasser.Common.Data;
using Chasser.Common.Logic.Enums;
using Chasser.View;

namespace Chasser
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        private readonly string IP = "localhost";
        private readonly int PORT = 5000;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            

            // Intentar conexión TCP antes de mostrar la ventana
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

                Shutdown();
                return;
            }

            // Mostrar la ventana principal
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            //if (await AuthHelper.IsUserAuthenticatedAsync())
            //{
            //    mainWindow.MainFrame.Navigate(new MainPage());
            //}
            //else
            //{
            //    mainWindow.MainFrame.Navigate(new Login());
            //}
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // ✅ Registrar el DbContext
            services.AddDbContext<ChasserContext>(options =>
                options.UseSqlServer("Server=DESKTOP-MCGMEA7\\SQLEXPRESS;Database=Chasser_DB;Integrated Security=True;TrustServerCertificate=True;"));

            // ✅ Registrar la ventana principal
            services.AddTransient<MainWindow>();

            // (opcional) otros servicios si necesitas
            // services.AddSingleton<TCPServer>();
        }
    }
}
