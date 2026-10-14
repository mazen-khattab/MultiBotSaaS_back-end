using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUserBotAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserBots_UserId",
                table: "UserBots");

            migrationBuilder.CreateIndex(
                name: "IX_UserBots_UserId_BotId",
                table: "UserBots",
                columns: new[] { "UserId", "BotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserBots_UserId_BotId",
                table: "UserBots");

            migrationBuilder.CreateIndex(
                name: "IX_UserBots_UserId",
                table: "UserBots",
                column: "UserId");
        }
    }
}
