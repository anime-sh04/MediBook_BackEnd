using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediBook.Provider.API.Migrations
{
    /// <inheritdoc />
    public partial class InititalCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_profiles",
                columns: table => new
                {
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    qualification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    experience_years = table.Column<int>(type: "integer", nullable: false),
                    bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    clinic_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    clinic_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    consultation_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    avg_rating = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_profiles", x => x.provider_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_profiles_user_id",
                table: "provider_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_profiles");
        }
    }
}
