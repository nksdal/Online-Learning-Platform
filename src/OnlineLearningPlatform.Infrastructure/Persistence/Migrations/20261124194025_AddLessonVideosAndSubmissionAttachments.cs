using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearningPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonVideosAndSubmissionAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoPublicId",
                table: "Lessons",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Lessons",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "AssignmentSubmissions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "AssignmentSubmissions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPublicId",
                table: "AssignmentSubmissions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSizeBytes",
                table: "AssignmentSubmissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "AssignmentSubmissions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoPublicId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "AttachmentPublicId",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "AttachmentSizeBytes",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "AssignmentSubmissions");
        }
    }
}
