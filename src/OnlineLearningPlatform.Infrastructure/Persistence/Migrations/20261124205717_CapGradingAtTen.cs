using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearningPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CapGradingAtTen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing grades before lowering their assignments' maximum scores so each
            // learner keeps the same percentage (for example, 10/100 becomes 1/10).
            migrationBuilder.Sql("""
                UPDATE submission
                SET [Score] = ROUND(submission.[Score] * 10.0 / assignment.[MaxScore], 2)
                FROM [AssignmentSubmissions] AS submission
                INNER JOIN [Assignments] AS assignment ON assignment.[Id] = submission.[AssignmentId]
                WHERE submission.[Score] IS NOT NULL AND assignment.[MaxScore] > 10.0;

                UPDATE [Assignments]
                SET [MaxScore] = 10.0
                WHERE [MaxScore] > 10.0;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentSubmissions_Score_Range",
                table: "AssignmentSubmissions",
                sql: "[Score] IS NULL OR ([Score] >= 0 AND [Score] <= 10.0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assignments_MaxScore_Range",
                table: "Assignments",
                sql: "[MaxScore] > 0 AND [MaxScore] <= 10.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the normalized values when rolling code back; restoring old maxima could
            // inflate a student's grade or discard percentage-preserving score conversions.
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentSubmissions_Score_Range",
                table: "AssignmentSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assignments_MaxScore_Range",
                table: "Assignments");
        }
    }
}
