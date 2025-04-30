using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Model
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Contrasenia { get; set; }
        public int? Partidas_Ganadas { get; set; }

        public DateTime Fecha_Creacion { get; set; }
    }
}
