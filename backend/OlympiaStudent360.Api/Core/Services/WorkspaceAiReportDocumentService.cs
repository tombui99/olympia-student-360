using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OlympiaStudent360.Api.Core.DTOs;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace OlympiaStudent360.Api.Core.Services;

public class WorkspaceAiReportDocumentService
{
    private const string Purple = "32124F";
    private const string Lavender = "E7DDEF";
    private const string Gold = "C09118";
    private const string DarkBlue = "17365D";
    private const string Muted = "6F6877";

    public byte[] Build(
        string workspaceTitle,
        string workspaceDescription,
        GenerateWorkspaceAiReportRequestDto request,
        WorkspaceAiNarrative narrative,
        string generatedBy)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            AddDocumentSettings(mainPart);
            AddStyles(mainPart);
            AddNumbering(mainPart);
            var headerRelationshipId = AddHeader(mainPart, request.Language);
            var footerRelationshipId = AddFooter(mainPart, request.Language);

            var body = mainPart.Document.Body!;
            body.Append(CenteredParagraph(narrative.Title, "ReportTitle"));
            body.Append(CenteredParagraph(workspaceTitle, "Subtitle"));
            if (!string.IsNullOrWhiteSpace(workspaceDescription))
            {
                body.Append(CenteredParagraph(workspaceDescription, "Metadata"));
            }

            var generatedLabel = request.Language == "en" ? "Generated" : "Ngày tạo";
            var authorLabel = request.Language == "en" ? "Prepared for" : "Người tạo báo cáo";
            AppendDefinitionRows(body,
            [
                (generatedLabel, DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                (authorLabel, generatedBy)
            ]);

            if (request.Filters.Count > 0)
            {
                body.Append(Paragraph(request.Language == "en" ? "Applied filters" : "Bộ lọc đã áp dụng", "Heading2"));
                AppendDefinitionRows(body, request.Filters.Select(filter => (filter.Label, filter.Value)));
            }

            body.Append(Paragraph(request.Language == "en" ? "Executive summary" : "Tóm tắt điều hành", "Heading1"));
            AppendText(body, narrative.ExecutiveSummary);

            var sectionById = narrative.Sections
                .Where(section => !string.IsNullOrWhiteSpace(section.ChartId))
                .GroupBy(section => section.ChartId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            uint imageId = 1;

            foreach (var chart in request.Charts)
            {
                sectionById.TryGetValue(chart.ChartId, out var section);
                body.Append(Paragraph(section?.Heading?.Trim() is { Length: > 0 } heading ? heading : chart.Title, "Heading1"));
                body.Append(Paragraph(
                    request.Language == "en"
                        ? $"Chart: {chart.Title} | Type: {chart.ChartType} | Source: {chart.Source}"
                        : $"Biểu đồ: {chart.Title} | Loại: {chart.ChartType} | Nguồn: {chart.Source}",
                    "Metadata",
                    keepWithNext: true));

                if (TryAddChartImage(mainPart, body, chart, imageId++))
                {
                    body.Append(Caption(
                        request.Language == "en"
                            ? $"Snapshot of {chart.Title} with the selected workspace filters"
                            : $"Ảnh chụp biểu đồ {chart.Title} theo bộ lọc đã chọn"));
                }

                if (section == null)
                {
                    body.Append(Paragraph(
                        request.Language == "en" ? "AI analysis was not available for this chart." : "Chưa có phân tích AI cho biểu đồ này.",
                        "Normal"));
                    continue;
                }

                AppendText(body, section.Analysis);
                AppendList(body,
                    request.Language == "en" ? "Key findings" : "Phát hiện chính",
                    section.KeyFindings);
                if (request.IncludeRecommendations)
                {
                    AppendList(body,
                        request.Language == "en" ? "Suggested actions" : "Hành động đề xuất",
                        section.Recommendations);
                }
            }

            if (request.IncludeRecommendations && narrative.OverallRecommendations.Count > 0)
            {
                AppendList(body,
                    request.Language == "en" ? "Cross-chart priorities" : "Ưu tiên tổng hợp",
                    narrative.OverallRecommendations,
                    "Heading1");
            }

            if (narrative.Caveats.Count > 0)
            {
                AppendList(body,
                    request.Language == "en" ? "Data considerations" : "Lưu ý về dữ liệu",
                    narrative.Caveats,
                    "Heading1");
            }

            body.Append(Paragraph(
                request.Language == "en"
                    ? "This report was generated with Gemini from the aggregated charts visible in the workspace. Review findings alongside school context before taking action."
                    : "Báo cáo này được Gemini tạo từ dữ liệu tổng hợp hiển thị trong không gian làm việc. Cần xem xét các nhận định cùng bối cảnh thực tế của nhà trường trước khi hành động.",
                "Disclaimer"));

            body.Append(new SectionProperties(
                new HeaderReference { Type = HeaderFooterValues.Default, Id = headerRelationshipId },
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerRelationshipId },
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1700, Right = 1020, Bottom = 1050, Left = 1020, Header = 260, Footer = 300 }));
            mainPart.Document.Save();
        }
        return stream.ToArray();
    }

    private static void AppendText(Body body, string? value)
    {
        var paragraphs = (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var text in paragraphs)
        {
            body.Append(Paragraph(text, "Normal"));
        }
    }

    private static void AppendList(Body body, string heading, IEnumerable<string> values, string headingStyle = "Heading2")
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        if (items.Count == 0) return;
        body.Append(Paragraph(heading, headingStyle));
        foreach (var item in items) body.Append(Bullet(item.Trim()));
    }

    private static Paragraph Paragraph(string text, string styleId, bool keepWithNext = false)
    {
        var properties = new ParagraphProperties(new ParagraphStyleId { Val = styleId });
        if (keepWithNext || styleId is "Heading1" or "Heading2" or "Heading3")
        {
            properties.Append(new KeepNext());
        }
        var paragraph = new Paragraph(properties);
        paragraph.Append(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        return paragraph;
    }

    private static Paragraph CenteredParagraph(string text, string styleId)
    {
        var paragraph = Paragraph(text, styleId);
        paragraph.ParagraphProperties!.Append(new Justification { Val = JustificationValues.Center });
        return paragraph;
    }

    private static void AppendDefinitionRows(Body body, IEnumerable<(string Label, string Value)> rows)
    {
        foreach (var (label, value) in rows)
        {
            var properties = new ParagraphProperties(
                new Shading { Fill = Lavender, Val = ShadingPatternValues.Clear },
                new ParagraphBorders(
                    new BottomBorder { Val = BorderValues.Single, Color = "FFFFFF", Size = 6 }),
                new Tabs(new TabStop { Val = TabStopValues.Left, Position = 2700 }),
                new Indentation { Left = "120", Right = "120" },
                new SpacingBetweenLines { Before = "90", After = "90", Line = "240", LineRule = LineSpacingRuleValues.Auto });
            var labelRun = new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Cambria", HighAnsi = "Cambria", EastAsia = "Cambria" },
                    new Bold(),
                    new Color { Val = Purple }),
                new Text(label) { Space = SpaceProcessingModeValues.Preserve },
                new TabChar());
            var valueRun = new Run(
                new RunProperties(new RunFonts { Ascii = "Cambria", HighAnsi = "Cambria", EastAsia = "Cambria" }),
                new Text(value) { Space = SpaceProcessingModeValues.Preserve });
            body.Append(new Paragraph(properties, labelRun, valueRun));
        }
    }

    private static Paragraph Bullet(string text)
    {
        var properties = new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 }));
        return new Paragraph(properties, new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph Caption(string text)
    {
        var properties = new ParagraphProperties(
            new ParagraphStyleId { Val = "Metadata" },
            new Justification { Val = JustificationValues.Center });
        return new Paragraph(properties, new Run(new RunProperties(new Italic()), new Text(text)));
    }

    private static bool TryAddChartImage(
        MainDocumentPart mainPart,
        Body body,
        WorkspaceAiChartDto chart,
        uint imageId)
    {
        const string prefix = "data:image/png;base64,";
        if (string.IsNullOrWhiteSpace(chart.ImageDataUrl) ||
            !chart.ImageDataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(chart.ImageDataUrl[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!TryReadPngSize(bytes, out var pixelWidth, out var pixelHeight)) return false;

        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var imageStream = new MemoryStream(bytes)) imagePart.FeedData(imageStream);
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        const long maxWidth = 6_350_000L; // Approximately 6.95 inches on A4.
        const long maxHeight = 3_474_720L;
        const double emusPerPixelAt96Dpi = 9_525d;
        var scale = Math.Min(emusPerPixelAt96Dpi,
            Math.Min((double)maxWidth / pixelWidth, (double)maxHeight / pixelHeight));
        var width = (long)Math.Round(pixelWidth * scale);
        var height = (long)Math.Round(pixelHeight * scale);

        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = width, Cy = height },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties
                {
                    Id = imageId,
                    Name = $"Chart snapshot {imageId}",
                    Description = chart.Title
                },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties
                                {
                                    Id = 0,
                                    Name = $"chart-{imageId}.png",
                                    Description = chart.Title
                                },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip
                                {
                                    Embed = relationshipId,
                                    CompressionState = A.BlipCompressionValues.Print
                                },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = width, Cy = height }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                {
                                    Preset = A.ShapeTypeValues.Rectangle
                                })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0,
                DistanceFromBottom = 0,
                DistanceFromLeft = 0,
                DistanceFromRight = 0
            });

        body.Append(new Paragraph(
            new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = "80", After = "60" },
                new Justification { Val = JustificationValues.Center }),
            new Run(drawing)));
        return true;
    }

    private static bool TryReadPngSize(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 24 ||
            bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
        {
            return false;
        }

        width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return width > 0 && height > 0;
    }

    private static void AddStyles(MainDocumentPart mainPart)
    {
        var part = mainPart.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(
            Style("Normal", "Normal", true, "Cambria", "21", "000000", "0", "100", "252"),
            Style("ReportTitle", "Report Title", false, "Cambria", "34", Purple, "100", "80", "252", true),
            Style("Subtitle", "Subtitle", false, "Cambria", "25", DarkBlue, "0", "70", "252", true),
            Style("Metadata", "Metadata", false, "Cambria", "18", Muted, "0", "70", "230"),
            Style("Heading1", "Heading 1", false, "Cambria", "27", Purple, "280", "120", "252", true),
            Style("Heading2", "Heading 2", false, "Cambria", "23", Purple, "200", "100", "252", true),
            Style("Heading3", "Heading 3", false, "Cambria", "21", DarkBlue, "140", "70", "252", true),
            Style("Disclaimer", "Disclaimer", false, "Cambria", "17", Muted, "220", "0", "230", false, true));
        part.Styles.Save();
    }

    private static void AddDocumentSettings(MainDocumentPart mainPart)
    {
        var part = mainPart.AddNewPart<DocumentSettingsPart>();
        part.Settings = new Settings(
            new Compatibility(
                new CompatibilitySetting
                {
                    Name = CompatSettingNameValues.CompatibilityMode,
                    Uri = "http://schemas.microsoft.com/office/word",
                    Val = "15"
                }),
            new DoNotUseHTMLParagraphAutoSpacing());
        part.Settings.Save();
    }

    private static Style Style(
        string id, string name, bool isDefault, string font, string size, string color,
        string before, string after, string line, bool bold = false, bool italic = false)
    {
        var runProperties = new StyleRunProperties(
            new RunFonts { Ascii = font, HighAnsi = font, EastAsia = font });
        if (bold) runProperties.Append(new Bold());
        if (italic) runProperties.Append(new Italic());
        runProperties.Append(
            new Color { Val = color },
            new FontSize { Val = size },
            new FontSizeComplexScript { Val = size });

        var style = new Style(new StyleName { Val = name })
        {
            Type = StyleValues.Paragraph,
            StyleId = id,
            Default = isDefault
        };
        if (id != "Normal") style.Append(new BasedOn { Val = "Normal" });
        style.Append(
            new NextParagraphStyle { Val = "Normal" },
            new StyleParagraphProperties(
                new SpacingBetweenLines { Before = before, After = after, Line = line, LineRule = LineSpacingRuleValues.Auto }),
            runProperties);
        return style;
    }

    private static void AddNumbering(MainDocumentPart mainPart)
    {
        var part = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var level = new Level(
            new NumberingFormat { Val = NumberFormatValues.Bullet },
            new LevelText { Val = "•" },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(
                new Tabs(new TabStop { Val = TabStopValues.Number, Position = 720 }),
                new Indentation { Left = "720", Hanging = "360" }))
        { LevelIndex = 0 };
        var abstractNumber = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }, level)
        { AbstractNumberId = 1 };
        part.Numbering = new Numbering(
            abstractNumber,
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
        part.Numbering.Save();
    }

    private static string AddHeader(MainDocumentPart mainPart, string language)
    {
        var headerPart = mainPart.AddNewPart<HeaderPart>();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "olympia_logo.png");
        Drawing? logo = null;
        if (File.Exists(logoPath))
        {
            var imagePart = headerPart.AddImagePart(ImagePartType.Png);
            using var source = File.OpenRead(logoPath);
            imagePart.FeedData(source);
            logo = ImageDrawing(headerPart.GetIdOfPart(imagePart), 500_000, 500_000, 1, "The Olympia Schools logo");
        }

        var identity = new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "20" }),
            logo == null ? new Run() : new Run(logo),
            new Run(new RunProperties(new RunFonts { Ascii = "Cambria", HighAnsi = "Cambria" }, new Bold(), new Color { Val = Purple }, new FontSize { Val = "18" }),
                new Text("  THE OLYMPIA SCHOOLS")));
        var rightParagraph = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Right },
                new SpacingBetweenLines { After = "20" },
                new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Color = Gold, Size = 10 })),
            new Run(new RunProperties(new RunFonts { Ascii = "Cambria", HighAnsi = "Cambria" }, new Bold(), new Color { Val = DarkBlue }, new FontSize { Val = "17" }),
                new Text((language == "en" ? "SCHOOL ANALYTICS REPORT" : "BÁO CÁO PHÂN TÍCH NHÀ TRƯỜNG") + "  |  Olympia Student 360")));
        headerPart.Header = new Header(identity, rightParagraph);
        headerPart.Header.Save();
        return mainPart.GetIdOfPart(headerPart);
    }

    private static Drawing ImageDrawing(string relationshipId, long width, long height, uint id, string description) =>
        new(new DW.Inline(
            new DW.Extent { Cx = width, Cy = height },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = id, Name = description, Description = description },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0, Name = description },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));

    private static string AddFooter(MainDocumentPart mainPart, string language)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        footerPart.Footer = new Footer(
            new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new ParagraphBorders(
                        new TopBorder { Val = BorderValues.Single, Color = Purple, Size = 28, Space = 6 },
                        new BottomBorder { Val = BorderValues.Single, Color = Gold, Size = 8, Space = 1 })),
                new Run(
                    new RunProperties(new RunFonts { Ascii = "Cambria", HighAnsi = "Cambria" }, new Bold(), new Color { Val = Purple }, new FontSize { Val = "16" }),
                    new Text("www.theolympiaschools.edu.vn   |   Olympia Student 360"))));
        footerPart.Footer.Save();
        return mainPart.GetIdOfPart(footerPart);
    }
}
