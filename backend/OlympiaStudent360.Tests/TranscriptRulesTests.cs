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

    [Fact]
    public void GpaIncludesGymFineArtsAndChoirAndUsesAvailableSemesterOneMarks()
    {
        TranscriptSubjectDto[] subjects = [
            new() { SubjectCode = "math", Semester1Final = 95, YearAverage = 95 },
            new() { SubjectCode = "gym", Semester1Final = 75, YearAverage = 75, Credit = .5 },
            new() { SubjectCode = "fine-arts", Semester1Final = 80, YearAverage = 80 },
            new() { SubjectCode = "choir", Semester1Final = 0, YearAverage = 60 },
            new() { SubjectCode = "local-education", Semester1Final = 100 },
            new() { SubjectCode = "chemistry", Credit = 0 }];
        Assert.Equal(3.39, TranscriptRules.AnnualGpa(subjects));
        Assert.Equal(2.93, TranscriptRules.Semester1Gpa(subjects));
        subjects[3].Semester1Final = subjects[3].YearAverage = null;
        Assert.Null(TranscriptRules.AnnualGpa(subjects));
        Assert.Equal(3.82, TranscriptRules.Semester1Gpa(subjects));
        Assert.Null(TranscriptRules.Semester1Gpa(subjects.Skip(4)));
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
    [InlineData("2526 IELTS Prep Course 11E1.1", "ielts", "IELTS Prep Course")]
    [InlineData("IELTS 11E1.1", "ielts", "IELTS Prep Course")]
    [InlineData("Hoạt động trải nghiệm, hướng nghiệp 11E1.1", "experiential-activities", "Experiential and Career Guidance Activities")]
    [InlineData("Nội dung giáo dục địa phương 11E1.1", "local-education", "Local Education")]
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
        year.Bilingual = new() { Academic = new() { FullYear = "Tốt/Good" }, LocalEducation = new() { Semester1 = "Pass" } };
        Assert.Null(TranscriptRules.Validate(record));
        year.Bilingual.Academic.FullYear = new string('a', 61);
        Assert.NotNull(TranscriptRules.Validate(record));
        year.Bilingual.Academic.FullYear = null;
        year.Bilingual.LocalEducation.Semester1 = "80";
        Assert.NotNull(TranscriptRules.Validate(record));
        year.Bilingual.LocalEducation.Semester1 = null;
        year.IbSubjects[0].Term1 = "95";
        Assert.NotNull(TranscriptRules.Validate(record));
        year.IbSubjects[0].Term1 = "6";
        year.Credits = [new() { SubjectCode = "math", Credit = 1 }, new() { SubjectCode = "MATH", Credit = .5 }];
        Assert.NotNull(TranscriptRules.Validate(record));
    }
}
