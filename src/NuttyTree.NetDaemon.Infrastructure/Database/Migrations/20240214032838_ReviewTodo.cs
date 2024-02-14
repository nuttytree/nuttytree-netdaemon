using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuttyTree.NetDaemon.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReviewTodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewUid",
                table: "ToDoListItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewUid",
                table: "ToDoListItems");
        }
    }
}
