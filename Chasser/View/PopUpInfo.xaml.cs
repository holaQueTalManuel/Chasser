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
using System.Windows.Shapes;
using Chasser.Common.Logic.Enums;

namespace Chasser.View
{
    /// <summary>
    /// Lógica de interacción para PopUpInfo.xaml
    /// </summary>
    public partial class PopUpInfo : Window
    {
        public PopUpInfo(string message, MessageType type)
        {
            InitializeComponent();
            MessageText.Text = message;

            string iconPath = type switch
            {
                MessageType.Info => "/Imgs/infoIcon.png",
                MessageType.Warning => "/Imgs/warning.png",
                MessageType.Error => "/Imgs/erroricon.png",
                MessageType.Success => "/Imgs/sucessicon.png",
                _ => "/Imgs/infoIcon.png"
            };

            IconImage.Source = new BitmapImage(new Uri($"pack://application:,,,{iconPath}", UriKind.Absolute));
        }
        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        public static void ShowMessage(string message, Window owner, MessageType type = MessageType.Info)
        {
            var popup = new PopUpInfo(message, type)
            {
                Owner = owner
            };
            popup.ShowDialog();
        }
    }
}
