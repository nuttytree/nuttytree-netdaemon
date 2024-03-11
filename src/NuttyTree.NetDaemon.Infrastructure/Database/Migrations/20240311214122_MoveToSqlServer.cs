using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuttyTree.NetDaemon.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class MoveToSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Calendar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAllDay = table.Column<bool>(type: "bit", nullable: false),
                    Person = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToDoListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinutesEarned = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewUid = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToDoListItems", x => x.Id);
                    table.UniqueConstraint("AK_ToDoListItems_Uid", x => x.Uid);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentReminders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ArriveLeadMinutes = table.Column<int>(type: "int", nullable: true),
                    AnnouncementTypes = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NextAnnouncementType = table.Column<int>(type: "int", nullable: true),
                    NextAnnouncement = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAnnouncement = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<bool>(type: "bit", nullable: false),
                    TravelMiles = table.Column<double>(type: "float", nullable: true),
                    TravelMinutes = table.Column<double>(type: "float", nullable: true),
                    NextTravelTimeUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentId = table.Column<string>(type: "nvarchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentReminders_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminders_AppointmentId",
                table: "AppointmentReminders",
                column: "AppointmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentReminders");

            migrationBuilder.DropTable(
                name: "ToDoListItems");

            migrationBuilder.DropTable(
                name: "Appointments");
        }
    }
}
