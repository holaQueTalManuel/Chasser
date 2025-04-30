using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Model
{
    public class Partida
    {
        public int Id { get; set; }
        public string Ganador {  get; set; }
        public Usuario Jugador1 {  get; set; }
        public Usuario Jugador2 {  get; set; }
        public string Codigo { get; set; }
        public DateTime Duracion { get; set; }
        public DateTime Fecha_Creacion { get; set; }
    }
}
