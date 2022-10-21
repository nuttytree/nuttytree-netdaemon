using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuttyTree.NetDaemon.Infrastructure.Database.Migrations
{
    public partial class AppointmentReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Calendar = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    StartDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    Person = table.Column<string>(type: "TEXT", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentReminders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ArriveLeadMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    AnnouncementTypes = table.Column<string>(type: "TEXT", nullable: true),
                    NextAnnouncementType = table.Column<int>(type: "INTEGER", nullable: true),
                    NextAnnouncement = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAnnouncement = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TravelMiles = table.Column<double>(type: "REAL", nullable: true),
                    TravelMinutes = table.Column<double>(type: "REAL", nullable: true),
                    NextTravelTimeUpdate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AppointmentId = table.Column<string>(type: "TEXT", nullable: false)
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentReminders");

            migrationBuilder.DropTable(
                name: "Appointments");
        }
    }
}
