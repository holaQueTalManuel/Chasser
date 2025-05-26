using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Chasser.Common.Model;

namespace Chasser.Common.Data
{
    public class ChasserContext : DbContext
    {
        public ChasserContext() { }
        public ChasserContext(DbContextOptions<ChasserContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Partida> Partidas { get; set; }
        public DbSet<Partida_Jugador> Partidas_Jugadores { get; set; }
        public DbSet<Sesion_Usuario> Sesiones_Usuarios { get; set; }  

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer("Server=DESKTOP-MCGMEA7\\SQLEXPRESS;Database=Chasser_DB;Integrated Security=True;TrustServerCertificate=True;");

        //usuario para cuando lo ponga en marcha
        //options.UseSqlServer("Server=DESKTOP-MCGMEA7\\SQLEXPRESS;Database=Chasser_DB;User Id=chasser_user;Password=chasser_user;TrustServerCertificate=True;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración para Partida
            modelBuilder.Entity<Partida>(entity =>
            {
                entity.HasOne(p => p.Jugador1)
                      .WithMany()
                      .HasForeignKey(p => p.Jugador1Id)
                      .OnDelete(DeleteBehavior.Restrict); // Evita el borrado en cascada para Jugador1

                entity.HasOne(p => p.Jugador2)
                      .WithMany()
                      .HasForeignKey(p => p.Jugador2Id)
                      .OnDelete(DeleteBehavior.Restrict); // Evita el borrado en cascada para Jugador2
            });

            // Configuración para Sesion_Usuario
            modelBuilder.Entity<Sesion_Usuario>()
                .HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade); // Borrado en cascada para Sesion_Usuario

            modelBuilder.Entity<Sesion_Usuario>()
                .HasIndex(su => su.Token)
                .IsUnique();

            // Configuración para Partida_Jugador
            modelBuilder.Entity<Partida_Jugador>(entity =>
            {
                entity.HasKey(pj => new { pj.PartidaId, pj.UsuarioId });

                entity.HasOne(pj => pj.Partida)
                      .WithMany(p => p.PartidasJugadores)
                      .HasForeignKey(pj => pj.PartidaId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pj => pj.Usuario)
                      .WithMany()
                      .HasForeignKey(pj => pj.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

    }
}
