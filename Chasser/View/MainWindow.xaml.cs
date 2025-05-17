using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.IO;
using Chasser.Common.Data;
using System.ComponentModel;
using Chasser.Logic.Network;

namespace Chasser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string userPath = "user.txt";
        private readonly ChasserContext _context;
        string connectionString = "Server=DESKTOP-MCGMEA7\\SQLEXPRESS;Database=Chasser_DB;Integrated Security=True;TrustServerCertificate=True;";
        // Query SQL
        string query = "SELECT id, nombre, fecha_creacion, correo FROM usuarios";


        public MainWindow(ChasserContext context)
        {
            InitializeComponent();
           // MainFrame.Navigate(new Login());

            _context = context;

            if (File.Exists(userPath))
            {
                string savedUser = File.ReadAllText(userPath);
                var user = _context.Usuarios.FirstOrDefault(u => u.Nombre == savedUser);

                if (user != null)
                {
                    MainFrame.Navigate(new MainPage());
                    return;
                }
                
            }
            else
            {
                MainFrame.Navigate(new Login());
            }

        }
        public void AjustarTamaño(double ancho, double alto)
        {
            this.SizeToContent = SizeToContent.Manual;
            this.Width = ancho;
            this.Height = alto;
        }
        private void CenterWindowOnScreen()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var windowWidth = this.Width;
            var windowHeight = this.Height;
            this.Left = (screenWidth / 2) - (windowWidth / 2);
            this.Top = (screenHeight / 2) - (windowHeight / 2);
        }
        public void ResizeAndCenterWindow(double width, double height)
        {
            this.Width = width;
            this.Height = height;
            CenterWindowOnScreen();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            // Cerrar la conexión TCP correctamente antes de salir
            try
            {
                if (TCPClient.IsConnected) // Asume que tienes esta propiedad
                {
                    TCPClient.Disconnect(); // Método que cierra Socket/Stream
                }
            }
            catch { } // Ignorar errores durante el cierre

            base.OnClosing(e);
        }

    }
    

    }