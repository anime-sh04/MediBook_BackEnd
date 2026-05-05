using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediBook.Schedule.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "availability_slots",
                columns: table => new
                {
                    slot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_booked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    recurrence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "none"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_slots", x => x.slot_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_availability_slots_provider_available",
                table: "availability_slots",
                columns: new[] { "provider_id", "is_booked", "is_blocked" });

            migrationBuilder.CreateIndex(
                name: "ix_availability_slots_provider_date",
                table: "availability_slots",
                columns: new[] { "provider_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_availability_slots_provider_id",
                table: "availability_slots",
                column: "provider_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability_slots");
        }
    }
}
