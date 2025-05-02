using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chasser.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSesionUsuarioTable2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contrasenia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Partidas_Ganadas = table.Column<int>(type: "int", nullable: true),
                    Fecha_Creacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Partidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ganador = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jugador1Id = table.Column<int>(type: "int", nullable: false),
                    Jugador2Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fecha_Creacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partidas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Partidas_Usuarios_Jugador1Id",
                        column: x => x.Jugador1Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Partidas_Usuarios_Jugador2Id",
                        column: x => x.Jugador2Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sesiones_Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Expiration = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sesiones_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sesiones_Usuarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sesiones_Usuarios_Usuarios_UsuarioId1",
                        column: x => x.UsuarioId1,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Partidas_Jugadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartidaId = table.Column<int>(type: "int", nullable: false),
                    Jugador1Id = table.Column<int>(type: "int", nullable: false),
                    Jugador2Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partidas_Jugadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Partidas_Jugadores_Partidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Partidas_Jugadores_Usuarios_Jugador1Id",
                        column: x => x.Jugador1Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Partidas_Jugadores_Usuarios_Jugador2Id",
                        column: x => x.Jugador2Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Jugador1Id",
                table: "Partidas",
                column: "Jugador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Jugador2Id",
                table: "Partidas",
                column: "Jugador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Jugadores_Jugador1Id",
                table: "Partidas_Jugadores",
                column: "Jugador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Jugadores_Jugador2Id",
                table: "Partidas_Jugadores",
                column: "Jugador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Partidas_Jugadores_PartidaId",
                table: "Partidas_Jugadores",
                column: "PartidaId");

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_Usuarios_Token",
                table: "Sesiones_Usuarios",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_Usuarios_UsuarioId",
                table: "Sesiones_Usuarios",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_Usuarios_UsuarioId1",
                table: "Sesiones_Usuarios",
                column: "UsuarioId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Partidas_Jugadores");

            migrationBuilder.DropTable(
                name: "Sesiones_Usuarios");

            migrationBuilder.DropTable(
                name: "Partidas");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
