using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearningPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseDetailConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Courses_Difficulty_Values",
                table: "Courses",
                sql: "[Difficulty] IN ('Beginner', 'Intermediate', 'Advanced')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Courses_Price_NonNegative",
                table: "Courses",
                sql: "[Price] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Courses_Difficulty_Values",
                table: "Courses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Courses_Price_NonNegative",
                table: "Courses");
        }
    }
}
