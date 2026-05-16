using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VolleySquad.Api.Migrations
{
    /// <inheritdoc />
    public partial class updatecolumn2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RelatedMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchActivities_EventType",
                table: "MatchActivities",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_MatchActivities_OccurredAt",
                table: "MatchActivities",
                column: "OccurredAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MatchActivities_RelatedMemberId",
                table: "MatchActivities",
                column: "RelatedMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchActivities");
        }
    }
}
