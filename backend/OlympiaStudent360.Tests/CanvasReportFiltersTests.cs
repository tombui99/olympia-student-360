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
}
