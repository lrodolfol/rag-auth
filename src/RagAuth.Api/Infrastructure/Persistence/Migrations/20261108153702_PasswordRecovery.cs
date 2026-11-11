using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RagAuth.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PasswordRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "passwordChangedAt",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "password_recovery_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clientId = table.Column<int>(type: "integer", nullable: false),
                    selector = table.Column<string>(type: "varchar(22)", nullable: false),
                    validatorHash = table.Column<string>(type: "char(64)", nullable: false),
                    purpose = table.Column<string>(type: "varchar(32)", nullable: false),
                    issuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_recovery_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_recovery_links_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_password_recovery_links_client_issued",
                table: "password_recovery_links",
                columns: new[] { "clientId", "issuedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_password_recovery_links_active_per_client",
                table: "password_recovery_links",
                column: "clientId",
                unique: true,
                filter: "\"usedAt\" IS NULL AND \"revokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_password_recovery_links_selector",
                table: "password_recovery_links",
                column: "selector",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_recovery_links");

            migrationBuilder.DropColumn(
                name: "passwordChangedAt",
                table: "clients");
        }
    }
}
