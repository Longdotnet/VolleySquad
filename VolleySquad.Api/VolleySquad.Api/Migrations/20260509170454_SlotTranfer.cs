using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VolleySquad.Api.Migrations
{
    /// <inheritdoc />
    public partial class SlotTranfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm 2 cột mới vào bảng Matches đã tồn tại
            migrationBuilder.AddColumn<bool>(
                name: "IsSettled",
                table: "Matches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FeePerPerson",
                table: "Matches",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Tạo bảng mới SlotTransfers
            migrationBuilder.CreateTable(
                name: "SlotTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotTransfers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlotTransfers");

            migrationBuilder.DropColumn(
                name: "IsSettled",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "FeePerPerson",
                table: "Matches");
        }
    }
}
