using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeLoConsiglio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoliticalProfileIndirizzoMandato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArgomentiFortiJson",
                table: "PoliticalProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LineaPoliticaSource",
                table: "PoliticalProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TemiInteresseJson",
                table: "PoliticalProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNotesMd",
                table: "Acts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceUrlsJson",
                table: "Acts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ActAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    OriginalName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActAttachments_Acts_ActId",
                        column: x => x.ActId,
                        principalTable: "Acts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActAttachments_ActId",
                table: "ActAttachments",
                column: "ActId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActAttachments");

            migrationBuilder.DropColumn(
                name: "ArgomentiFortiJson",
                table: "PoliticalProfiles");

            migrationBuilder.DropColumn(
                name: "LineaPoliticaSource",
                table: "PoliticalProfiles");

            migrationBuilder.DropColumn(
                name: "TemiInteresseJson",
                table: "PoliticalProfiles");

            migrationBuilder.DropColumn(
                name: "ReferenceNotesMd",
                table: "Acts");

            migrationBuilder.DropColumn(
                name: "ReferenceUrlsJson",
                table: "Acts");
        }
    }
}
