using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Chasser.Model;

namespace Chasser
{
    public class ChasserContext : DbContext
    {
        public ChasserContext(DbContextOptions<ChasserContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Partida> Partidas { get; set; }
        public DbSet<Partida_Jugador> Partidas_Jugadores { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer("Server=DESKTOP-MCGMEA7\\SQLEXPRESS;Database=Chasser_DB;Integrated Security=True;TrustServerCertificate=True;");
    }
}
