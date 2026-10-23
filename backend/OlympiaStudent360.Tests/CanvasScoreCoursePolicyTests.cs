using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class CanvasScoreCoursePolicyTests
{
    [Theory]
    [InlineData("2627 Tiếng Anh/English ESL 9E1.1", "2627-EnglishESL9E1.1", true, true)]
    [InlineData("2627 Tiếng Anh/English ELA 9E1.1", "2627-EnglishELA9E1.1", true, false)]
    [InlineData("English esl 5H1", null, false, true)]
    [InlineData(null, "2627-EnglishESL10E1.1", true, true)]
    [InlineData(null, "2627-EnglishELA10E1.1", false, false)]
    [InlineData("ESL_5H1", null, true, true)]
    [InlineData("ELA_5H1", null, false, false)]
    [InlineData("English ESL / ELA", null, true, false)]
    [InlineData("English ESL 5H1", "2627-EnglishELA5H1", true, false)]
    [InlineData("2627 Tiếng Anh/English OIDP1", null, true, false)]
    [InlineData("2627 OIS Tiếng Anh/English 5H4", null, true, false)]
    [InlineData("2627 Nhân văn/Humanities 9H", "2627-Humanities9H", true, false)]
    [InlineData("2627 Nhân văn/Humanities 9H", "2627-Humanities9H", false, true)]
    [InlineData("2627 IELTS Prep G10", null, true, false)]
    [InlineData("2627 Toán/Math 5H1", "2627-Math5H1", false, true)]
    [InlineData("Elasticity", "Elasticity5", false, true)]
    [InlineData("ESLasticity", null, false, true)]
    [InlineData(null, null, false, true)]
    [InlineData(null, null, true, false)]
    public void IncludesOnlyExplicitEslCoursesForEnglish(
        string? name, string? code, bool englishSubject, bool expected)
    {
        Assert.Equal(expected, CanvasScoreCoursePolicy.Include(name, code, englishSubject, ["ESL"]));
    }

    [Theory]
    [InlineData("ESL", true, false)]
    [InlineData("ELA", false, true)]
    [InlineData("ESL,ELA", true, true)]
    [InlineData("", true, true)]
    public void ProgrammeSelectionIncludesEslElaOrBoth(string selection, bool esl, bool ela)
    {
        var programmes = selection.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(esl, CanvasScoreCoursePolicy.Include("English ESL 5H1", "EnglishESL5H1", true, programmes));
        Assert.Equal(ela, CanvasScoreCoursePolicy.Include("English ELA 5H1", "EnglishELA5H1", true, programmes));
        Assert.True(CanvasScoreCoursePolicy.Include("Math 5H1", null, false, programmes));
        Assert.False(CanvasScoreCoursePolicy.Include("English OIDP1", null, true, programmes));
        Assert.False(CanvasScoreCoursePolicy.Include("English ESL / ELA", null, true, programmes));
    }

    [Fact]
    public void EnglishAccountScopeIncludesDescendantsButNotOtherSubjects()
    {
        (long, long?, string?)[] accounts =
        [
            (1, null, "School"), (20, 1, "G5_English/Tiếng Anh"), (21, 20, "Electives"),
            (22, 21, "Classes"), (30, 1, "G5_Math/Toán"), (40, 1, "Tiếng Anh/ English"),
            (50, 51, "English Department"), (51, 50, "Cycle")
        ];
        Assert.Equal(new long[] { 20, 21, 22, 40, 50, 51 },
            CanvasScoreCoursePolicy.EnglishAccountIds(accounts).Order());
    }
}
