using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VolleySquad.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordAuthHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string bootstrapPasswordHash = "pbkdf2-sha256.100000.+mNJW9QbS5A+1KLGx67R8A==.trfRqiVDxfrHaUN2xfxDz/np+n22RVjUke/6GK4n+Hw=";

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SlotTransfers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);

            // Backfill dữ liệu cũ để account hiện tại không bị lock ngay sau deploy.
            // Tạm thời tất cả account cũ dùng chung password bootstrap: Volley@123
            // Sau lần login đầu tiên, user nên gọi /api/auth/change-password để đổi ngay.
            migrationBuilder.Sql($"UPDATE Members SET PasswordHash = '{bootstrapPasswordHash}' WHERE PasswordHash IS NULL");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Matches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Upcoming");

            migrationBuilder.CreateIndex(
                name: "IX_SlotTransfers_FromMemberId",
                table: "SlotTransfers",
                column: "FromMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotTransfers_MatchId_Status",
                table: "SlotTransfers",
                columns: new[] { "MatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SlotTransfers_Status",
                table: "SlotTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SlotTransfers_ToMemberId",
                table: "SlotTransfers",
                column: "ToMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_SlotTransfers_Matches_MatchId",
                table: "SlotTransfers",
                column: "MatchId",
                principalTable: "Matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SlotTransfers_Members_FromMemberId",
                table: "SlotTransfers",
                column: "FromMemberId",
                principalTable: "Members",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SlotTransfers_Members_ToMemberId",
                table: "SlotTransfers",
                column: "ToMemberId",
                principalTable: "Members",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SlotTransfers_Matches_MatchId",
                table: "SlotTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_SlotTransfers_Members_FromMemberId",
                table: "SlotTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_SlotTransfers_Members_ToMemberId",
                table: "SlotTransfers");

            migrationBuilder.DropIndex(
                name: "IX_SlotTransfers_FromMemberId",
                table: "SlotTransfers");

            migrationBuilder.DropIndex(
                name: "IX_SlotTransfers_MatchId_Status",
                table: "SlotTransfers");

            migrationBuilder.DropIndex(
                name: "IX_SlotTransfers_Status",
                table: "SlotTransfers");

            migrationBuilder.DropIndex(
                name: "IX_SlotTransfers_ToMemberId",
                table: "SlotTransfers");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Matches");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SlotTransfers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
