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
        
    }
}