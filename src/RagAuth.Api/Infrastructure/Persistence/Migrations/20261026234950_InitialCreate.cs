using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RagAuth.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    accessCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    passwordHash = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clientId = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    telephone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.id);
                    table.ForeignKey(
                        name: "FK_businesses_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_businesses_clientId",
                table: "businesses",
                column: "clientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_accessCode",
                table: "clients",
                column: "accessCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_email_active",
                table: "clients",
                column: "email",
                unique: true,
                filter: "\"deletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "businesses");

            migrationBuilder.DropTable(
                name: "clients");
        }
    }
}
