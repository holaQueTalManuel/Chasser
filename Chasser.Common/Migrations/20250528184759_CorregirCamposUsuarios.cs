using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chasser.Common.Migrations
{
    /// <inheritdoc />
    public partial class CorregirCamposUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Racha_Victorias",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Partidas_Jugadas",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Racha_Victorias",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Partidas_Jugadas",
                table: "Usuarios");
        }
    }
}
