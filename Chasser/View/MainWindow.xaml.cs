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
using System.Windows.Media.Animation;

namespace Chasser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        

        public MainWindow()
        {
            InitializeComponent();
           MainFrame.Navigate(new Login());


            //if (File.Exists(userPath))
            //{
            //    string savedUser = File.ReadAllText(userPath);
            //    var user = _context.Usuarios.FirstOrDefault(u => u.Nombre == savedUser);

            //    if (user != null)
            //    {
            //        MainFrame.Navigate(new MainPage());
            //        return;
            //    }
                
            //}
            //else
            //{
            //    MainFrame.Navigate(new Login());
            //}

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
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
        

    }
    

    }