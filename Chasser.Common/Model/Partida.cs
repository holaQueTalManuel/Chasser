using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Common.Model
{
    public class Partida
    {
        public int Id { get; set; }
        public string Ganador {  get; set; }
        public Usuario Jugador1 {  get; set; }
        public Usuario Jugador2 {  get; set; }

        // Propiedades de claves foráneas
        public int Jugador1Id { get; set; } // Clave foránea a Usuario
        public int Jugador2Id { get; set; } // Clave foránea a Usuario
        public string Codigo { get; set; }
        public DateTime Duracion { get; set; }
        public DateTime Fecha_Creacion { get; set; }
        public ICollection<Partida_Jugador> PartidasJugadores { get; set; }
    }
}
