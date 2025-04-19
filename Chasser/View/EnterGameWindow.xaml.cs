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

namespace Chasser
{
    /// <summary>
    /// Lógica de interacción para EnterGameWindow.xaml
    /// </summary>
    public partial class EnterGameWindow : Window
    {
        public EnterGameWindow()
        {
            InitializeComponent();
        }

        public void EnterGame_Click(object sender, RoutedEventArgs e)
        {
            string code = inputCod.Text.Trim();

            if (!string.IsNullOrEmpty(code))
            {
                this.DialogResult = true; // Muy importante: marca la ventana como "OK"
                this.Close();
            }
            else
            {
                MessageBox.Show("Debes introducir un codigo valido");
            }
        }
    }
}
