using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Model;

namespace Chasser.Common.Model
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Contrasenia { get; set; }
        public int? Partidas_Ganadas { get; set; }
        public int? Racha_Victorias { get; set; }
        public int? Partidas_Jugadas { get; set; }
        public ICollection<Sesion_Usuario> Sesiones { get; set; }
        public DateTime Fecha_Creacion { get; set; }
    }
}
