using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UiModuleCode = table.Column<string>(type: "varchar(50)", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 180),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAdmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Admin"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TwoFactorSecret = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsTwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAdmins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentSessionToken = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastLoginIp = table.Column<string>(type: "varchar(50)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminRefreshTokens_SystemAdmins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "SystemAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotActivityLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BotId = table.Column<int>(type: "int", nullable: true),
                    LogLevel = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "INFO"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotActivityLogs_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BotActivityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConnectedAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastStatusUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectedAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectedAccounts_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConnectedAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "varchar(100)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TargetGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotId = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    GroupUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    LastCursor = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetGroups_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TargetGroups_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "USD"),
                    PaymentMethod = table.Column<string>(type: "varchar(50)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserBots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotId = table.Column<int>(type: "int", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBots_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyPitch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DailyMessageLimit = table.Column<int>(type: "int", nullable: false, defaultValue: 50),
                    AIApiKeyEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScraperApiTokenEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AIApiKeyExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "DATEADD(day, 7, GETUTCDATE())"),
                    ScraperApiTokenExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "DATEADD(day, 7, GETUTCDATE())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectedAccountCookies",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    EncryptedCookies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CookiesExpireDate = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectedAccountCookies", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_ConnectedAccountCookies_ConnectedAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ConnectedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Scrapes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotId = table.Column<int>(type: "int", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    CollectedLeadsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InfoJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    StartedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EndedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", nullable: false, defaultValue: "Running")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scrapes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scrapes_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Scrapes_ConnectedAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ConnectedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Scrapes_TargetGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TargetGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Scrapes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BotId = table.Column<int>(type: "int", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    ScrapeId = table.Column<int>(type: "int", nullable: true),
                    ProfileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProfileUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AiMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetaDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_ConnectedAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ConnectedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Scrapes_ScrapeId",
                        column: x => x.ScrapeId,
                        principalTable: "Scrapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_TargetGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TargetGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminRefreshTokens_Token",
                table: "AdminRefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_AdminId_Active",
                table: "AdminRefreshTokens",
                column: "AdminId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BotActivityLogs_BotId",
                table: "BotActivityLogs",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_BotActivityLogs_CorrelationId_UserId",
                table: "BotActivityLogs",
                columns: new[] { "CorrelationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BotActivityLogs_UserId_BotId_CreatedAt_Id",
                table: "BotActivityLogs",
                columns: new[] { "UserId", "BotId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_BotId",
                table: "ConnectedAccounts",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_UserId_BotId",
                table: "ConnectedAccounts",
                columns: new[] { "UserId", "BotId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_UserId_Status",
                table: "ConnectedAccounts",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status",
                table: "Jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_UserId",
                table: "Jobs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AccountId",
                table: "Leads",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BotId",
                table: "Leads",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_GroupId",
                table: "Leads",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ProfileUrl",
                table: "Leads",
                column: "ProfileUrl");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ScrapeId",
                table: "Leads",
                column: "ScrapeId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_UserId_ExternalId",
                table: "Leads",
                columns: new[] { "UserId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_UserId_ScrapeId_Status",
                table: "Leads",
                columns: new[] { "UserId", "ScrapeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Scrape_UserId_BotId_Status",
                table: "Scrapes",
                columns: new[] { "UserId", "BotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Scrapes_AccountId",
                table: "Scrapes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Scrapes_BotId",
                table: "Scrapes",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_Scrapes_GroupId",
                table: "Scrapes",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAdmins_Email",
                table: "SystemAdmins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetGroups_BotId",
                table: "TargetGroups",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetGroups_UserId",
                table: "TargetGroups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBots_BotId",
                table: "UserBots",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBots_UserId",
                table: "UserBots",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRefreshTokens_Token",
                table: "UserRefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_UserId_Active",
                table: "UserRefreshTokens",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRefreshTokens");

            migrationBuilder.DropTable(
                name: "BotActivityLogs");

            migrationBuilder.DropTable(
                name: "ConnectedAccountCookies");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "UserBots");

            migrationBuilder.DropTable(
                name: "UserRefreshTokens");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "SystemAdmins");

            migrationBuilder.DropTable(
                name: "Scrapes");

            migrationBuilder.DropTable(
                name: "ConnectedAccounts");

            migrationBuilder.DropTable(
                name: "TargetGroups");

            migrationBuilder.DropTable(
                name: "Bots");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
