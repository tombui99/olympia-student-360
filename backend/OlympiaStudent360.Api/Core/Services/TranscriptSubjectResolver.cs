using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Explicit Canvas-to-subject mappings used by transcript reporting. Dictionary values may be a
/// canonical subject code (for example, "math"), a known alias, or a display name for a custom
/// subject.
/// </summary>
public sealed class TranscriptSubjectOptions
{
    public Dictionary<long, string> CanvasCourseIds { get; set; } = [];
    public Dictionary<string, string> SisCourseIds { get; set; } = [];
    public Dictionary<string, string> CourseCodes { get; set; } = [];
}

/// <summary>A reporting subject resolved without changing the source Canvas course.</summary>
public sealed record TranscriptSubjectResolution(
    string Code,
    string Name);

/// <summary>
/// Resolves Canvas course identities into stable transcript subjects. Explicit configuration takes
/// precedence over the conservative bilingual alias catalog. An unrecognized course receives a
/// course-specific code so unrelated Canvas courses can never be merged merely because their names
/// happen to match.
/// </summary>
public sealed partial class TranscriptSubjectResolver
{
    private sealed record CatalogSubject(
        string Code,
        string Name,
        int SortOrder,
        params string[] Aliases);

    private sealed record SubjectAlias(
        string Value,
        CatalogSubject Subject);

    private static readonly CatalogSubject[] Catalog =
    [
        new("math", "Math", 10,
            "math", "maths", "mathematics", "toán", "toan", "môn toán", "mon toan",
            "ib mathematics", "igcse mathematics"),
        new("physics", "Physics", 20,
            "physics", "vật lý", "vật lí", "vat ly", "vat li"),
        new("chemistry", "Chemistry", 30,
            "chemistry", "hóa học", "hoá học", "hoa hoc"),
        new("biology", "Biology", 40,
            "biology", "sinh học", "sinh hoc"),
        new("science", "Science", 50,
            "science", "natural science", "natural sciences", "integrated science",
            "khoa học", "khoa hoc", "khoa học tự nhiên", "khoa hoc tu nhien"),
        new("history-and-geography", "History and Geography", 55,
            "history and geography", "history & geography", "lịch sử và địa lý",
            "lịch sử và địa lí", "lich su va dia ly", "lich su va dia li"),
        new("literature", "Literature", 60,
            "literature", "ngữ văn", "ngu van"),
        new("vietnamese", "Vietnamese", 70,
            "vietnamese", "tiếng việt", "tieng viet"),
        new("history", "History", 80,
            "history", "lịch sử", "lich su"),
        new("geography", "Geography", 90,
            "geography", "địa lý", "địa lí", "dia ly", "dia li"),
        new("english", "English", 100,
            "english", "english language", "tiếng anh", "tieng anh"),
        new("chinese", "Chinese", 110,
            "chinese", "mandarin", "tiếng trung", "tieng trung", "trung văn", "trung van"),
        new("french", "French", 120,
            "french", "tiếng pháp", "tieng phap"),
        new("spanish", "Spanish", 130,
            "spanish", "tiếng tây ban nha", "tieng tay ban nha"),
        new("national-defense-education", "National Defense Education", 140,
            "national defense education", "national defence education", "gdqp-an", "gdqp an",
            "giáo dục quốc phòng và an ninh", "giao duc quoc phong va an ninh",
            "giáo dục quốc phòng an ninh", "giao duc quoc phong an ninh"),
        new("economics-and-law", "Economics and Law", 150,
            "economics and law", "economics & law", "kinh tế và pháp luật",
            "kinh te va phap luat", "kinh tế & pháp luật", "kinh te & phap luat"),
        new("economics", "Economics", 160,
            "economics", "kinh tế", "kinh te"),
        new("business", "Business", 170,
            "business", "business studies", "business management", "kinh doanh",
            "quản trị kinh doanh", "quan tri kinh doanh"),
        new("technology", "Technology", 180,
            "technology", "công nghệ", "cong nghe"),
        new("ict", "ICT", 190,
            "ict", "information and communication technology", "tin học", "tin hoc"),
        new("computer-science", "Computer Science", 200,
            "computer science", "khoa học máy tính", "khoa hoc may tinh"),
        new("global-perspectives", "Global Perspectives", 210,
            "global perspectives", "global perspective"),
        new("humanities", "Humanities", 215,
            "humanities", "nhân văn", "nhan van"),
        new("music", "Music", 220,
            "music", "âm nhạc", "am nhac"),
        new("fine-arts", "Fine Arts", 230,
            "fine arts", "visual arts", "art", "mỹ thuật", "my thuat", "mĩ thuật", "mi thuat"),
        new("physical-education", "Physical Education", 240,
            "physical education", "pe", "giáo dục thể chất", "giao duc the chat", "gdtc",
            "thể dục", "the duc"),
        new("civic-education", "Civic Education", 245,
            "civic education", "giáo dục công dân", "giao duc cong dan", "gdcd"),
        new("service-learning", "Service Learning", 250,
            "service learning", "community service", "research projects and service learning",
            "dự án nghiên cứu và phục vụ cộng đồng", "du an nghien cuu va phuc vu cong dong"),
        new("cas", "CAS", 260,
            "cas", "creativity activity service"),
        new("cas-and-extended-essay", "CAS and Extended Essay", 262,
            "hoạt động cas và ee", "hoat dong cas va ee",
            "creativity & activity & service & extendedessay",
            "creativity activity service extendedessay"),
        new("theory-of-knowledge", "Theory of Knowledge", 264,
            "theory of knowledge", "lý luận tri thức", "ly luan tri thuc"),
        new("personal-development", "Personal Development", 265,
            "personal development", "personal development program", "life phát triển con người",
            "life phat trien con nguoi"),
        new("entrepreneurship", "Entrepreneurship", 270,
            "entrepreneurship", "khởi nghiệp", "khoi nghiep"),
        new("critical-thinking", "Critical Thinking", 275,
            "critical thinking", "tư duy phản biện", "tu duy phan bien"),
        new("travel-and-tourism", "Travel and Tourism", 280,
            "travel and tourism", "travel & tourism"),
        new("basketball", "Basketball", 285,
            "basketball", "bóng rổ", "bong ro"),
        new("soccer", "Soccer", 287,
            "soccer", "football", "bóng đá", "bong da"),
        new("volleyball", "Volleyball", 290,
            "volleyball", "bóng chuyền", "bong chuyen"),
        new("gym", "Gym", 295,
            "gym", "gymnastics"),
        new("piano", "Piano", 300,
            "piano", "dương cầm", "duong cam"),
        new("guitar", "Guitar", 301,
            "guitar", "ghi ta"),
        new("drums", "Drums", 302,
            "drums", "drum", "trống", "trong"),
        new("traditional-musical-instruments", "Traditional Musical Instruments", 303,
            "traditional musical instruments", "nhạc cụ dân tộc", "nhac cu dan toc"),
        new("vocal", "Vocal", 304,
            "vocal", "thanh nhạc", "thanh nhac"),
        new("performing-arts", "Performing Arts", 305,
            "performing arts", "nghệ thuật tổng hợp", "nghe thuat tong hop"),
        new("psat", "PSAT", 310,
            "psat"),
        new("sat", "SAT", 315,
            "sat", "sat prep", "sat prep course"),
        new("ielts", "IELTS", 320,
            "ielts", "ielts prep", "ielts prep course"),
        new("global-studies", "Global Studies", 330,
            "global studies", "quốc tế học", "quoc te hoc")
    ];

    private static readonly IReadOnlyDictionary<string, CatalogSubject> SubjectsByCode = Catalog
        .ToDictionary(subject => subject.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly SubjectAlias[] Aliases = Catalog
        .SelectMany(subject => subject.Aliases
            .Append(subject.Code)
            .Append(subject.Name)
            .Select(alias => new SubjectAlias(NormalizeForMatching(alias), subject)))
        .Where(alias => alias.Value.Length > 0)
        .GroupBy(alias => alias.Value, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.OrderBy(item => item.Subject.SortOrder).First())
        .OrderByDescending(alias => alias.Value.Length)
        .ThenBy(alias => alias.Subject.SortOrder)
        .ToArray();

    private readonly IReadOnlyDictionary<long, string> _canvasCourseIds;
    private readonly IReadOnlyDictionary<string, string> _sisCourseIds;
    private readonly IReadOnlyDictionary<string, string> _courseCodes;

    public TranscriptSubjectResolver(IOptions<TranscriptSubjectOptions> options)
    {
        _canvasCourseIds = Clean(options.Value.CanvasCourseIds);
        _sisCourseIds = Clean(options.Value.SisCourseIds);
        _courseCodes = Clean(options.Value.CourseCodes);
    }

    public TranscriptSubjectResolution Resolve(
        long canvasCourseId,
        string? sisCourseId,
        string? courseCode,
        string? courseName)
    {
        if (_canvasCourseIds.TryGetValue(canvasCourseId, out var configuredSubject))
        {
            return ResolveConfigured(configuredSubject);
        }

        if (TryConfigured(_sisCourseIds, sisCourseId, out configuredSubject))
        {
            return ResolveConfigured(configuredSubject);
        }

        if (TryConfigured(_courseCodes, courseCode, out configuredSubject))
        {
            return ResolveConfigured(configuredSubject);
        }

        if (TryResolveKnown(courseName, out var knownSubject) ||
            TryResolveKnown(courseCode, out knownSubject))
        {
            return ToResolution(knownSubject);
        }

        var fallbackName = FirstMeaningful(courseName, courseCode, sisCourseId)
            ?? $"Canvas course {canvasCourseId}";
        return new TranscriptSubjectResolution(
            $"canvas-course-{canvasCourseId}",
            fallbackName);
    }

    public int SortOrder(string? subjectCode)
    {
        if (string.IsNullOrWhiteSpace(subjectCode)) return int.MaxValue;
        var code = subjectCode.Trim();
        if (SubjectsByCode.TryGetValue(code, out var subject)) return subject.SortOrder;
        return code.StartsWith("canvas-course-", StringComparison.OrdinalIgnoreCase)
            ? int.MaxValue
            : int.MaxValue - 1;
    }

    private static TranscriptSubjectResolution ResolveConfigured(string configuredSubject)
    {
        var value = configuredSubject.Trim();
        if (SubjectsByCode.TryGetValue(value, out var byCode) ||
            TryResolveKnownExact(value, out byCode))
        {
            return ToResolution(byCode);
        }

        return new TranscriptSubjectResolution(
            Slug(value),
            value);
    }

    private static TranscriptSubjectResolution ToResolution(CatalogSubject subject) =>
        new(subject.Code, subject.Name);

    private static bool TryResolveKnown(string? source, out CatalogSubject subject)
    {
        subject = default!;
        if (string.IsNullOrWhiteSpace(source)) return false;

        var withoutPrefix = source;
        for (var index = 0; index < 2; index++)
        {
            withoutPrefix = LeadingAcademicYearPattern().Replace(withoutPrefix, "");
            withoutPrefix = LeadingOrganizationPattern().Replace(withoutPrefix, "");
        }

        // Olympia's bilingual course convention puts the Vietnamese and English subject names on
        // either side of '/'. Matching the complete first segment avoids classifying unrelated
        // titles such as "English Literature" or "Art History" by their first word.
        var separatorIndex = withoutPrefix.IndexOf('/');
        if (separatorIndex >= 0 &&
            TryResolveKnownExact(withoutPrefix[..separatorIndex], out subject))
        {
            return true;
        }

        var normalized = NormalizeForMatching(withoutPrefix);
        if (normalized.Length == 0) return false;

        if (TryResolveNormalizedExact(normalized, out subject)) return true;

        foreach (var alias in Aliases)
        {
            if (normalized.StartsWith(alias.Value + " ", StringComparison.OrdinalIgnoreCase) &&
                ClassSuffixPattern().IsMatch(normalized[(alias.Value.Length + 1)..]))
            {
                subject = alias.Subject;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveKnownExact(string? source, out CatalogSubject subject)
    {
        subject = default!;
        return !string.IsNullOrWhiteSpace(source) &&
            TryResolveNormalizedExact(NormalizeForMatching(source), out subject);
    }

    private static bool TryResolveNormalizedExact(string normalized, out CatalogSubject subject)
    {
        subject = default!;
        var alias = Aliases.FirstOrDefault(item =>
            normalized.Equals(item.Value, StringComparison.OrdinalIgnoreCase));
        if (alias is null) return false;
        subject = alias.Subject;
        return true;
    }

    private static bool TryConfigured(
        IReadOnlyDictionary<string, string> mappings,
        string? key,
        out string configuredSubject)
    {
        configuredSubject = default!;
        if (string.IsNullOrWhiteSpace(key) ||
            !mappings.TryGetValue(key.Trim(), out var configured))
        {
            return false;
        }

        configuredSubject = configured;
        return true;
    }

    private static IReadOnlyDictionary<long, string> Clean(Dictionary<long, string>? values)
    {
        var result = new Dictionary<long, string>();
        foreach (var (key, value) in values ?? [])
        {
            if (!string.IsNullOrWhiteSpace(value)) result[key] = value.Trim();
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> Clean(Dictionary<string, string>? values)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                result[key.Trim()] = value.Trim();
            }
        }
        return result;
    }

    private static string? FirstMeaningful(params string?[] values) => values
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?.Trim();

    private static string Slug(string value)
    {
        var normalized = NormalizeForMatching(value);
        return normalized.Length == 0
            ? "configured-subject"
            : normalized.Replace(' ', '-');
    }

    private static string NormalizeForMatching(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalizedCharacter = character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(normalizedCharacter))
            {
                if (pendingSpace && result.Length > 0) result.Append(' ');
                result.Append(normalizedCharacter);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = result.Length > 0;
            }
        }

        return result.ToString();
    }

    [GeneratedRegex(
        @"^\s*(?:(?:19|20)\d{2}\s*[-\u2013\u2014/]\s*(?:19|20)\d{2}|\d{4})(?:\s*[-_:]\s*|\s+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingAcademicYearPattern();

    [GeneratedRegex(@"^\s*(?:OIS)\b(?:\s*[-_:]\s*|\s+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingOrganizationPattern();

    [GeneratedRegex(@"^(?:(?:grade|class|g|esl|ela)\s*)?\d{1,2}(?:\s*[a-z]{1,3}\d*)?(?:\s+\d+)*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassSuffixPattern();
}
