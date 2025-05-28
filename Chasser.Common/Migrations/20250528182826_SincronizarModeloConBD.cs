using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chasser.Common.Migrations
{
    /// <inheritdoc />
    public partial class SincronizarModeloConBD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Partidas_Jugadas",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Racha_Victorias",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Partidas_Jugadas",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Racha_Victorias",
                table: "Usuarios");
        }
    }
}
