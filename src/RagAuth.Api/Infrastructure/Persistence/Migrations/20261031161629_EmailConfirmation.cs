using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RagAuth.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "emailConfirmed",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "emailConfirmedAt",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            // FR-020: contas existentes não removidas passam a ser consideradas confirmadas.
            migrationBuilder.Sql(
                "UPDATE clients SET \"emailConfirmed\" = true, \"emailConfirmedAt\" = \"createdAt\" WHERE \"deletedAt\" IS NULL;");

            migrationBuilder.CreateTable(
                name: "email_confirmation_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clientId = table.Column<int>(type: "integer", nullable: false),
                    selector = table.Column<string>(type: "varchar(22)", nullable: false),
                    validatorHash = table.Column<string>(type: "char(64)", nullable: false),
                    purpose = table.Column<string>(type: "varchar(32)", nullable: false),
                    origin = table.Column<string>(type: "varchar(32)", nullable: false),
                    issuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_confirmation_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_email_confirmation_links_clients_clientId",
                        column: x => x.clientId,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_confirmation_links_client_issued",
                table: "email_confirmation_links",
                columns: new[] { "clientId", "issuedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_email_confirmation_links_active_per_client",
                table: "email_confirmation_links",
                column: "clientId",
                unique: true,
                filter: "\"usedAt\" IS NULL AND \"revokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_email_confirmation_links_selector",
                table: "email_confirmation_links",
                column: "selector",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_confirmation_links");

            migrationBuilder.DropColumn(
                name: "emailConfirmed",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "emailConfirmedAt",
                table: "clients");
        }
    }
}
