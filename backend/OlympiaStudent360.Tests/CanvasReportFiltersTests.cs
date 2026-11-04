using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class CanvasReportFiltersTests
{
    [Theory]
    [InlineData("GHK1", "HS2")]
    [InlineData("GHK2", "HS2")]
    [InlineData(" ghk1 ", "HS2")]
    [InlineData(" GhK2 ", "HS2")]
    [InlineData("HK1-HS1.1", "HS1")]
    [InlineData("HK2-HS2", "HS2")]
    [InlineData("Hệ số 3", "HS3")]
    [InlineData("GPA", "GPA")]
    [InlineData("GHK10", null)]
    [InlineData("GHK20", null)]
    [InlineData("GHK3", null)]
    [InlineData("Other GHK1 group", null)]
    [InlineData("HS10", null)]
    [InlineData("HS1-HS2", null)]
    [InlineData("GPA-HS2", null)]
    [InlineData(null, null)]
    public void AssessmentCategoryUsesExplicitGroupLabels(string? group, string? expected)
    {
        Assert.Equal(expected, CanvasReportFilters.CategoryForGroup(group));
    }

    [Theory]
    [InlineData("HK1-HS1.1", "HS1.1")]
    [InlineData("HK2-HS1.2", "HS1.2")]
    [InlineData("Hệ số 1.3", "HS1.3")]
    [InlineData(" HS1.4 (quiz) ", "HS1.4")]
    [InlineData("HS1.10", null)]
    [InlineData("HS1.5", null)]
    [InlineData("HS1.1.2", null)]
    [InlineData("HS1.1-HS1.2", null)]
    [InlineData("HS1.1-HS1.10", null)]
    [InlineData("HS1.1-HS2", null)]
    [InlineData("HS1", null)]
    public void IndividualAssessmentsKeepTheirNumbersAndRejectAmbiguousLabels(string group, string? expected)
    {
        Assert.Equal(expected, CanvasReportFilters.AssessmentForGroup(group));
        Assert.Equal(expected == "HS1.1", CanvasReportFilters.MatchesCategory(group, "HS1.1"));
    }
}
