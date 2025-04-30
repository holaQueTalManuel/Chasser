using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;

namespace Chasser.Utilities
{
    public class BCryptPasswordHasher
    {
        // Hashea la contraseña con un factor de costo configurable.
        public static string HashPassword(string password)
        {
            // El parámetro 12 es un ejemplo; puedes ajustarlo según el rendimiento deseado.
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        // Verifica la contraseña ingresada contra el hash almacenado.
        public static bool VerifyPassword(string password, string storedHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
    }
}
