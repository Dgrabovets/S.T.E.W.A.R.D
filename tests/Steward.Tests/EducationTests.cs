using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Hosting;
using Steward.Models;
using Steward.Preview;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет вкладку «Автоматизация образования»: разбор расшифровок и занятий, готовые файлы заданий, настройки процессов и карточки писем
/// </summary>
public class EducationTests
{
    /// <summary>
    /// Достаёт из просьбы путь к файлу результата: он стоит на строке после слов «Результат запиши в файл, путь на следующей строке:»
    /// </summary>
    /// <param name="prompt">Просьба</param>
    /// <returns>Путь</returns>
    private static string ResultPath(string prompt) => prompt.Split("Результат запиши в файл, путь на следующей строке:")[1].Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];

    /// <summary>
    /// Резюме и реплики разбираются на части: тема, пункты, решения, задачи и доли слов по участникам
    /// </summary>
    [Fact]
    public void A_transcript_is_split_into_summary_parts_and_speaker_shares()
    {
        const string text = "Занятие: Гейм-дизайн\n\n== Резюме ==\nТема встречи: Игровые циклы\n\nКраткое содержание:\nРазобрали цикл.\nЕщё раз про баланс.\n\nКлючевые моменты:\n• Цикл\n• Баланс\n\nПринятые решения:\n• Начать курс\n\nЗадачи:\n• Написать отчёт — Срок: к пятнице\n\n== Расшифровка ==\n[00:00:01] Алексей: раз два три четыре\n[00:00:09] Данила: пять\n[00:00:12] Алексей: шесть";

        var parsed = TranscriptText.Parse(text);

        Assert.Equal("Игровые циклы", parsed.Topic);
        Assert.Equal("Разобрали цикл.\n\nЕщё раз про баланс.", parsed.Overview);
        Assert.Equal(["Цикл", "Баланс"], parsed.Points);
        Assert.Equal(["Начать курс"], parsed.Decisions);
        Assert.Single(parsed.Tasks);
        Assert.Equal("Алексей", parsed.Speakers[0].Name);
        Assert.Equal(5, parsed.Speakers[0].Words);
        Assert.Equal(6, parsed.Words);
        Assert.False(parsed.Detailed);
    }

    /// <summary>
    /// Подробный разбор от Claude заменяет короткое резюме, а статистика реплик остаётся из текста
    /// </summary>
    [Fact]
    public void A_detailed_analysis_replaces_the_summary()
    {
        var analysis = LessonAnalysis.Parse("""
            ```json
            {"topic":"Баланс","overview":"Говорили о балансе.","covered":["Кривая сложности"],"topics":[{"title":"Сложность","details":"Растёт плавно."}],"formulations":["Баланс это ощущение честности"],"decisions":[],"tasks":[{"title":"Отчёт","due":"к пятнице"}]}
            ```
            """);
        Assert.NotNull(analysis);

        var parsed = TranscriptText.Parse("== Расшифровка ==\n[00:00:01] Алексей: слово", analysis);

        Assert.True(parsed.Detailed);
        Assert.Equal("Баланс", parsed.Topic);
        Assert.Equal(["Кривая сложности"], parsed.Points);
        Assert.Equal(["Отчёт (срок: к пятнице)"], parsed.Tasks);
        Assert.Single(parsed.Topics);
        Assert.Single(parsed.Formulations);
        Assert.Equal(1, parsed.Words);
    }

    /// <summary>
    /// Разбор не JSON и не объект не принимается, лишние и недостающие ключи не мешают
    /// </summary>
    [Theory]
    [InlineData("не json")]
    [InlineData("[1,2]")]
    public void A_broken_analysis_is_rejected(string json) => Assert.Null(LessonAnalysis.Parse(json));

    /// <summary>
    /// Недостающие ключи разбора остаются пустыми
    /// </summary>
    [Fact]
    public void Missing_analysis_keys_stay_empty()
    {
        var analysis = LessonAnalysis.Parse("""{"topic":"Тема","extra":1}""");

        Assert.NotNull(analysis);
        Assert.Equal("Тема", analysis.Topic);
        Assert.Empty(analysis.Covered);
        Assert.Empty(analysis.Tasks);
    }

    /// <summary>
    /// Заметка с разбором содержит тему, пройденное и формулировки, но не реплики
    /// </summary>
    [Fact]
    public void The_analysis_note_has_the_analysis_and_no_dialogue()
    {
        var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 25), "Лекция 3", "https://record.example/1", 163);
        var analysis = new LessonAnalysis("Баланс", "Говорили о балансе.", ["Кривая"], [new AnalysisTopic("Сложность", "Растёт")], ["Баланс это честность"], ["Начать"], [new AnalysisTask("Отчёт", "к пятнице")]);

        var note = AnalysisNote.Text(lesson, analysis);

        Assert.Contains("Тема: Баланс", note);
        Assert.Contains("Кривая", note);
        Assert.Contains("Баланс это честность", note);
        Assert.Contains("срок: к пятнице", note);
        Assert.Contains("Запись занятия", note);
        Assert.Equal("lms:transcript:e1", AnalysisNote.Build(lesson, analysis, null, TimeSpan.Zero).ExternalId);
    }

    /// <summary>
    /// Markdown делится на заголовки, пункты и абзацы, жирный текст делится на куски
    /// </summary>
    [Fact]
    public void Markdown_is_split_into_blocks_and_runs()
    {
        var blocks = MarkdownBlocks.Parse("# Заголовок\n\nАбзац с **важным** словом.\n- пункт\n1. номер\n```\nкод\n```");

        Assert.Equal([MarkdownBlocks.BlockKind.Heading, MarkdownBlocks.BlockKind.Paragraph, MarkdownBlocks.BlockKind.Bullet, MarkdownBlocks.BlockKind.Bullet, MarkdownBlocks.BlockKind.Paragraph], blocks.Select(block => block.Kind).ToArray());
        Assert.Equal(1, blocks[0].Level);
        Assert.Equal([("Абзац с ", false, false), ("важным", true, false), (" словом.", false, false)], MarkdownBlocks.Runs(blocks[1].Text).ToArray());
        Assert.Equal("важным", MarkdownBlocks.Plain("**важным**"));
        Assert.Equal([("Допущение", false, true), (" и 2*3", false, false)], MarkdownBlocks.Runs("*Допущение* и 2*3").ToArray());
    }

    /// <summary>
    /// Word собирается из Markdown: в файле заголовок, абзац и жирный текст
    /// </summary>
    [Fact]
    public void A_word_file_is_written_from_markdown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"steward-{Guid.NewGuid():N}.docx");
        try
        {
            OfficeWriter.WriteWord(path, "# Игровые циклы\n\nОпишите **цикл** игры.\n- пункт");

            using var archive = ZipFile.OpenRead(path);
            var xml = XDocument.Load(archive.GetEntry("word/document.xml")!.Open());
            var text = string.Concat(xml.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value));

            Assert.Contains("Игровые циклы", text);
            Assert.Contains("цикл", text);
            Assert.Contains("пункт", text);
            Assert.Contains(xml.Descendants(), node => node.Name.LocalName == "b");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Презентация собирается из Markdown: каждый заголовок это отдельный слайд
    /// </summary>
    [Fact]
    public void A_presentation_has_a_slide_for_each_heading()
    {
        var path = Path.Combine(Path.GetTempPath(), $"steward-{Guid.NewGuid():N}.pptx");
        try
        {
            OfficeWriter.WriteSlides(path, "# Титул\n\n# Баланс\n- кривая\n- награда");

            using var archive = ZipFile.OpenRead(path);
            var slides = archive.Entries.Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, slides.Length);

            var second = XDocument.Load(archive.GetEntry("ppt/slides/slide2.xml")!.Open());
            var text = string.Concat(second.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value));
            Assert.Contains("Баланс", text);
            Assert.Contains("награда", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Блок оформления в начале текста отделяется от работы и применяется в Word: шрифт, размер, интервал, поля, красная строка, титульный лист, разрывы страниц и нумерация
    /// </summary>
    [Fact]
    public void A_word_style_block_is_applied_to_the_document()
    {
        var style = WordStyle.Extract("---\nfont: Times New Roman\nsize: 14\nspacing: 1,5\nmargins: 30,20,15,20\nalign: justify\nindent: 12.5 мм\nnumbers: center\nlost: 1\n---\n# Текст", out var body);

        Assert.Equal("# Текст", body);
        Assert.Equal("Times New Roman", style.Font);
        Assert.Equal(14, style.SizePt);
        Assert.Equal(1.5, style.Spacing);
        Assert.Equal((30, 20, 15, 20), style.Margins);
        Assert.True(style.Justify);
        Assert.Equal(12.5, style.IndentMm);
        Assert.Equal("center", style.PageNumbers);
        Assert.Equal(WordStyle.None, WordStyle.Extract("# Просто текст", out var plain));
        Assert.Equal("# Просто текст", plain);

        var path = Path.Combine(Path.GetTempPath(), $"steward-{Guid.NewGuid():N}.docx");
        try
        {
            OfficeWriter.WriteWord(path, "---\nfont: Times New Roman\nsize: 14\nspacing: 1.5\nmargins: 30,20,15,20\nalign: justify\nindent: 12.5\nnumbers: center\n---\n<center>Титульный лист</center>\n<pagebreak>\n# Введение\n\nТекст работы.");

            using var archive = ZipFile.OpenRead(path);
            var document = archive.GetEntry("word/document.xml")!.Open();
            var xml = XDocument.Load(document).ToString();

            Assert.Contains("Times New Roman", xml, StringComparison.Ordinal);
            Assert.Contains("w:sz w:val=\"28\"", xml, StringComparison.Ordinal);
            Assert.Contains("w:left=\"1701\"", xml, StringComparison.Ordinal);
            Assert.Contains("w:line=\"360\"", xml, StringComparison.Ordinal);
            Assert.Contains("w:firstLine=\"708\"", xml, StringComparison.Ordinal);
            Assert.Contains("w:type=\"page\"", xml, StringComparison.Ordinal);
            Assert.Contains("footerReference", xml, StringComparison.Ordinal);
            Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("word/footer", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Файл и папка добавляются как материалы предмета: файл копируется, путь остаётся на месте, Word превращается в читаемый текст с оформлением, выключенный материал и материалы вне выбора в подготовку не попадают
    /// </summary>
    [Fact]
    public void Guides_are_stored_per_subject_and_made_readable()
    {
        var directory = Directory.CreateTempSubdirectory("steward-guides-");
        var outside = Directory.CreateTempSubdirectory("steward-outside-");
        try
        {
            var store = new GuideStore(directory.FullName);
            var docx = Path.Combine(outside.FullName, "Указания.docx");
            OfficeWriter.WriteWord(docx, "---\nfont: Arial\nsize: 12\nspacing: 1.5\nmargins: 20,20,20,20\n---\n# Требования\n\nОбъём три страницы.\n\nРазделы начинаются с новой страницы.\n\nСписок источников обязателен.");
            File.WriteAllText(Path.Combine(outside.FullName, "образец.md"), "# Образец");

            var copied = store.AddFile("Гейм-дизайн", GuideKindEnum.Guide, docx);
            var folder = store.AddReference("Гейм-дизайн", GuideKindEnum.Example, outside.FullName);

            Assert.True(File.Exists(copied.Path));
            Assert.StartsWith(store.Folder, copied.Path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(outside.FullName, folder.Path);
            var web = store.AddReference("Гейм-дизайн", GuideKindEnum.Guide, "https://drive.google.com/file/d/abc123/view");

            Assert.Equal(string.Empty, web.Path);
            Assert.Equal("https://drive.google.com/uc?export=download&id=abc123", GuideStore.Direct(web.Url));
            Assert.Equal("https://docs.google.com/document/d/XYZ/export?format=docx", GuideStore.Direct("https://docs.google.com/document/d/XYZ/edit"));
            Assert.Equal(3, store.ForSubject("гейм-дизайн").Count);
            Assert.Equal(3, store.ForSubject("Базы данных").Count);
            Assert.Throws<ArgumentException>(() => store.AddReference("Гейм-дизайн", GuideKindEnum.Guide, "не путь и не файл"));

            var materials = store.Materials("Гейм-дизайн");

            Assert.Single(materials.Guides);
            Assert.EndsWith(".txt", materials.Guides[0], StringComparison.Ordinal);
            var text = File.ReadAllText(materials.Guides[0]);
            Assert.Contains("шрифт Arial", text, StringComparison.Ordinal);
            Assert.Contains("размер 12 pt", text, StringComparison.Ordinal);
            Assert.Contains("поля 20/20/20/20 мм", text, StringComparison.Ordinal);
            Assert.Contains("Объём три страницы.", text, StringComparison.Ordinal);
            Assert.Contains(materials.Examples, path => path.EndsWith("образец.md", StringComparison.Ordinal));
            Assert.Contains(outside.FullName, materials.Directories);

            store.SetEnabled(copied.Id, false);
            Assert.Empty(store.Materials("Гейм-дизайн").Guides);
            Assert.Single(store.Materials("Гейм-дизайн", new HashSet<string> { copied.Id }).Guides);
            Assert.Empty(store.Materials("Гейм-дизайн", new HashSet<string>()).Examples);

            store.Remove(copied.Id);
            Assert.False(File.Exists(copied.Path));
            Assert.True(File.Exists(docx));
            store.Remove(folder.Id);
            Assert.True(Directory.Exists(outside.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
            outside.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Просьба на подготовку задания перечисляет указания и образцы и просит оформить работу блоком оформления
    /// </summary>
    [Fact]
    public void The_draft_prompt_lists_the_guides_and_examples()
    {
        var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 25));
        var item = new HomeworkItem("Отчёт", null, string.Empty, string.Empty);
        var materials = new GuideMaterials([@"D:\g\guide.txt"], [@"D:\g\example.md"], [@"D:\g"]);

        var withGuides = DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md", materials);
        var without = DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md");

        Assert.Contains(@"D:\g\guide.txt", withGuides, StringComparison.Ordinal);
        Assert.Contains(@"D:\g\example.md", withGuides, StringComparison.Ordinal);
        Assert.Contains("numbers", withGuides, StringComparison.Ordinal);
        Assert.Contains("Дополнительное указание", DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md", null, "уложись в страницу"), StringComparison.Ordinal);
        Assert.DoesNotContain("Дополнительное указание", without, StringComparison.Ordinal);
        Assert.DoesNotContain("Методические указания по предмету", without, StringComparison.Ordinal);
        Assert.Contains("--add-dir", ClaudeAgent.Arguments("п", "w", [@"D:\g"]));
        Assert.Equal(2, ClaudeAgent.Arguments("п", "w", [@"D:\g"]).Count(argument => argument == "--add-dir"));
        var rules = ClaudeAgent.Arguments("п", @"D:\data\homework", [@"D:\g"]);
        Assert.Contains("Edit(//d/g/**)", rules);
        Assert.Contains("Edit(//d/data/homework/item-states.json)", rules);
        Assert.Contains("Edit(//d/data/homework/item-confirmed.json)", rules);
        Assert.Contains("Edit(//d/data/homework/item-overrides.json)", rules);
        Assert.Contains("Edit(//d/data/homework/prepare-attempts.json)", rules);
        Assert.Contains("Edit(//d/data/homework/sample-registered.json)", rules);
    }

    /// <summary>
    /// Окно «Методические указания» делит материалы по закладкам, считает их, сохраняет галочку и добавляет файлы на открытую закладку
    /// </summary>
    [Fact]
    public void The_guides_dialog_splits_files_between_tabs_and_saves_the_checkbox()
    {
        var entries = new List<GuideEntry>
        {
            new("g1", "Гейм-дизайн", GuideKindEnum.Guide, "Указания.docx", @"D:\data\guides\Указания.docx", string.Empty, string.Empty),
            new("e1", "Гейм-дизайн", GuideKindEnum.Example, "Образец.docx", @"E:\Учёба\Образец.docx", string.Empty, string.Empty, false),
        };
        var enabled = new List<(string, bool)>();
        var added = new List<(GuideKindEnum, string)>();
        var linked = new List<string>();
        var model = new GuidesDialogViewModel("Гейм-дизайн", _ => entries, @"D:\data\guides", _ => { }, id => entries.RemoveAll(entry => entry.Id == id), (id, value) => enabled.Add((id, value)), (_, kind, paths) =>
        {
            added.Add((kind, paths[0]));

            return string.Empty;
        }, (_, _, _) => string.Empty, (_, _, link) =>
        {
            linked.Add(link);

            return Task.FromResult(string.Empty);
        });

        Assert.Equal("Указания · 1", model.GuidesTitle);
        Assert.Equal("Образцы · 1", model.ExamplesTitle);
        Assert.True(model.Guides[0].IsEnabled);
        Assert.False(model.Examples[0].IsEnabled);
        Assert.Equal("Скопировано в папку приложения", model.Guides[0].Meta);
        Assert.StartsWith("Файл: ", model.Examples[0].Meta, StringComparison.Ordinal);

        model.Examples[0].IsEnabled = true;
        model.ShowExamplesCommand.Execute(null);
        model.AddFiles([@"F:\Новый образец.docx"]);
        model.AddLinkAsync("https://example.com/guide.docx").GetAwaiter().GetResult();
        model.AddLinkAsync("не ссылка").GetAwaiter().GetResult();
        Assert.Contains("нет ссылки", model.Message, StringComparison.Ordinal);
        model.Guides[0].RemoveCommand.Execute(null);

        Assert.Equal(["https://example.com/guide.docx"], linked);
        Assert.Equal([("e1", true)], enabled);
        Assert.Equal([(GuideKindEnum.Example, @"F:\Новый образец.docx")], added);
        Assert.Equal("Указания · 0", model.GuidesTitle);
        Assert.True(model.GuidesEmpty);
    }

    /// <summary>
    /// Окно «Подготовить задание» предлагает форматы, берёт отметки материалов из списка и собирает запрос из выбранного
    /// </summary>
    [Fact]
    public void The_prepare_dialog_builds_the_request_from_the_choices()
    {
        var materials = new List<GuideEntry>
        {
            new("g1", "Гейм-дизайн", GuideKindEnum.Guide, "Указания.docx", @"D:\g\Указания.docx", string.Empty, string.Empty),
            new("e1", "Гейм-дизайн", GuideKindEnum.Example, "Образец.docx", @"D:\g\Образец.docx", string.Empty, string.Empty, false),
        };
        var model = new PrepareDialogViewModel("25.10.2026 · Лекция 3", HomeworkFormatEnum.Skip, materials, @"D:\g");

        Assert.Equal(["Word", "Презентация", "MD", "Код"], model.Formats.Select(choice => choice.Title).ToArray());
        Assert.Equal(HomeworkFormatEnum.Word, model.Format);
        Assert.True(model.Formats[0].IsSelected);
        Assert.False(model.NoMaterials);

        model.Formats.Single(choice => choice.Format == HomeworkFormatEnum.Presentation).SelectCommand.Execute(null);
        model.Examples[0].IsChecked = true;
        model.Guides[0].IsChecked = false;
        model.Extra = "  уложись в страницу  ";
        var request = model.Request();

        Assert.False(model.Formats[0].IsSelected);
        Assert.Equal(HomeworkFormatEnum.Presentation, request.Format);
        Assert.Equal("уложись в страницу", request.Extra);
        Assert.Equal(["e1"], request.GuideIds!.ToArray());
        Assert.True(new PrepareDialogViewModel("x", HomeworkFormatEnum.Word, [], string.Empty).NoMaterials);
    }

    /// <summary>
    /// Задание, которое по данным LMS уже сдано, читается из ответа агента, не готовится, а в просьбе агенту есть список работ предмета
    /// </summary>
    [Fact]
    public async Task A_submitted_item_is_read_and_skipped_when_preparing()
    {
        var items = HomeworkItemParser.Parse("""[{"title":"Отчёт 1","done":true},{"title":"Отчёт 2","done":false},{"title":"Отчёт 3"}]""");

        Assert.Equal([true, false, false], items.Select(item => item.Done).ToArray());

        var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 19));
        var prompt = HomeworkExtractionPrompt.Build(lesson, "t.txt", "r.json", [new ActivityInfo("Гейм-дизайн", "Задание 1", false, true, "Баланс", new DateOnly(2026, 10, 23)), new ActivityInfo("Гейм-дизайн", "Итоговый тест", true, false)]);

        Assert.Contains("Задание 1; раздел: Баланс; срок: 23.10.2026; сдано", prompt, StringComparison.Ordinal);
        Assert.Contains("Итоговый тест; раздел: ; срок: нет; не сдано; тест", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Работы предмета в LMS", HomeworkExtractionPrompt.Build(lesson, "t.txt", "r.json"), StringComparison.Ordinal);

        var directory = Directory.CreateTempSubdirectory("steward-done-");
        try
        {
            var calls = 0;
            DraftBuilder.AgentRun agent = (request, _, _, _) =>
            {
                calls++;
                File.WriteAllText(ResultPath(request), "# Работа");

                return Task.FromResult(string.Empty);
            };

            var (files, error) = await DraftBuilder.BuildAsync(new HomeworkStore(directory.FullName), lesson, items, HomeworkFormatEnum.Text, agent, CancellationToken.None);

            Assert.Empty(error);
            Assert.Equal(2, calls);
            Assert.Equal(2, files.Count);
            Assert.DoesNotContain(files, file => Path.GetFileName(file).StartsWith("Задание 1.", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Когда у занятия есть готовые файлы, кнопка «Подготовить задание» уходит, а вместо неё кнопки «Посмотреть задание»: у одного файла без номера, у нескольких с номером. Файл открывается через переданное действие
    /// </summary>
    [Fact]
    public void Ready_files_replace_the_prepare_button_with_view_buttons()
    {
        var opened = new List<string>();
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)));
        model.PathOpened = opened.Add;
        var directory = Directory.CreateTempSubdirectory("steward-view-");
        try
        {
            var first = Path.Combine(directory.FullName, "Задание 1. Отчёт.docx");
            var second = Path.Combine(directory.FullName, "Задание 2. План.md");
            File.WriteAllText(first, "x");
            model.DraftLister = _ => [first];
            model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
            var lesson = model.Lessons.Single();

            Assert.False(lesson.CanPrepare);
            Assert.Equal("Посмотреть задание", lesson.Drafts.Single().ButtonTitle);

            File.WriteAllText(second, "x");
            model.DraftLister = _ => [first, second];
            model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
            lesson.Drafts[1].OpenCommand.Execute(null);

            Assert.Equal(["Посмотреть задание 1", "Посмотреть задание 2"], lesson.Drafts.Select(draft => draft.ButtonTitle).ToArray());
            Assert.Equal([second], opened);

            model.DraftLister = _ => [];
            model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
            Assert.False(lesson.HasDrafts);
            Assert.Equal("MD", HomeworkFormats.Title(HomeworkFormatEnum.Text));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Убранное в архив задание не показывается, не попадает в строки заданий и возвращается из архива, а поиск заданий нужен только там, где их ещё не искали
    /// </summary>
    [Fact]
    public void Archived_items_are_hidden_and_the_search_runs_once()
    {
        var directory = Directory.CreateTempSubdirectory("steward-archive-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 19), RecordUrl: "https://record.example/1");
            store.SetSubjectState("Гейм-дизайн", LessonStateEnum.Fetch);
            Directory.CreateDirectory(Path.GetDirectoryName(store.TranscriptPath("e1"))!);
            File.WriteAllText(store.TranscriptPath("e1"), "== Расшифровка ==");

            Assert.True(store.ShouldExtract(lesson));

            Directory.CreateDirectory(Path.GetDirectoryName(store.ItemsPath("e1"))!);
            File.WriteAllText(store.ItemsPath("e1"), """[{"title":"Один"},{"title":"Два"}]""");
            File.WriteAllText(Path.Combine(directory.FullName, "lessons.json"), """[{"id":"e1","title":"Гейм-дизайн","date":"2026-10-19"}]""");

            Assert.False(store.ShouldExtract(lesson));
            Assert.Equal(2, store.LoadRows().Count);

            store.SetArchived("e1", 1, archived: true);
            Assert.True(store.IsArchived("e1", 1));
            Assert.Equal([1], store.ArchivedNumbers("e1").ToArray());
            Assert.Empty(store.ArchivedNumbers("e10"));
            Assert.False(store.IsArchived("e1", 2));
            Assert.Equal(["Два"], store.LoadRows().Select(row => row.Item.Title).ToArray());

            store.SetArchived("e1", 1, archived: false);
            Assert.Equal(2, store.LoadRows().Count);

            store.SetSubjectState("Гейм-дизайн", LessonStateEnum.Analyze);
            File.Delete(store.ItemsPath("e1"));
            Assert.False(store.ShouldExtract(lesson));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Строка найденного задания показывает срок и пометки, сокращает требования и передаёт нажатия «В архив» и «В Singularity»
    /// </summary>
    [Fact]
    public void An_item_row_shows_the_meta_and_reports_its_buttons()
    {
        var archived = new List<int>();
        var added = new List<int>();
        var row = new LessonItemViewModel(2, new HomeworkItem("Отчёт", new DateOnly(2026, 11, 2), new string('я', 400), string.Empty, true), false, item => archived.Add(item.Number), item => added.Add(item.Number));

        Assert.Equal("Срок: 02.11.2026  ·  уже сдано", row.Meta);
        Assert.Equal(221, row.Requirements.Length);
        Assert.True(row.AddToSingularityCommand.CanExecute(null));

        row.ArchiveCommand.Execute(null);
        row.AddToSingularityCommand.Execute(null);

        Assert.Equal([2], archived);
        Assert.Equal([2], added);
        Assert.False(new LessonItemViewModel(1, new HomeworkItem("А", null, string.Empty, string.Empty), true, _ => { }, _ => { }).AddToSingularityCommand.CanExecute(null));
    }

    /// <summary>
    /// Кнопка подготовки есть, только если нашлись несданные задания и готовых файлов ещё нет, а задания, архив и отметка поиска обновляются из чтения
    /// </summary>
    [Fact]
    public void The_prepare_button_needs_pending_items_and_no_ready_files()
    {
        var found = new List<(int Number, HomeworkItem Item, bool InSingularity)>();
        var searched = false;
        var archived = new List<(string, int)>();
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            ItemsReader = _ => searched ? found : null,
            ItemArchived = (id, number) => archived.Add((id, number)),
        };
        var lesson = model.Lessons.Single();

        Assert.False(lesson.CanPrepare);
        Assert.Contains("ищутся", lesson.ItemsHint, StringComparison.Ordinal);

        searched = true;
        found.Add((1, new HomeworkItem("Отчёт", null, string.Empty, string.Empty), false));
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);

        Assert.True(lesson.CanPrepare);
        Assert.Single(lesson.Items);

        found[0] = (1, new HomeworkItem("Отчёт", null, string.Empty, string.Empty, true), false);
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        Assert.False(lesson.CanPrepare);

        found.Clear();
        found.Add((1, new HomeworkItem("Отчёт", null, string.Empty, string.Empty), false));
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        lesson.Items[0].ArchiveCommand.Execute(null);

        Assert.Equal([("a", 1)], archived);

        found.Clear();
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        Assert.Contains("заданий нет", lesson.ItemsHint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Просьба на подготовку получает условия заданий из LMS, а просьба на поиск заданий получает пути к их описаниям
    /// </summary>
    [Fact]
    public void Assignment_descriptions_reach_both_prompts()
    {
        var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 19));
        var item = new HomeworkItem("Отчёт", null, string.Empty, string.Empty);

        var draft = DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md", null, string.Empty, [@"D:\a\1\assignment.md"]);
        var search = HomeworkExtractionPrompt.Build(lesson, "t.txt", "r.json", [new ActivityInfo("Гейм-дизайн", "Задание 5", false, false, "Баланс", null, "99")], new Dictionary<string, string> { ["99"] = @"D:\a\99\assignment.md" });

        Assert.Contains(@"D:\a\1\assignment.md", draft, StringComparison.Ordinal);
        Assert.Contains("Условия заданий предмета из LMS", draft, StringComparison.Ordinal);
        Assert.Contains(@"условие и файлы: D:\a\99\assignment.md", search, StringComparison.Ordinal);
        Assert.DoesNotContain("Условия заданий предмета", DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md"), StringComparison.Ordinal);
        Assert.True(new PrepareRequest(HomeworkFormatEnum.Word, "замечания", null, Redo: true).Redo);
        Assert.False(PrepareRequest.Default(HomeworkFormatEnum.Word).Redo);
    }

    /// <summary>
    /// Готовые файлы делаются для каждого задания: агент пишет Markdown, Steward собирает Word, уже готовые не переделываются
    /// </summary>
    [Fact]
    public async Task Drafts_are_built_for_every_item_and_not_repeated()
    {
        var directory = Directory.CreateTempSubdirectory("steward-drafts-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 25));
            var items = new[] { new HomeworkItem("Описать цикл", null, "одна страница", string.Empty), new HomeworkItem("Баланс", null, string.Empty, string.Empty) };
            var runs = 0;
            DraftBuilder.AgentRun agent = (prompt, _, _, _) =>
            {
                runs++;
                var path = ResultPath(prompt);
                File.WriteAllText(path, "# Работа\n\nТекст.");

                return Task.FromResult(string.Empty);
            };

            var (files, error) = await DraftBuilder.BuildAsync(store, lesson, items, HomeworkFormatEnum.Word, agent, CancellationToken.None);

            Assert.Empty(error);
            Assert.Equal(2, files.Count);
            Assert.All(files, file => Assert.EndsWith(".docx", file, StringComparison.Ordinal));
            Assert.Equal(files.Order().ToArray(), store.DraftFiles("e1").Order().ToArray());
            Assert.False(Directory.Exists(Path.Combine(store.DraftsDirectory("e1"), ".src")));

            await DraftBuilder.BuildAsync(store, lesson, items, HomeworkFormatEnum.Word, agent, CancellationToken.None);
            Assert.Equal(2, runs);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ошибка агента не роняет подготовку: остальные задания готовятся, причина возвращается текстом
    /// </summary>
    [Fact]
    public async Task An_agent_failure_is_reported_and_other_items_continue()
    {
        var directory = Directory.CreateTempSubdirectory("steward-drafts-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 25));
            var items = new[] { new HomeworkItem("Один", null, string.Empty, string.Empty), new HomeworkItem("Два", null, string.Empty, string.Empty) };
            var call = 0;
            DraftBuilder.AgentRun agent = (prompt, _, _, _) =>
            {
                if (call++ == 0)
                    return Task.FromResult("лимит исчерпан");

                File.WriteAllText(ResultPath(prompt), "# Работа");

                return Task.FromResult(string.Empty);
            };

            var (files, error) = await DraftBuilder.BuildAsync(store, lesson, items, HomeworkFormatEnum.Text, agent, CancellationToken.None);

            Assert.Single(files);
            Assert.Contains("Задание 1: лимит исчерпан", error);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Имя файла очищается от запрещённых знаков и обрезается
    /// </summary>
    [Fact]
    public void A_file_name_is_cleaned()
    {
        Assert.Equal("Что делать", DraftBuilder.SafeName("Что: делать?"));
        Assert.Equal("работа", DraftBuilder.SafeName("???"));
        Assert.True(DraftBuilder.SafeName(new string('а', 100)).Length <= 60);
        Assert.Equal("Выбрать одну любую игру и написать по ней аналитический", DraftBuilder.SafeName("Выбрать одну любую игру и написать по ней аналитический отчёт"));
    }

    /// <summary>
    /// Отметка «Только анализ» забирает расшифровку, разбор нужен, а домашние задания не готовятся
    /// </summary>
    [Fact]
    public void The_analyze_state_fetches_and_analyzes_but_skips_homework()
    {
        var directory = Directory.CreateTempSubdirectory("steward-state-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            var lesson = new Lesson("e1", "Английский", new DateOnly(2026, 10, 25), RecordUrl: "https://record.example/1");

            store.SetSubjectState("Английский", LessonStateEnum.Analyze);

            Assert.True(store.ShouldFetch(lesson));
            Assert.False(store.PreparesHomework(lesson));

            Directory.CreateDirectory(Path.GetDirectoryName(store.TranscriptPath("e1"))!);
            File.WriteAllText(store.TranscriptPath("e1"), "== Резюме ==\nТема встречи: Тема");
            Assert.False(store.ShouldFetch(lesson));
            Assert.True(store.ShouldAnalyze(lesson));

            Directory.CreateDirectory(Path.GetDirectoryName(store.AnalysisPath("e1"))!);
            File.WriteAllText(store.AnalysisPath("e1"), """{"topic":"Тема"}""");
            Assert.False(store.ShouldAnalyze(lesson));
            Assert.Equal("Тема", store.ReadAnalysis("e1")!.Topic);

            store.SetState("e1", LessonStateEnum.Fetch);
            Assert.True(store.PreparesHomework(lesson));
            store.SetState("e1", LessonStateEnum.Skip);
            Assert.False(store.PreparesHomework(lesson));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Работы из LMS читаются в счётчики: тест отличается от задания, сдано считается отдельно
    /// </summary>
    [Fact]
    public void Activities_are_read_for_the_counters()
    {
        var directory = Directory.CreateTempSubdirectory("steward-activities-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Assert.Empty(store.LoadActivities());

            File.WriteAllText(Path.Combine(directory.FullName, "activities.json"), """[{"subject":"Гейм-дизайн","name":"Задание 1","kind":0,"done":true},{"subject":"Гейм-дизайн","name":"Тест","kind":1,"done":false}]""");
            var activities = store.LoadActivities();

            Assert.Equal(2, activities.Count);
            Assert.True(activities[0].Done);
            Assert.True(activities[1].IsQuiz);

            var subject = new SubjectViewModel("Гейм-дизайн", [], LessonStateEnum.Fetch, (_, _) => { }, HomeworkFormatEnum.Word, (_, _) => { });
            subject.SetActivities(activities);

            Assert.Equal("Сдано 1 из 1", subject.AssignmentsText);
            Assert.Equal("Тесты 0 из 1", subject.QuizzesText);
            Assert.Equal(1.0, subject.AssignmentsProgress);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Список формата показывается только при «Расшифровывать», а выбор в списке доходит до хранилища
    /// </summary>
    [Fact]
    public void The_format_list_is_shown_only_for_fetching_subjects()
    {
        var chosen = new List<LessonStateEnum>();
        var subject = new SubjectViewModel("Гейм-дизайн", [], LessonStateEnum.Undecided, (_, state) => chosen.Add(state), HomeworkFormatEnum.Word, (_, _) => { });

        Assert.False(subject.ShowFormat);
        Assert.Equal(["Ждёт решения", "Расшифровывать", "Только анализ", "Не нужно", "Завершён"], subject.StateOptions.Select(option => option.Title).ToArray());

        subject.SelectedStateOption = subject.StateOptions.Single(option => option.State == LessonStateEnum.Fetch);
        Assert.True(subject.ShowFormat);

        subject.SelectedStateOption = subject.StateOptions.Single(option => option.State == LessonStateEnum.Analyze);
        Assert.False(subject.ShowFormat);
        Assert.Equal([LessonStateEnum.Fetch, LessonStateEnum.Analyze], chosen);
    }

    /// <summary>
    /// Карточка письма берёт ссылку и описание из хвоста строки, чужие адреса не принимаются
    /// </summary>
    [Fact]
    public void A_mail_card_reads_the_link_and_description()
    {
        var opened = new List<string>();
        var card = MailLetterViewModel.From(new LogLine("25.10.2026 14:11:06", "Ответить: Иванов Иван — Вопрос → задача «Ответ» ‖ https://mail.example/#q ‖ Когда срок сдачи?", PanelToneEnum.Attention), opened.Add);

        Assert.Equal("Ответить", card.Kind);
        Assert.Equal("Вопрос", card.Subject);
        Assert.Equal("Иванов Иван", card.Sender);
        Assert.Contains("задача создана", card.Meta);
        Assert.Equal("Когда срок сдачи?", card.Description);
        Assert.True(card.HasLink);

        card.OpenCommand.Execute(null);
        Assert.Equal(["https://mail.example/#q"], opened);

        var unsafeCard = MailLetterViewModel.From(new LogLine("25.10.2026 14:11:06", "Инфо: Кто-то — Тема ‖ javascript:alert(1) ‖ текст", PanelToneEnum.Idle), opened.Add);
        Assert.False(unsafeCard.HasLink);

        var plain = MailLetterViewModel.From(new LogLine("25.10.2026 14:11:06", "Инфо: Яндекс ID — Вы создали пароль", PanelToneEnum.Idle));
        Assert.False(plain.HasLink);
        Assert.False(plain.HasDescription);
    }

    /// <summary>
    /// Страница настроек читает значения без заглушек, пишет только свои ключи и числа числами
    /// </summary>
    [Fact]
    public void Settings_are_loaded_and_saved_without_touching_other_keys()
    {
        var directory = Directory.CreateTempSubdirectory("steward-settings-");
        try
        {
            var file = Path.Combine(directory.FullName, "appsettings.json");
            File.WriteAllText(file, """{"Mail":{"Login":"<логин>","Port":993,"Folder":"INBOX"},"Singularity":{"Token":"секрет"},"Other":true}""");
            var settings = new JsonSettingsViewModel(
                file,
                string.Empty,
                [new SettingsSectionViewModel("Почта", [new SettingFieldViewModel("mail.login", "Логин", string.Empty), new SettingFieldViewModel("Mail.Port", "Порт", string.Empty, number: true), new SettingFieldViewModel("Singularity.Token", "Токен", string.Empty, secret: true)])],
                () => { });
            var saved = 0;
            settings.Saved += () => saved++;

            var fields = settings.Sections[0].Fields;
            Assert.Equal(string.Empty, fields[0].Value);
            Assert.Equal("993", fields[1].Value);

            fields[0].Value = "student@example.com";
            fields[1].Value = "995";
            settings.SaveCommand.Execute(null);

            var written = JsonNode.Parse(File.ReadAllText(file))!;
            Assert.Equal("student@example.com", (string?)written["Mail"]!["Login"]);
            Assert.Equal(995, (int?)written["Mail"]!["Port"]);
            Assert.Equal("INBOX", (string?)written["Mail"]!["Folder"]);
            Assert.Equal("секрет", (string?)written["Singularity"]!["Token"]);
            Assert.True((bool?)written["Other"]);
            Assert.Equal(1, saved);

            fields[1].Value = "много";
            settings.SaveCommand.Execute(null);
            Assert.Equal(1, saved);
            Assert.Contains("целое число", settings.Message);
            Assert.Equal(995, (int?)JsonNode.Parse(File.ReadAllText(file))!["Mail"]!["Port"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Проверка настроек видит заглушки и пустые значения, а секреты не читает
    /// </summary>
    [Fact]
    public void The_settings_probe_tells_filled_values_from_placeholders()
    {
        var root = Directory.CreateTempSubdirectory("steward-probe-");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsProbe.MailFile(root.FullName))!);
            File.WriteAllText(SettingsProbe.MailFile(root.FullName), """{"Mail":{"AppPassword":"<пароль>"},"Singularity":{"Token":"abc"}}""");

            var chips = SettingsProbe.Mail(root.FullName);

            Assert.Equal(["Файл настроек", "Пароль приложения", "Токен Singularity"], chips.Select(chip => chip.Label).ToArray());
            Assert.Equal("найден", chips[0].Value);
            Assert.Equal("не задан", chips[1].Value);
            Assert.Equal("задан", chips[2].Value);
            Assert.Equal(PanelToneEnum.Attention, chips[1].Tone);
            Assert.Equal(PanelToneEnum.Running, chips[2].Tone);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Расширения настроек процессов в реестре: «homePage» читается из файла, расписание всегда открывается настройками
    /// </summary>
    [Fact]
    public void The_schedule_always_opens_on_its_settings()
    {
        var model = new MainViewModel();
        model.SetSettingsHome([]);

        model.Page = AppPageEnum.Schedule;
        Assert.True(model.HomeIsSettings);
        Assert.True(model.IsSettingsOpen);
        Assert.False(model.CanToggleSettings);

        model.Page = AppPageEnum.Mail;
        Assert.False(model.IsSettingsOpen);
        model.ToggleSettingsCommand.Execute(null);
        Assert.True(model.IsSettingsOpen);

        model.Page = AppPageEnum.Status;
        Assert.False(model.IsSettingsOpen);

        model.SetSettingsHome(["mail"]);
        model.Page = AppPageEnum.Mail;
        Assert.True(model.HomeIsSettings);
    }
}
