using Microsoft.Extensions.Options;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class TranscriptRulesTests
{
    [Fact]
    public void SemesterOneDoesNotBecomeAnAnnualResultOrGpa()
    {
        Assert.Null(TranscriptRules.AnnualAverage(100, null, null));
        Assert.Null(TranscriptRules.AnnualGpa([new() { SubjectCode = "math", Semester1Final = 100, Credit = 1 }]));
        Assert.Equal(83.3, TranscriptRules.AnnualAverage(70, 90, null));
        Assert.Equal(83.3, TranscriptRules.AnnualAverage(70, 90, 86));
        Assert.Equal(86, TranscriptRules.AnnualAverage(70, null, 86));
    }

    [Fact]
    public void AnnualGpaRequiresCompleteAcademicSubjectsAndOmitsPassFailActivities()
    {
        TranscriptSubjectDto[] subjects = [
            new() { SubjectCode = "math", YearAverage = 95, Credit = 1 },
            new() { SubjectCode = "english", YearAverage = 80, Credit = .5 },
            new() { SubjectCode = "soccer", YearAverage = 20, Credit = 1 }];
        Assert.Equal(4.03, TranscriptRules.AnnualGpa(subjects));
        subjects[1].YearAverage = null;
        Assert.Null(TranscriptRules.AnnualGpa(subjects));
        Assert.Null(TranscriptRules.AnnualGpa([subjects[2]]));
    }

    [Theory]
    [InlineData("2526 Academic English 11E1.1", "12", "11")]
    [InlineData("2627 Academic English 12E1.1", "12", "12")]
    [InlineData("Math", "10", "10")]
    [InlineData("Math", null, null)]
    public void HistoricalGradeUsesClassEvidenceAndNeverTheCurrentStudentGrade(string name, string? courseGrade, string? expected) =>
        Assert.Equal(expected, TranscriptRules.HistoricalGrade(name, null, courseGrade));

    [Theory]
    [InlineData("Phan Anh", "Anh Phan")]
    [InlineData("Vũ Minh Châu", "Chau Minh Vu")]
    [InlineData("Thái Vũ Minh Châu", "Chau Vu Minh Thai")]
    public void NamesFollowTheRequestedOrder(string source, string expected) => Assert.Equal(expected, TranscriptRules.InternationalName(source));

    [Fact]
    public void LiveCourseTotalIsNotAnOfficialAnnualMark()
    {
        var now = new DateTime(2026, 11, 2);
        Assert.False(TranscriptRules.AnnualTotalAvailable("2026-2027", null, now));
        Assert.True(TranscriptRules.AnnualTotalAvailable("2025-2026", null, now));
        Assert.False(TranscriptRules.AnnualTotalAvailable("2025-2026", new DateTime(2027, 1, 1), now));
    }

    [Theory]
    [InlineData("2526 Academic English/ Tiếng Anh học thuật 11E1.1", "academic-english", "Academic English")]
    [InlineData("2627 Hợp xướng/Choir 12E1.1", "choir", "Choir")]
    [InlineData("Thiết kế đồ họa/Graphic Design 11E1.1", "graphic-design", "Graphic Design")]
    public void SubjectNamesKeepTheirMeaningWithoutClassSuffixes(string course, string code, string english)
    {
        var resolver = new TranscriptSubjectResolver(Options.Create(new TranscriptSubjectOptions()));
        var subject = resolver.Resolve(1, null, null, course);
        Assert.Equal(code, subject.Code);
        Assert.Equal(english, TranscriptSubjectResolver.DisplayNames(subject.Name).English);
        Assert.DoesNotContain("11E1.1", subject.Name);
    }

    [Fact]
    public void TranscriptEntriesRejectInvalidGradesDuplicateSubjectsAndBadDates()
    {
        var record = new TranscriptRecordDto { EnrollmentDate = new(2026, 9, 1), GraduationDate = new(2026, 6, 1) };
        Assert.NotNull(TranscriptRules.Validate(record));
        record.GraduationDate = new(2027, 6, 1);
        var year = new TranscriptYearRecordDto { AcademicYear = "2026-2027", GradeLevel = "12",
            IbSubjects = [new() { SubjectCode = "math", SubjectName = "Math", Level = "HL", PredictedGrade = "7", Term1 = "6" }] };
        record.Years.Add(year);
        Assert.Null(TranscriptRules.Validate(record));
        year.IbSubjects[0].Term1 = "95";
        Assert.NotNull(TranscriptRules.Validate(record));
        year.IbSubjects[0].Term1 = "6";
        year.Credits = [new() { SubjectCode = "math", Credit = 1 }, new() { SubjectCode = "MATH", Credit = .5 }];
        Assert.NotNull(TranscriptRules.Validate(record));
    }
}
