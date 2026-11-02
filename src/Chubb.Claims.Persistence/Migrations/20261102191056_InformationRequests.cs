using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chubb.Claims.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InformationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimInformationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimInformationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimInformationRequests_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimInformationRequests_ClaimId_RequestedAtUtc",
                table: "ClaimInformationRequests",
                columns: new[] { "ClaimId", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimInformationRequests");
        }
    }
}
