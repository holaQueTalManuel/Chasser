using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Chasser
{
    public static class SoundManager
    {
        private static MediaPlayer mediaPlayer = new MediaPlayer();

        public static void PlayMoveSound()
        {
            try
            {
                mediaPlayer.Open(new Uri("Imgs/caballo-ajedrez-95114.mp3", UriKind.Relative));
                mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al reproducir sonido: " + ex.Message);
            }
        }
    }
}
