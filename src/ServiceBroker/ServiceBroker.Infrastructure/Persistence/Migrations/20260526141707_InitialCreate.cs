using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_instances",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    LastOperationDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_instances", x => x.InstanceId);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_instances_created_at",
                table: "service_instances",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_service_instances_state",
                table: "service_instances",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_instances");
        }
    }
}
