using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Model;

namespace Chasser.Common.Model
{
    public class Partida_Jugador
    {
        public int Id { get; set; }
        public int PartidaId { get; set; }
        public int Jugador1Id { get; set; }
        public int Jugador2Id { get; set; }

        public Partida Partida { get; set; }
        public Usuario Jugador1 { get; set; }
        public Usuario Jugador2 { get; set; }

    }
}
