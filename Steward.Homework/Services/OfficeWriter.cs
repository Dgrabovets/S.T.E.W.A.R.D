using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Steward.Homework.Services;

/// <summary>
/// Записывает готовые файлы Word и презентации из Markdown. Файлы открываются в Word и PowerPoint
/// </summary>
public static class OfficeWriter
{
    /// <summary>
    /// Имя вшитого шаблона презентации
    /// </summary>
    private const string SLIDES_TEMPLATE = "Steward.Homework.Templates.slides-template.pptx";

    /// <summary>
    /// Пишет документ Word: заголовки крупным жирным шрифтом, абзацы, пункты списка, жирный текст и курсив. Если текст начинается с блока оформления, применяются его шрифт, размер, интервал, поля, выравнивание и красная строка, а строки «&lt;center&gt;» и «&lt;pagebreak&gt;» дают титульный лист и разрывы страниц
    /// </summary>
    /// <param name="path">Куда записать .docx</param>
    /// <param name="markdown">Текст работы в Markdown</param>
    public static void WriteWord(string path, string markdown)
    {
        var style = WordStyle.Extract(markdown, out var text);
        var basePt = style.SizePt ?? 11;
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var body = new Body();
        foreach (var block in MarkdownBlocks.Parse(text))
        {
            if (block.Kind == MarkdownBlocks.BlockKind.PageBreak)
            {
                body.AppendChild(new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new Break { Type = BreakValues.Page })));

                continue;
            }

            var paragraph = new Paragraph();
            var properties = new ParagraphProperties();
            var size = basePt;
            var bold = false;
            var line = style.Spacing is { } spacing ? ((int)(spacing * 240)).ToString(System.Globalization.CultureInfo.InvariantCulture) : "300";
            switch (block.Kind)
            {
                case MarkdownBlocks.BlockKind.Heading:
                    properties.ParagraphStyleId = new ParagraphStyleId { Val = block.Level == 1 ? "Title" : $"Heading{Math.Min(block.Level - 1, 3)}" };
                    properties.SpacingBetweenLines = new SpacingBetweenLines { Before = "240", After = "120", Line = line, LineRule = LineSpacingRuleValues.Auto };
                    size = style.SizePt is { } baseSize ? baseSize + (block.Level == 1 ? 2 : block.Level == 2 ? 1 : 0) : block.Level switch { 1 => 20, 2 => 16, _ => 13 };
                    bold = true;
                    if (style.Justify)
                        properties.Justification = new Justification { Val = JustificationValues.Center };

                    break;
                case MarkdownBlocks.BlockKind.Center:
                    properties.SpacingBetweenLines = new SpacingBetweenLines { After = "120", Line = line, LineRule = LineSpacingRuleValues.Auto };
                    properties.Justification = new Justification { Val = JustificationValues.Center };
                    break;
                case MarkdownBlocks.BlockKind.Bullet:
                    properties.Indentation = new Indentation { Left = "567", Hanging = "283" };
                    properties.SpacingBetweenLines = new SpacingBetweenLines { After = "60", Line = line, LineRule = LineSpacingRuleValues.Auto };
                    if (style.Justify)
                        properties.Justification = new Justification { Val = JustificationValues.Both };

                    paragraph.AppendChild(Run("•\t", size, false, false, style.Font));
                    break;
                default:
                    properties.SpacingBetweenLines = new SpacingBetweenLines { After = "140", Line = line, LineRule = LineSpacingRuleValues.Auto };
                    if (style.IndentMm is { } indent)
                        properties.Indentation = new Indentation { FirstLine = ((int)(indent * 56.7)).ToString(System.Globalization.CultureInfo.InvariantCulture) };

                    if (style.Justify)
                        properties.Justification = new Justification { Val = JustificationValues.Both };

                    break;
            }

            paragraph.PrependChild(properties);
            foreach (var (piece, isBold, isItalic) in MarkdownBlocks.Runs(block.Text))
                paragraph.AppendChild(Run(piece, size, bold || isBold, isItalic, style.Font));

            body.AppendChild(paragraph);
        }

        var section = new SectionProperties();
        if (style.PageNumbers is { } alignment)
        {
            var footer = main.AddNewPart<FooterPart>();
            var number = new Paragraph(
                new ParagraphProperties(new Justification { Val = alignment == "left" ? JustificationValues.Left : alignment == "right" ? JustificationValues.Right : JustificationValues.Center }),
                new DocumentFormat.OpenXml.Wordprocessing.Run(new RunProperties(new FontSize { Val = ((int)(basePt * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) }), new FieldChar { FieldCharType = FieldCharValues.Begin }),
                new DocumentFormat.OpenXml.Wordprocessing.Run(new RunProperties(new FontSize { Val = ((int)(basePt * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) }), new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
                new DocumentFormat.OpenXml.Wordprocessing.Run(new RunProperties(new FontSize { Val = ((int)(basePt * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) }), new FieldChar { FieldCharType = FieldCharValues.Separate }),
                new DocumentFormat.OpenXml.Wordprocessing.Run(new RunProperties(new FontSize { Val = ((int)(basePt * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) }), new DocumentFormat.OpenXml.Wordprocessing.Text("1")),
                new DocumentFormat.OpenXml.Wordprocessing.Run(new RunProperties(new FontSize { Val = ((int)(basePt * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) }), new FieldChar { FieldCharType = FieldCharValues.End }));
            footer.Footer = new Footer(number);
            footer.Footer.Save();
            section.AppendChild(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) });
        }

        if (style.Margins is { } margins)
        {
            static string Twips(int mm) => ((int)(mm * 56.7)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            section.AppendChild(new PageMargin { Left = uint.Parse(Twips(margins.Left)), Top = int.Parse(Twips(margins.Top)), Right = uint.Parse(Twips(margins.Right)), Bottom = int.Parse(Twips(margins.Bottom)), Header = 708u, Footer = 708u, Gutter = 0u });
        }

        if (section.HasChildren)
            body.AppendChild(section);

        main.Document = new Document(body);
        main.Document.Save();
    }

    /// <summary>
    /// Пишет презентацию: каждый заголовок начинает новый слайд, пункты и абзацы после него идут в тело слайда
    /// </summary>
    /// <param name="path">Куда записать .pptx</param>
    /// <param name="markdown">Текст презентации в Markdown</param>
    public static void WriteSlides(string path, string markdown)
    {
        using var template = Assembly.GetExecutingAssembly().GetManifestResourceStream(SLIDES_TEMPLATE) ?? throw new InvalidOperationException("В сборке нет шаблона презентации");
        using var buffer = new MemoryStream();
        template.CopyTo(buffer);

        var slides = new List<(string Title, List<string> Lines)>();
        WordStyle.Extract(markdown, out var content);
        foreach (var block in MarkdownBlocks.Parse(content).Where(block => block.Kind != MarkdownBlocks.BlockKind.PageBreak))
        {
            if (block.Kind == MarkdownBlocks.BlockKind.Heading || slides.Count == 0)
                slides.Add((block.Kind == MarkdownBlocks.BlockKind.Heading ? MarkdownBlocks.Plain(block.Text) : "Презентация", []));

            if (block.Kind != MarkdownBlocks.BlockKind.Heading)
                slides[^1].Lines.Add(MarkdownBlocks.Plain(block.Text));
        }

        using (var document = PresentationDocument.Open(buffer, true))
        {
            var presentation = document.PresentationPart!;
            var layout = presentation.SlideMasterParts.First().SlideLayoutParts.ElementAt(1);
            var list = presentation.Presentation.SlideIdList ?? presentation.Presentation.InsertAfter(new SlideIdList(), presentation.Presentation.SlideMasterIdList)!;
            uint id = 256;
            foreach (var (title, lines) in slides)
            {
                var part = presentation.AddNewPart<SlidePart>();
                part.Slide = new Slide(SlideXml(title, lines));
                part.AddPart(layout);
                list.AppendChild(new SlideId { Id = id++, RelationshipId = presentation.GetIdOfPart(part) });
            }

            presentation.Presentation.Save();
        }

        File.WriteAllBytes(path, buffer.ToArray());
    }

    /// <summary>
    /// Собирает фрагмент текста Word
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="size">Размер шрифта в пунктах</param>
    /// <param name="bold">Жирный шрифт</param>
    /// <param name="italic">Курсив</param>
    /// <param name="font">Шрифт или <see langword="null"/>, тогда по умолчанию</param>
    /// <returns>Фрагмент</returns>
    private static DocumentFormat.OpenXml.Wordprocessing.Run Run(string text, double size, bool bold, bool italic = false, string? font = null)
    {
        var properties = new RunProperties(new FontSize { Val = ((int)(size * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        if (font is not null)
            properties.PrependChild(new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font, EastAsia = font });

        if (italic)
            properties.PrependChild(new Italic());

        if (bold)
            properties.PrependChild(new Bold());

        var run = new DocumentFormat.OpenXml.Wordprocessing.Run(properties);
        var parts = text.Split('\t');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                run.AppendChild(new TabChar());

            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        return run;
    }

    /// <summary>
    /// Собирает XML слайда с заголовком и пунктами по макету «Заголовок и объект»
    /// </summary>
    /// <param name="title">Заголовок слайда</param>
    /// <param name="lines">Строки тела</param>
    /// <returns>XML слайда</returns>
    private static string SlideXml(string title, IReadOnlyList<string> lines)
    {
        static string Escape(string text) => System.Security.SecurityElement.Escape(text) ?? string.Empty;
        var paragraphs = lines.Count == 0 ? "<a:p><a:endParaRPr lang=\"ru-RU\"/></a:p>" : string.Concat(lines.Select(line => $"<a:p><a:r><a:rPr lang=\"ru-RU\" sz=\"2200\"/><a:t>{Escape(line)}</a:t></a:r></a:p>"));

        return $"<p:sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>"
            + $"<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Заголовок\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"title\"/></p:nvPr></p:nvSpPr><p:spPr/><p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"ru-RU\"/><a:t>{Escape(title)}</a:t></a:r></a:p></p:txBody></p:sp>"
            + $"<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Содержимое\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr/><p:txBody><a:bodyPr><a:normAutofit/></a:bodyPr><a:lstStyle/>{paragraphs}</p:txBody></p:sp>"
            + "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>";
    }
}
