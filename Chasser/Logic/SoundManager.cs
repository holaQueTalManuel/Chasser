using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Chasser.Logic
{

    public static class SoundManager
    {
        private static readonly MediaPlayer _player = new MediaPlayer();

        public static void PlayMoveSound()
        {
            PlaySound("Imgs/caballo-ajedrez-95114.mp3");
        }

        private static void PlaySound(string relativePath)
        {
            try
            {
                var uri = new Uri($"pack://siteoforigin:,,,/{relativePath}");
                _player.Open(uri);
                _player.Volume = 0.7;
                _player.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SoundManager] Error al reproducir sonido: {ex.Message}");
            }
        }
    }
}
