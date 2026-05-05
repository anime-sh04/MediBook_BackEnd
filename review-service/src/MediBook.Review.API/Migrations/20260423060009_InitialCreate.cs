using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediBook.Review.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    review_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    appointment_id = table.Column<int>(type: "integer", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    review_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.review_id);
                    table.CheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_appointment_id_unique",
                table: "reviews",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_patient_id",
                table: "reviews",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_provider_id",
                table: "reviews",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_rating",
                table: "reviews",
                column: "rating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews");
        }
    }
}
