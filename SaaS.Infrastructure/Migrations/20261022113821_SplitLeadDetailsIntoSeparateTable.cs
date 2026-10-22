using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitLeadDetailsIntoSeparateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadDetails",
                columns: table => new
                {
                    LeadId = table.Column<long>(type: "bigint", nullable: false),
                    MetaDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadDetails", x => x.LeadId);
                    table.ForeignKey(
                        name: "FK_LeadDetails_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copy before dropping the source column. NULL means the lead has no details.
            // Dynamic SQL also lets idempotent scripts run after that column is gone.
            migrationBuilder.Sql("""
                EXEC(N'
                    INSERT INTO [LeadDetails] ([LeadId], [MetaDataJson])
                    SELECT [Id], [MetaDataJson]
                    FROM [Leads]
                    WHERE [MetaDataJson] IS NOT NULL;
                ');
                """);

            migrationBuilder.DropColumn(
                name: "MetaDataJson",
                table: "Leads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetaDataJson",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "{}");

            // Restore both metadata and the absence of details before removing the table.
            migrationBuilder.Sql("""
                EXEC(N'
                    UPDATE [lead]
                    SET [MetaDataJson] = [detail].[MetaDataJson]
                    FROM [Leads] AS [lead]
                    LEFT JOIN [LeadDetails] AS [detail] ON [lead].[Id] = [detail].[LeadId];
                ');
                """);

            migrationBuilder.DropTable(
                name: "LeadDetails");
        }
    }
}
