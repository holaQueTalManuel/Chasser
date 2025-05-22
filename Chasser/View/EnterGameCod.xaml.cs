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
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;

namespace Chasser.View
{
    /// <summary>
    /// Lógica de interacción para EnterGameCod.xaml
    /// </summary>
    public partial class EnterGameCod : Window
    {
        private readonly string _token;
        public EnterGameCod(string token)
        {
            InitializeComponent();
            _token = token;
        }
        
    }
}
