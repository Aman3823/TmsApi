using TmsApi.Application.Grading;

namespace TmsApi.Tests;

public class GradingServiceTests
{
    // Step 3: Single Logic Test
    [Fact]
    public void CalculateLetterGrade_HighScore_ReturnsDistinction()
    {
        // Arrange
        var service = new GradingService();

        // Act
        var result = service.CalculateLetterGrade(score: 85m, maxScore: 100m);

        // Assert
        Assert.Equal(GradeLevel.Distinction, result);
    }

    [Fact]
    public void CalculateFromEnrollmentGrade_NullGrade_ReturnsInvalid()
    {
        // Arrange
        var service = new GradingService();

        // Act
        var result = service.CalculateFromEnrollmentGrade(null);

        // Assert
        Assert.Equal(GradeLevel.Invalid, result);
    }

    // Step 4: Parameterized Boundary Tests
    [Theory]
    [InlineData(0, 100, GradeLevel.Fail)]         // Boundary: zero score
    [InlineData(70, 100, GradeLevel.Distinction)] // Boundary: at distinction threshold
    [InlineData(50, 100, GradeLevel.Pass)]        // Boundary: at pass threshold
    [InlineData(-1, 100, GradeLevel.Invalid)]     // Boundary: negative score
    [InlineData(101, 100, GradeLevel.Invalid)]    // Boundary: score exceeds max
    [InlineData(50, 0, GradeLevel.Invalid)]       // Boundary: zero max score (undefined percentage)
    public void CalculateLetterGrade_VariousInputs_ReturnsExpectedLevel(
        decimal score, 
        decimal maxScore, 
        GradeLevel expected)
    {
        // Arrange
        var service = new GradingService();

        // Act
        var result = service.CalculateLetterGrade(score, maxScore);

        // Assert
        Assert.Equal(expected, result);
    }
}