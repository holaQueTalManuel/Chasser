using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Model
{
    public class Partida_Jugador
    {
        public int Id { get; set; }
        public int PartidaId { get; set; }
        public int Jugador1 { get; set; }
        public int Jugador2 { get; set; }
        
    }
}
