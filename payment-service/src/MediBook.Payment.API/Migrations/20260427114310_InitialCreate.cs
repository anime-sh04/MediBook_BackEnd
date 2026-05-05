using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediBook.Payment.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    payment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    appointment_id = table.Column<int>(type: "integer", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "INR"),
                    razorpay_order_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    razorpay_payment_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_id = table.Column<int>(type: "integer", nullable: false),
                    appointment_date = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    start_time = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    end_time = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    service_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    mode_of_consultation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.payment_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_appointment_id",
                table: "payments",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_correlation_id",
                table: "payments",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_paid_at",
                table: "payments",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "ix_payments_patient_id",
                table: "payments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_provider_id",
                table: "payments",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_status",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_payments_transaction_id",
                table: "payments",
                column: "transaction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
