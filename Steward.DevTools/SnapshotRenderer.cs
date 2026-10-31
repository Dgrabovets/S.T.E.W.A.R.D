using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Ideas.Models;
using Steward.Models;
using Steward.Monitoring.Models;
using Steward.Status.Registry;
using Steward.Status.Rows;
using Steward.Status.Running;
using Steward.ViewModels;
using Steward.Views;
using Steward.Shell;
using Steward.DevTools.Design;

namespace Steward.DevTools;

/// <summary>
/// Рисует окна приложения в PNG на примере выдуманных данных. Нужен, чтобы проверить вид, не открывая окно на рабочем столе
/// Запуск: <c>dotnet run --project Steward.DevTools -- snapshot файл.png [appicon | logo | social | settings | tray | design | main[:status|ideas|homework|journal] | maximized:1920x1080[@125][:страница] | card:found|confirm|ready|checked|submitted | guides[:examples|remarks] | redo]</c>
/// </summary>
public static class SnapshotRenderer
{
    /// <summary>
    /// Высота панели задач Windows 11 в пикселях при масштабе 100 %
    /// </summary>
    private const int TASKBAR_PIXELS = 48;

    /// <summary>
    /// На сколько пикселей при 100 % Windows выносит развёрнутое окно без рамки за каждый край: толщина рамки изменения размера
    /// </summary>
    private const int FRAME_PIXELS = 8;

    /// <summary>
    /// Разбирает размер монитора вида <c>1920x1080</c> или <c>1366x768@125</c>
    /// </summary>
    /// <param name="spec">Ширина и высота в пикселях и, после @, масштаб в процентах</param>
    /// <returns>Ширина, высота и масштаб (1.25 при 125 %)</returns>
    private static (int Width, int Height, double Scale) ParseMonitor(string spec)
    {
        var atParts = spec.Split('@');
        var size = atParts[0].Split('x', 'X', '×');
        var width = size.Length == 2 && int.TryParse(size[0], out var w) ? w : 1920;
        var height = size.Length == 2 && int.TryParse(size[1], out var h) ? h : 1080;
        var scale = atParts.Length > 1 && int.TryParse(atParts[1], out var percent) && percent > 0 ? percent / 100.0 : 1.0;

        return (width, height, scale);
    }

    /// <summary>
    /// Рисует окно и сохраняет картинку
    /// </summary>
    /// <param name="path">Куда сохранить PNG</param>
    /// <param name="which">
    /// Какое окно: <c>main</c>, <c>tray</c> или <c>design</c> (страница-образец дизайн-системы)
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если картинка записана
    /// </returns>
    public static bool Render(string path, string which)
    {
        if (string.Equals(which, "logo", StringComparison.OrdinalIgnoreCase))
        {
            AppIconWriter.WritePng(path, 512);

            return true;
        }

        if (string.Equals(which, "social", StringComparison.OrdinalIgnoreCase))
        {
            AppIconWriter.WriteSocial(path);

            return true;
        }

        if (string.Equals(which, "appicon", StringComparison.OrdinalIgnoreCase))
        {
            AppIconWriter.Write(path);

            return true;
        }

        var now = DateTimeOffset.Now;
        var settleSeconds = 0.0;
        Rect? crop = null;
        System.Windows.Size? target = null;
        Action? afterShow = null;
        var snapshot = SampleSnapshot(now);
        var jobs = SampleJobs();

        var parts = which.Split(':');
        Window window;
        if (string.Equals(parts[0], "design", StringComparison.OrdinalIgnoreCase))
        {
            window = new DesignWindow { SizeToContent = SizeToContent.Manual, Height = 1560 };
        }
        else if (string.Equals(parts[0], "slider", StringComparison.OrdinalIgnoreCase))
        {
            window = SliderSheet();
        }
        else if (string.Equals(parts[0], "icons", StringComparison.OrdinalIgnoreCase))
        {
            window = IconSheet();
        }
        else if (string.Equals(parts[0], "orbs", StringComparison.OrdinalIgnoreCase))
        {
            window = OrbSheet();
        }
        else if (string.Equals(parts[0], "guides", StringComparison.OrdinalIgnoreCase))
        {
            // Окно «Методические указания» на примерных материалах
            var folder = @"D:\data\homework\guides";
            var entries = new List<GuideEntry>
            {
                new("g1", "Гейм-дизайн", GuideKindEnum.Guide, "Методические указания по курсовой.docx", folder + @"\Методические указания по курсовой.docx", string.Empty, string.Empty),
                new("g2", "Гейм-дизайн", GuideKindEnum.Guide, "Требования к оформлению", @"E:\Учёба\Требования", string.Empty, string.Empty, false),
                new("g3", "Гейм-дизайн", GuideKindEnum.Guide, "drive.google.com/file/d/abc", string.Empty, "https://drive.google.com/file/d/abc/view", "Ссылка ведёт на страницу, а не на файл. Скачай файл и добавь его кнопкой «Обзор»"),
                new("e1", "Гейм-дизайн", GuideKindEnum.Example, "Мой образец отчёта.docx", folder + @"\Мой образец отчёта.docx", string.Empty, string.Empty),
                new("sample:gd-1015:1", "Гейм-дизайн", GuideKindEnum.Example, "gd-1015-1.docx", @"D:\data\homework\samples\Гейм-дизайн\gd-1015-1.docx", string.Empty, string.Empty),
                new("sample:db-1022:2", "Базы данных", GuideKindEnum.Example, "db-1022-2.pptx", @"D:\data\homework\samples\Базы данных\db-1022-2.pptx", string.Empty, string.Empty, false, Manual: true),
            };

            // Замечания: два предмета, общее, выключенное и одно длинное, чтобы проверить перенос
            var remarks = new List<Remark>
            {
                new("r1", new DateOnly(2026, 10, 18), "Гейм-дизайн", "gd-1015", 1, "Больше примеров из настоящих игр, а не общих слов", RemarkKindEnum.Content, false),
                new("r2", new DateOnly(2026, 10, 22), "Базы данных", "db-1022", 0, "Шрифт Times New Roman 14, поля по ГОСТ", RemarkKindEnum.Format, false, Enabled: false),
                new("r3", new DateOnly(2026, 10, 25), "Гейм-дизайн", "gd-1025", 0, "Без канцелярита и вводных фраз вроде «в современном мире»: пиши коротко и по делу, как в хорошем аналитическом отчёте", RemarkKindEnum.Style, true),
                new("r4", new DateOnly(2026, 10, 27), "Базы данных", "db-1027", 2, "Верное условие задания: нормализовать схему до 3НФ и показать шаги", RemarkKindEnum.Condition, false),
            };
            var model = new GuidesDialogViewModel("Гейм-дизайн", _ => entries, folder, _ => { }, _ => { }, (_, _) => { }, (_, _, _) => string.Empty, (_, _, _) => string.Empty, (_, _, _) => Task.FromResult(string.Empty),
                new RemarkHooks(() => remarks, _ => { }, _ => { }));
            if (parts.Length > 1 && parts[1].Equals("examples", StringComparison.OrdinalIgnoreCase))
                model.ShowExamplesCommand.Execute(null);

            if (parts.Length > 1 && parts[1].Equals("remarks", StringComparison.OrdinalIgnoreCase))
            {
                // Замечания читаются в фоне: окно ждёт их, прокачивая очередь, а не блокируя поток
                model.ShowRemarksCommand.Execute(null);
                settleSeconds = 1.0;
            }

            window = new GuidesWindow(model) { WindowStartupLocation = WindowStartupLocation.Manual };

            // На снимке замечаний окно выше, чтобы были видны все строки, в том числе выключенная
            if (parts.Length > 1 && parts[1].Equals("remarks", StringComparison.OrdinalIgnoreCase))
                window.Height = 860;
        }
        else if (string.Equals(parts[0], "menu", StringComparison.OrdinalIgnoreCase))
        {
            window = CardMenuSheet();
        }
        else if (string.Equals(parts[0], "redo", StringComparison.OrdinalIgnoreCase))
        {
            window = new RedoWindow("25.10.2026  ·  Лекция 3  ·  2 ч 43 мин") { WindowStartupLocation = WindowStartupLocation.Manual };
        }
        else if (string.Equals(parts[0], "prepare", StringComparison.OrdinalIgnoreCase))
        {
            // Окно «Подготовить задание» на примерных материалах
            var folder = @"D:\data\homework\guides";
            var materials = new List<GuideEntry>
            {
                new("g1", "Гейм-дизайн", GuideKindEnum.Guide, "Методические указания по курсовой.docx", folder + @"\a.docx", string.Empty, string.Empty),
                new("g2", "Гейм-дизайн", GuideKindEnum.Guide, "Требования к оформлению", @"E:\Требования", string.Empty, string.Empty, false),
                new("e1", "Гейм-дизайн", GuideKindEnum.Example, "Мой образец отчёта.docx", folder + @"\b.docx", string.Empty, string.Empty),
            };
            window = new PrepareWindow(new PrepareDialogViewModel("25.10.2026  ·  Лекция 3  ·  2 ч 43 мин", HomeworkFormatEnum.Word, materials, folder) { Extra = "Сделай упор на сравнение двух игр" }) { WindowStartupLocation = WindowStartupLocation.Manual };
        }
        else
        {
            var main = new MainWindow();
            main.Apply(snapshot, jobs, SampleRecords(now), ["Задача «reels»: путь содержит {repo}, а корень репозитория не найден"], SampleIdeas(now));
            main.ApplyHomework(SampleHomework());
            main.Model.ApplyChips(
                [new StatusChipViewModel("Файл настроек", "найден", PanelToneEnum.Running), new StatusChipViewModel("Пароль приложения", "задан", PanelToneEnum.Running), new StatusChipViewModel("Токен Singularity", "не задан", PanelToneEnum.Attention)],
                [new StatusChipViewModel("Токен бота", "задан", PanelToneEnum.Running), new StatusChipViewModel("Вход в Instagram", "firefox", PanelToneEnum.Running)],
                [new StatusChipViewModel("Вход в LMS", "задан", PanelToneEnum.Running), new StatusChipViewModel("Вход в MTS Link", "сохранён 27.10", PanelToneEnum.Running), new StatusChipViewModel("Запись в Singularity", "выключена", PanelToneEnum.Attention)]);
            main.Model.Formats = new HomeworkFormatsViewModel(SampleLessons(), (_, _) => { }, lesson => lesson.Id != "en-1020", (_, _) => Task.FromResult((0, string.Empty)), (_, _) => { }, title => title == "Гейм-дизайн" ? LessonStateEnum.Fetch : LessonStateEnum.Undecided, _ => { }, (_, _) => { });
            main.Model.Formats.SubjectOptionsReader = subject => subject == "Гейм-дизайн" ? new SubjectOptions(true, TaskSourceEnum.Transcript) : new SubjectOptions();
            main.Model.Formats.SubjectOptionsSaver = (_, _) => { };
            if (parts.Length > 1 && parts[1].ToLowerInvariant() is "homework" or "transcripts" or "schedule")
                settleSeconds = 1.0;

            if (parts.Length > 1 && Enum.TryParse<AppPageEnum>(parts[1], ignoreCase: true, out var page))
                main.Model.Page = page;

            // «main:journal:open»: все разделы журнала раскрыты
            if (parts.Length > 2 && string.Equals(parts[2], "open", StringComparison.OrdinalIgnoreCase))
            {
                main.Height = 1700;
                foreach (var group in main.Model.Groups)
                    group.IsOpen = true;

                settleSeconds = 1.0;
            }

            // «main:transcripts:read» и «main:transcripts:docx|pptx|code|md»: открыта расшифровка или предпросмотр готового файла
            if (parts.Length > 2 && parts[1].Equals("transcripts", StringComparison.OrdinalIgnoreCase) && main.Model.Formats is { } education)
            {
                var drafts = SampleDrafts();
                education.TranscriptReader = _ => "Занятие: Гейм-дизайн, Лекция 1\nДата: 07.09.2026\n\n== Резюме ==\nТема встречи: Обсуждение курса по геймдизайну\n\nКраткое содержание:\nУчастники познакомились и обсудили содержание курса.\n\nКлючевые моменты:\n• Знакомство участников\n• Системность и реализация идей\n\n== Расшифровка ==\n[00:00] Преподаватель: Добрый день, начинаем.\n[00:12] Студент: Слышно хорошо.\n[00:30] Преподаватель: Сегодня разберём игровые циклы и баланс.";
                // Подробный разбор с темами: без него читалка не показывает кнопку «Скопировать конспект к практике»
                education.AnalysisReader = _ => new LessonAnalysis("Игровые циклы и баланс", "Обсудили, из чего складывается игровой цикл и как держать баланс.", ["Игровой цикл", "Баланс"], [new AnalysisTopic("Игровой цикл", "Действие, награда, новое действие"), new AnalysisTopic("Баланс", "Согласованность ресурсов и сложности")], ["Баланс — это согласованность"], [], [new AnalysisTask("Выбрать игру для отчёта", "02.11")]);
                var firstId = SampleLessons().Select(item => item.Lesson.Id).First();
                education.DraftLister = id => id == firstId ? [drafts[0]] : [];
                education.ItemsReader = id => id == firstId
                    ? [(1, new HomeworkItem("Выбрать игру и написать по ней аналитический отчёт", new DateOnly(2026, 11, 2), "Объём 1-2 страницы. Разобрать правила, конфликт, агентность, обратную связь и место игры в истории.", string.Empty), false)]
                    : [(1, new HomeworkItem("Собрать главное меню в Unity", new DateOnly(2026, 11, 5), "Первый этап практики: меню с кнопками и окном настроек звука.", string.Empty), false), (2, new HomeworkItem("Сделать второй этап: привязать UI к коду", null, string.Empty, string.Empty, true), true)];
                var sure = new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);
                education.PipelineReader = lesson => lesson.Id == firstId
                    ? new Dictionary<int, ItemPipelineInfo> { [1] = new(PipelineStepEnum.Ready, drafts[0], sure, false, false, string.Empty) }
                    : new Dictionary<int, ItemPipelineInfo> { [1] = new(PipelineStepEnum.Found, string.Empty, sure, false, false, "Работа подготовится сама при ближайшей выгрузке"), [2] = new(PipelineStepEnum.Submitted, string.Empty, sure, false, false, string.Empty) };
                education.GuideFolder = @"D:\data\homework\guides";
                education.GuideLister = _ =>
                [
                    new GuideEntry("g1", "Гейм-дизайн", GuideKindEnum.Guide, "Методические указания по курсовой.docx", @"D:\data\homework\guides\a.docx", string.Empty, string.Empty),
                    new GuideEntry("g2", "Гейм-дизайн", GuideKindEnum.Guide, "Требования", @"E:\Учёба", string.Empty, string.Empty),
                    new GuideEntry("e1", "Гейм-дизайн", GuideKindEnum.Example, "Мой образец отчёта.docx", @"D:\data\homework\guides\b.docx", string.Empty, string.Empty),
                ];
                education.Update(SampleLessons());
                var chosen = education.Subjects.First(item => item.Title == "Гейм-дизайн");
                chosen.SelectCommand.Execute(null);
                var first = chosen.Lessons.First(lesson => lesson.HasTranscript);
                if (parts[2].Equals("read", StringComparison.OrdinalIgnoreCase))
                {
                    first.OpenCommand.Execute(null);
                }
                else if (parts[2].Equals("settings", StringComparison.OrdinalIgnoreCase))
                {
                    // «main:transcripts:settings»: страница настроек «Автоматизации образования»
                    main.Model.ToggleSettingsCommand.Execute(null);
                }
                else
                {
                    education.ShowHomeworkCommand.Execute(null);
                    var wanted = parts[2].ToLowerInvariant() switch { "docx" => ".docx", "pptx" => ".pptx", "code" => ".py", _ => ".md" };
                    chosen.Lessons.SelectMany(lesson => lesson.Drafts).First(draft => draft.Path.EndsWith(wanted, StringComparison.OrdinalIgnoreCase)).OpenCommand.Execute(null);
                }

                settleSeconds = 1.0;
            }

            // «card:found|confirm|ready|checked|submitted»: карточка задания на вкладке «Домашние задания» в одном шаге конвейера
            if (string.Equals(parts[0], "card", StringComparison.OrdinalIgnoreCase) && main.Model.Formats is { } cards)
            {
                ShowCard(cards, parts.Length > 1 ? parts[1].ToLowerInvariant() : "found");
                main.Model.Page = AppPageEnum.Transcripts;
                main.Height = 1250;
                settleSeconds = 1.0;
            }

            // «main:done»: задача «telegram» только что закончилась, на её карточке рисуется галочка
            if (parts.Length > 1 && string.Equals(parts[1], "done", StringComparison.OrdinalIgnoreCase))
            {
                main.Apply(snapshot, [.. jobs.Select(job => job.Id == "telegram" ? job with { Tone = RowToneEnum.Ok, Headline = "Выполнено только что" } : job)], SampleRecords(now), [], SampleIdeas(now));
                settleSeconds = 1.2;
            }

            // «main:progress»: «Автоматизация образования» работает, на карточке видны шаги запуска и что обрабатывается сейчас
            if (parts.Length > 1 && string.Equals(parts[1], "progress", StringComparison.OrdinalIgnoreCase))
            {
                main.Height = 1300;
                var started = DateTimeOffset.Now.AddMinutes(-4);
                main.Model.ProgressReader = id => id == "lms"
                    ? new Steward.Status.Progress.RunProgress(["Занятия", "Расшифровки", "Разбор", "Задания", "Работы", "Заметки"], 2, 1, 3, "Гейм-дизайн, 25.10.2026", started, DateTimeOffset.Now)
                    : null;
                main.Apply(snapshot, [.. jobs.Select(job => job.Id == "lms" ? job with { Tone = RowToneEnum.Working, Headline = "Выполняется", Detail = "Запущена сегодня в 10:41" } : job)], SampleRecords(now), [], SampleIdeas(now));
                settleSeconds = 1.2;
            }

            // «maximized:1920x1080[@125][:страница]»: развёрнутое окно на мониторе такого размера и масштаба с панелью задач внизу.
            // Окно ставится так, как Windows ставит развёрнутое окно без рамки без поправки (шире рабочей области на рамку с каждой стороны),
            // содержимое отступает на рассчитанные отступы, а на снимок попадает только рабочая область, то есть то, что видно на экране
            if (string.Equals(parts[0], "maximized", StringComparison.OrdinalIgnoreCase))
            {
                var (monitorWidth, monitorHeight, scale) = ParseMonitor(parts.Length > 1 ? parts[1] : "1920x1080");
                if (parts.Length > 2 && Enum.TryParse<AppPageEnum>(parts[2], ignoreCase: true, out var shown))
                    main.Model.Page = shown;

                // «…:transcripts:settings»: открыты настройки процесса на выдуманных файлах во временной папке, без настоящих секретов
                if (parts.Length > 3 && parts[3].Equals("settings", StringComparison.OrdinalIgnoreCase))
                {
                    var root = Path.Combine(Path.GetTempPath(), "steward-sample-settings");
                    Directory.CreateDirectory(Path.GetDirectoryName(Steward.Hosting.SettingsProbe.LmsFile(root))!);
                    File.WriteAllText(Steward.Hosting.SettingsProbe.LmsFile(root), """{"Login": "student@example.com", "Password": "<пароль от LMS>", "StudentName": "Иванов Иван", "MaxFetchPerRun": 6, "CreateTasks": false}""");
                    main.Model.AttachSettings(root, () => { });
                    main.Model.IsSettingsOpen = true;
                }

                var work = new Int32Rect(0, 0, monitorWidth, monitorHeight - (int)Math.Round(TASKBAR_PIXELS * scale));
                var border = (int)Math.Round(FRAME_PIXELS * scale);
                var placed = new Int32Rect(-border, -border, work.Width + (2 * border), work.Height + (2 * border));
                var insets = WindowFit.MaximizedInsets(placed, work, scale, scale);
                main.WorkAreaOverride = new Rect(0, 0, work.Width / scale, work.Height / scale);
                main.ApplyFrame(true, insets);
                main.Width = placed.Width / scale;
                main.Height = placed.Height / scale;
                target = new System.Windows.Size(placed.Width / scale, placed.Height / scale);
                crop = new Rect(insets.Left, insets.Top, work.Width / scale, work.Height / scale);
                settleSeconds = 1.0;
            }

            window = main;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.ShowInTaskbar = false;
        window.Show();
        window.UpdateLayout();
        afterShow?.Invoke();
        window.UpdateLayout();
        if (settleSeconds > 0)
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            var until = DateTime.UtcNow.AddSeconds(settleSeconds);
            while (DateTime.UtcNow < until)
            {
                window.Dispatcher.Invoke(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.Render);
                Thread.Sleep(16);
            }

            Redraw(window);
            window.UpdateLayout();
        }

        // Страница дизайн-системы выше экрана, а окно Windows выше экрана не растёт, поэтому рисуем её содержимое целиком
        Visual source = window;
        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);
        if (window is DesignWindow { Content: FrameworkElement content })
        {
            content.Measure(new System.Windows.Size(width, double.PositiveInfinity));
            content.Arrange(new Rect(0, 0, width, content.DesiredSize.Height));
            source = content;
            height = (int)Math.Ceiling(content.DesiredSize.Height);
        }

        // Развёрнутое окно на мониторе больше настоящего: Windows не даёт окну вырасти больше экрана, поэтому содержимое раскладывается по нужному размеру само
        if (target is { } maximizedSize && window is MainWindow { Content: FrameworkElement frame })
        {
            width = (int)Math.Ceiling(maximizedSize.Width);
            height = (int)Math.Ceiling(maximizedSize.Height);
            frame.Measure(new System.Windows.Size(width, height));
            frame.Arrange(new Rect(0, 0, width, height));
            frame.UpdateLayout();
            source = frame;
        }

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.FindResource("ShadowColor")), null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(source), null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        BitmapSource image = bitmap;
        if (crop is { } part)
        {
            var x = Math.Clamp((int)Math.Round(part.X), 0, width - 1);
            var y = Math.Clamp((int)Math.Round(part.Y), 0, height - 1);
            image = new CroppedBitmap(bitmap, new Int32Rect(x, y, Math.Min((int)Math.Round(part.Width), width - x), Math.Min((int)Math.Round(part.Height), height - y)));
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var stream = File.Create(path))
            encoder.Save(stream);

        window.Close();
        return true;
    }

    /// <summary>
    /// Готовит вкладку «Домашние задания» с карточкой задания в нужном шаге: у первого занятия «Гейм-дизайна» одно задание в этом шаге, у второго задание без вопроса
    /// </summary>
    /// <param name="education">Модель вкладки</param>
    /// <param name="state">Шаг: <c>found</c> (средняя уверенность), <c>confirm</c> (нужно подтвердить), <c>ready</c>, <c>checked</c> или <c>submitted</c></param>
    private static void ShowCard(HomeworkFormatsViewModel education, string state)
    {
        var drafts = SampleDrafts();
        var work = drafts[0];
        var lessons = SampleLessons();
        var firstId = lessons[0].Lesson.Id;
        var secondId = lessons[1].Lesson.Id;
        const string quote = "к следующему занятию выберите любую игру и напишите по ней аналитический отчёт на одну-две страницы";
        var ready = new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);
        var (item, info) = state switch
        {
            "confirm" => (
                new HomeworkItem("Сделать прототип меню в Figma", null, "Три экрана: главное меню, настройки, пауза.", string.Empty, Evidence: "покажите прототип меню", Confidence: "low", Doubt: "говорили про курс по UX, а не про Гейм-дизайн"),
                new ItemPipelineInfo(PipelineStepEnum.Found, string.Empty, new ItemAssessment(ItemReadinessEnum.NeedsConfirmation, "Не уверен, что правильно понял задание «Сделать прототип меню в Figma» (говорили про курс по UX, а не про Гейм-дизайн)"), false, false, string.Empty)),
            "ready" => (
                new HomeworkItem("Выбрать игру и написать по ней аналитический отчёт", new DateOnly(2026, 11, 2), "Объём 1-2 страницы. Разобрать правила, конфликт, агентность и обратную связь.", string.Empty, ActivityId: "4211", Evidence: quote, Confidence: "high"),
                new ItemPipelineInfo(PipelineStepEnum.Ready, work, ready, true, true, string.Empty)),
            "checked" => (
                new HomeworkItem("Выбрать игру и написать по ней аналитический отчёт", new DateOnly(2026, 11, 2), "Объём 1-2 страницы. Разобрать правила, конфликт, агентность и обратную связь.", string.Empty, ActivityId: "4211", Evidence: quote, Confidence: "high"),
                new ItemPipelineInfo(PipelineStepEnum.Checked, work, ready, true, true, string.Empty)),
            "submitted" => (
                new HomeworkItem("Выбрать игру и написать по ней аналитический отчёт", new DateOnly(2026, 11, 2), "Объём 1-2 страницы. Разобрать правила, конфликт, агентность и обратную связь.", string.Empty, true, "4211", Submitted: true, Evidence: quote, Confidence: "high"),
                new ItemPipelineInfo(PipelineStepEnum.Submitted, work, ready, true, true, string.Empty)),
            _ => (
                new HomeworkItem("Выбрать игру и написать по ней аналитический отчёт", new DateOnly(2026, 11, 2), "Объём 1-2 страницы. Разобрать правила, конфликт, агентность и обратную связь.", string.Empty, Evidence: quote, Confidence: "medium", Doubt: "срок назван устно, в LMS его нет"),
                new ItemPipelineInfo(PipelineStepEnum.Found, string.Empty, new ItemAssessment(ItemReadinessEnum.ReadyWithNote, "срок назван устно, в LMS его нет"), true, false, "Работа подготовится сама при ближайшей выгрузке")),
        };
        var other = new HomeworkItem("Собрать главное меню в Unity", new DateOnly(2026, 11, 5), "Первый этап практики: меню с кнопками и окном настроек звука.", string.Empty);
        var otherInfo = new ItemPipelineInfo(PipelineStepEnum.Found, string.Empty, ready, false, false, "Работа подготовится сама при ближайшей выгрузке");

        education.DraftLister = id => id == firstId && info.WorkPath.Length > 0 ? [work] : [];
        education.CheckReader = id => id == firstId && info.Step == PipelineStepEnum.Checked ? work : string.Empty;
        education.ItemsReader = id => id == firstId ? [(1, item, false)] : id == secondId ? [(1, other, false)] : [];
        education.PipelineReader = lesson => lesson.Id == firstId ? new Dictionary<int, ItemPipelineInfo> { [1] = info } : new Dictionary<int, ItemPipelineInfo> { [1] = otherInfo };
        education.ItemConfirmer = (_, _) => { };
        education.GuideFolder = @"D:\data\homework\guides";
        education.GuideLister = _ => [];
        education.Update(lessons);
        var chosen = education.Subjects.First(subject => subject.Title == "Гейм-дизайн");
        chosen.SelectCommand.Execute(null);
        education.ShowHomeworkCommand.Execute(null);
    }

    /// <summary>
    /// Собирает лист с меню «…» карточки задания: всплывающее окно меню на снимок не попадает, поэтому меню рисуется прямо в окне. Пункты без готовой работы недоступны, как у задания на шаге «Найдено»
    /// </summary>
    /// <returns>Окно с меню</returns>
    private static Window CardMenuSheet()
    {
        var resources = System.Windows.Application.Current.Resources;

        // Контекстное меню не может стоять в окне, поэтому его рамка повторена вокруг обычного меню с теми же пунктами
        var menu = new System.Windows.Controls.Menu
        {
            Background = System.Windows.Media.Brushes.Transparent,
            ItemContainerStyle = (Style)resources["CardMenuItem"],
            ItemsPanel = new System.Windows.Controls.ItemsPanelTemplate(new FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel))),
        };
        var disabled = new HashSet<string>(StringComparer.Ordinal) { "Открыть работу", "Проверить заново", "Результат проверки", "Переделать", "Указать файл сданной работы", "Удалить готовое" };
        string?[] entries = ["Открыть работу", "Отметить сданным", null, "Открыть в LMS", "Скопировать условие", null, "Проверить заново", "Результат проверки", "Переделать", null, "Прикрепить готовую работу", "Указать файл сданной работы", null, "В Singularity", "В архив", "Удалить готовое"];
        foreach (var entry in entries)
        {
            if (entry is null)
                menu.Items.Add(new System.Windows.Controls.Separator { Style = (Style)resources["CardMenuSeparator"] });
            else
                menu.Items.Add(new System.Windows.Controls.MenuItem { Header = entry, IsEnabled = !disabled.Contains(entry) });
        }

        var host = new System.Windows.Controls.Border
        {
            Margin = new Thickness(24),
            Padding = new Thickness(4),
            MinWidth = 220,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = (System.Windows.Media.Brush)resources["RaisedBrush"],
            BorderBrush = (System.Windows.Media.Brush)resources["LineStrongBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)resources["RadiusSmall"],
            Child = menu,
        };

        return new Window { Width = 320, Height = 580, WindowStyle = WindowStyle.None, Background = (System.Windows.Media.Brush)resources["WindowBrush"], Content = host };
    }

    /// <summary>
    /// Собирает лист с ползунком в покое и в движении: бегунок догоняет значение, а хвост отстаёт
    /// </summary>
    /// <returns>Окно с образцами</returns>
    private static Window SliderSheet()
    {
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(24), Width = 420 };
        foreach (var (value, thumb, tail) in new[] { (60.0, 230.0, 230.0), (60.0, 250.0, 215.0), (60.0, 250.0, 190.0), (60.0, 250.0, 160.0), (60.0, 180.0, 215.0) })
        {
            var bar = new Controls.IntervalBar { Value = value };
            panel.Children.Add(bar);
            bar.Measure(new System.Windows.Size(420, 32));
            bar.Arrange(new Rect(0, 0, 420, 32));
            bar.Pose(thumb, tail);
        }

        return new Window { Width = 470, Height = 240, WindowStyle = WindowStyle.None, Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["WindowBrush"], Content = panel };
    }

    /// <summary>
    /// Собирает лист с иконками трея для каждого состояния в натуральную величину и вдвое крупнее
    /// </summary>
    /// <returns>Окно с иконками</returns>
    private static Window IconSheet()
    {
        var resources = System.Windows.Application.Current.Resources;
        var states = new (ActivityKindEnum Kind, string Brush)[]
        {
            (ActivityKindEnum.Alive, "OkBrush"),
            (ActivityKindEnum.Working, "WorkingBrush"),
            (ActivityKindEnum.Attention, "AttentionBrush"),
            (ActivityKindEnum.Failed, "DangerBrush"),
            (ActivityKindEnum.Dormant, "IdleBrush"),
        };
        var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(16) };

        foreach (var (kind, key) in states)
        {
            using var icon = TrayOrbIcon.Render(kind, (System.Windows.Media.Brush)resources[key]);
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            foreach (var size in new[] { 16, 32, 64 })
                panel.Children.Add(new System.Windows.Controls.Image { Source = image, Width = size, Height = size, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });

            panel.Children.Add(new System.Windows.Controls.Border { Width = 1, Height = 48, Background = (System.Windows.Media.Brush)resources["LineBrush"], Margin = new Thickness(10, 0, 10, 0) });
        }

        return new Window { Width = 1100, Height = 100, WindowStyle = WindowStyle.None, Background = (System.Windows.Media.Brush)resources["WindowBrush"], Content = panel };
    }

    /// <summary>
    /// Собирает лист с образцами сфер: виды состояния и три способа показать завершение в пяти моментах
    /// </summary>
    /// <returns>Окно с образцами</returns>
    private static Window OrbSheet()
    {
        var resources = System.Windows.Application.Current.Resources;
        System.Windows.Media.Brush Brush(string key) => (System.Windows.Media.Brush)resources[key];

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(24) };
        var kinds = new (ActivityKindEnum Kind, string Brush, string Label)[]
        {
            (ActivityKindEnum.Alive, "OkBrush", "Работает"),
            (ActivityKindEnum.Calm, "OkBrush", "Выполнено"),
            (ActivityKindEnum.Dormant, "IdleBrush", "Отключена"),
            (ActivityKindEnum.Attention, "AttentionBrush", "Ждёт тебя"),
            (ActivityKindEnum.Failed, "DangerBrush", "Не удалось"),
            (ActivityKindEnum.Beacon, "OkBrush", "Общий статус"),
            (ActivityKindEnum.Working, "WorkingBrush", "Выполняется"),
            (ActivityKindEnum.Waiting, "OkBrush", "Ждёт срока"),
            (ActivityKindEnum.Sealed, "OkBrush", "Журнал: успех"),
            (ActivityKindEnum.Shattered, "DangerBrush", "Журнал: ошибка"),
            (ActivityKindEnum.Sweep, "AttentionBrush", "Журнал: время"),
            (ActivityKindEnum.Hollow, "IdleBrush", "Журнал: не запущена"),
        };
        var ages = new[] { 0.12, 0.4, 0.8, 1.2, 1.7 };
        var styles = new[] { (ActivityDoneStyleEnum.Ripple, "А. Волна"), (ActivityDoneStyleEnum.DotTick, "Б. Галочка из точек"), (ActivityDoneStyleEnum.Burst, "В. Вспышка") };

        for (var row = 0; row < 2 + styles.Length; row++)
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

        for (var column = 0; column < 8; column++)
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        System.Windows.Controls.StackPanel Cell(UIElement orb, string label)
        {
            var cell = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 20, 14), HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
            cell.Children.Add(orb);
            cell.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Foreground = Brush("MutedBrush"), FontSize = 12, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });

            return cell;
        }

        for (var index = 0; index < kinds.Length; index++)
        {
            var orb = new Controls.ActivityOrb { Width = 72, Height = 72, Kind = kinds[index].Kind, Foreground = Brush(kinds[index].Brush) };
            var cell = Cell(orb, kinds[index].Label);
            System.Windows.Controls.Grid.SetRow(cell, index / 6);
            System.Windows.Controls.Grid.SetColumn(cell, index % 6);
            grid.Children.Add(cell);
        }

        for (var row = 0; row < styles.Length; row++)
        {
            for (var column = 0; column < ages.Length; column++)
            {
                var orb = new Controls.ActivityOrb
                {
                    Width = 72,
                    Height = 72,
                    State = ActivityStateEnum.Done,
                    Kind = ActivityKindEnum.Calm,
                    DoneStyle = styles[row].Item1,
                    Foreground = Brush("OkBrush"),
                    DoneBrush = Brush("OkBrush"),
                    Since = DateTimeOffset.UtcNow.AddSeconds(-ages[column]),
                };
                var cell = Cell(orb, column == 0 ? $"{styles[row].Item2}  {ages[column]:0.0} с" : $"{ages[column]:0.0} с");
                System.Windows.Controls.Grid.SetRow(cell, row + 2);
                System.Windows.Controls.Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }

        return new Window
        {
            Width = 800,
            Height = 700,
            WindowStyle = WindowStyle.None,
            Background = Brush("WindowBrush"),
            Content = grid,
        };
    }

    /// <summary>
    /// Перерисовывает все индикаторы процесса: без показа на экране кадры не идут, а рисунок зависит от времени
    /// </summary>
    /// <param name="element">Корень поддерева</param>
    private static void Redraw(DependencyObject element)
    {
        if (element is UIElement orb and Controls.ActivityOrb)
            orb.InvalidateVisual();

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            Redraw(VisualTreeHelper.GetChild(element, index));
    }

    /// <summary>
    /// Выдуманное состояние контейнера: работает, одна правка в журнале
    /// </summary>
    /// <param name="now">Текущий момент</param>
    /// <returns>Снимок для рисунка</returns>
    private static SyncSnapshot SampleSnapshot(DateTimeOffset now) => new()
    {
        DockerAvailable = true,
        ContainerRunning = true,
        LastSuccess = now.AddMinutes(-3),
        Entries =
        [
            new LogEntry(now.AddMinutes(-3), LogToneEnum.Quiet, "Всё совпало", "Новых правок в таблице нет", 4, TimeSpan.FromSeconds(2)),
            new LogEntry(now.AddMinutes(-40), LogToneEnum.Update, "Появились правки", "Убрано из таблицы 30.10 в 20:10\nОбновлено «Гейм-дизайн»", 1),
        ],
    };

    /// <summary>
    /// Выдуманные задачи: по одной на каждый цвет
    /// </summary>
    /// <returns>Строки задач</returns>
    private static IReadOnlyList<JobRow> SampleJobs() =>
    [
        new JobRow("mail", "Почта", "Выполнено сегодня в 09:00", "Новых важных писем нет\nСледующий запуск сегодня в 19:00", RowToneEnum.Ok, 0, true),
        new JobRow("reels", "Рилсы", "Ждёт тебя", "Разобрано 3 рилса\nБез решения: 3", RowToneEnum.Attention, 3, true),
        new JobRow("mts", "MTS Link", "Не удалось", "Нет доступа к LMS: войдите заново (вчера в 19:00)", RowToneEnum.Problem, 0, true),
        new JobRow("telegram", "Telegram-бот", "Выполняется", "Запущена сегодня в 12:00", RowToneEnum.Working, 0, true),
        new JobRow("lms", "Автоматизация образования", "Выполнено сегодня в 08:00", "Расшифровки: получено 2 из 2", RowToneEnum.Ok, 0, true),
    ];

    /// <summary>
    /// Выдуманные занятия с выбранными форматами ДЗ
    /// </summary>
    /// <returns>Занятия от свежих к старым</returns>
    /// <summary>
    /// Создаёт во временной папке по одному готовому файлу каждого формата: Word, презентацию, Markdown и код
    /// </summary>
    /// <returns>Пути к файлам</returns>
    private static IReadOnlyList<string> SampleDrafts()
    {
        var folder = Path.Combine(Path.GetTempPath(), "steward-sample-drafts");
        Directory.CreateDirectory(folder);

        const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        var docx = Path.Combine(folder, "Задание 1. Игровые циклы.docx");
        File.Delete(docx);
        using (var archive = System.IO.Compression.ZipFile.Open(docx, System.IO.Compression.ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open()))
        {
            writer.Write($"<w:document {W}><w:body><w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>Игровые циклы</w:t></w:r></w:p><w:p><w:r><w:t>Опишите основной цикл вашей игры: </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>действие, награда, прогресс</w:t></w:r><w:r><w:t>.</w:t></w:r></w:p><w:p><w:pPr><w:numPr/></w:pPr><w:r><w:t>Объём: одна страница</w:t></w:r></w:p><w:p><w:pPr><w:numPr/></w:pPr><w:r><w:t>Срок: 23.10</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Этап</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Что делает игрок</w:t></w:r></w:p></w:tc></w:tr><w:tr><w:tc><w:p><w:r><w:t>1</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Исследует уровень</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:body></w:document>");
        }

        const string P = "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"";
        var pptx = Path.Combine(folder, "Презентация. Баланс.pptx");
        File.Delete(pptx);
        using (var archive = System.IO.Compression.ZipFile.Open(pptx, System.IO.Compression.ZipArchiveMode.Create))
        {
            for (var i = 1; i <= 2; i++)
            {
                using var writer = new StreamWriter(archive.CreateEntry($"ppt/slides/slide{i}.xml").Open());
                writer.Write($"<p:sld {P}><p:cSld><p:spTree><p:sp><p:nvSpPr><p:nvPr><p:ph type=\"title\"/></p:nvPr></p:nvSpPr><p:txBody><a:p><a:r><a:t>Баланс игры, часть {i}</a:t></a:r></a:p></p:txBody></p:sp><p:sp><p:txBody><a:p><a:r><a:t>Кривая сложности растёт плавно</a:t></a:r></a:p><a:p><a:r><a:t>Награда соразмерна усилию</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>");
            }
        }

        var py = Path.Combine(folder, "balance.py");
        File.WriteAllText(py, "# Расчёт баланса\ndef reward(level, effort=1.5):\n    \"\"\"Награда растёт с уровнем\"\"\"\n    if level > 10:\n        return level * effort\n    return 5  # базовая награда\n");
        var md = Path.Combine(folder, "plan.md");
        File.WriteAllText(md, "# План работы\n\n## Шаги\n\n- Выбрать **жанр** игры\n- Описать цикл `core loop`\n1. Собрать прототип\n\n```csharp\nvar level = 1;\n```\n");

        return [docx, pptx, py, md];
    }

    private static IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> SampleLessons() =>
    [
        (new Lesson("ux-1025", "Гейм-дизайн", new DateOnly(2026, 10, 25), "Лекция 3", string.Empty, 163), HomeworkFormatEnum.Presentation),
        (new Lesson("ux-1018", "Гейм-дизайн", new DateOnly(2026, 10, 18), "Лекция 2", string.Empty, 118), HomeworkFormatEnum.Word),
        (new Lesson("db-1022", "Базы данных", new DateOnly(2026, 10, 22), "Нормализация", string.Empty, 90), HomeworkFormatEnum.Word),
        (new Lesson("en-1020", "Английский язык", new DateOnly(2026, 10, 20), "Эссе", string.Empty, 87), HomeworkFormatEnum.Skip),
    ];

    /// <summary>
    /// Выдуманные найденные задания: создана, ждёт и не готовить
    /// </summary>
    /// <returns>Задания от свежих занятий к старым</returns>
    private static IReadOnlyList<HomeworkRow> SampleHomework()
    {
        var game = new Lesson("ux-1025", "Гейм-дизайн: игровые механики", new DateOnly(2026, 10, 25));
        var db = new Lesson("db-1022", "Базы данных: нормализация", new DateOnly(2026, 10, 22));
        var english = new Lesson("en-1020", "Английский: эссе", new DateOnly(2026, 10, 20));

        return
        [
            new HomeworkRow(game, HomeworkFormatEnum.Presentation, new HomeworkItem("Описать три игровые механики и показать их в презентации", new DateOnly(2026, 11, 1), "Не больше десяти слайдов, на каждом механика, пример из игры и вывод.", ""), 1, false),
            new HomeworkRow(db, HomeworkFormatEnum.Word, new HomeworkItem("Привести схему интернет-магазина к третьей нормальной форме", new DateOnly(2026, 10, 30), "Диаграмма и пояснение на двух страницах. Сдавать в LMS.", "https://lms.example/1"), 1, true),
            new HomeworkRow(english, HomeworkFormatEnum.Skip, new HomeworkItem("Эссе о любимой книге", null, "", ""), 1, false),
        ];
    }

    /// <summary>
    /// Выдуманные идеи из рилсов
    /// </summary>
    /// <param name="now">Текущий момент</param>
    /// <returns>Идеи от свежих к старым</returns>
    private static IReadOnlyList<Idea> SampleIdeas(DateTimeOffset now) =>
    [
        new Idea { Id = "r1-1", CreatedAt = now.AddMinutes(-12), SourceUrl = "https://www.instagram.com/reel/sample/", Title = "Хранить конспекты лекций в одном поиске", Author = "@study.tools", Summary = "Суть: все конспекты лекций складываются в одну папку и прогоняются через расшифровку, поиск идёт по ключевым словам.\nДля чего: быстро находить нужное место перед экзаменом, не листая файлы.\nПлюсы: бесплатно; работает с тем, что уже есть.\nМинусы и риски: длинные расшифровки занимают место; поиск по ключевым словам пропускает смысл без слов.\nВывод: стоит попробовать на одном предмете.", Transcript = "Собери все конспекты в одну папку, прогони через расшифровку и ищи по ключевым словам вместо того, чтобы листать файлы. Я так готовлюсь к экзаменам уже второй семестр и экономлю кучу времени перед сессией." },
        new Idea { Id = "r1-2", CreatedAt = now.AddHours(-5), SourceUrl = "https://www.instagram.com/reel/sample2/", Title = "Напоминание о дедлайне за день", Author = "@productivity", Caption = "Настрой напоминание за сутки до сдачи" },
    ];

    /// <summary>
    /// Выдуманный журнал запусков
    /// </summary>
    /// <param name="now">Текущий момент</param>
    /// <returns>Записи от новых к старым</returns>
    private static IReadOnlyList<RunRecord> SampleRecords(DateTimeOffset now) =>
    [
        new RunRecord("telegram", "Telegram-бот", JobTrigger.START, now.AddMinutes(-5), now.AddMinutes(-4), 0, RunOutcomeEnum.Ok, null),
        new RunRecord("mts", "MTS Link", JobTrigger.DAILY, now.AddHours(-17), now.AddHours(-17).AddMinutes(1), 1, RunOutcomeEnum.Failed, "Код завершения 1: нет доступа к LMS", null, "17.10.2026 18:50:01  Ошибка: System.InvalidOperationException: нет доступа к LMS\nat Lms.Client.SignIn() in LmsClient.cs:line 41\n17.10.2026 18:50:02  Ошибка: проверьте вход в браузере"),
        new RunRecord("reels", "Рилсы", "manual", now.AddMinutes(-30), now.AddMinutes(-30).AddMilliseconds(400), 1, RunOutcomeEnum.Failed, "InvalidOperationException: Нет файла appsettings.json рядом с программой: скопируйте appsettings.example.json и впишите токен бота", null, "26.10.2026 08:34:40  Ошибка: System.InvalidOperationException: Нет файла appsettings.json рядом с программой: скопируйте appsettings.example.json и впишите токен бота\nat Steward.Reels.Program.LoadSettings() in D:\\MyRepositories\\Tools\\Steward\\Steward.Reels\\Program.cs:line 75\nat Steward.Reels.Program.Main(String[] args) in D:\\MyRepositories\\Tools\\Steward\\Steward.Reels\\Program.cs:line 35"),
        new RunRecord("mail", "Почта", "manual", now.AddHours(-3), now.AddHours(-3).AddSeconds(8), 0, RunOutcomeEnum.Ok, null, "Ответить: 1 (Иванов И. И.)", "25.10.2026 14:11:06  Ответить: Иванов Иван — Вопрос → задача «Пришёл ответ от Иванов И. И.» ‖ https://mail.360.yandex.ru/#search?request=Вопрос ‖ Добрый день! Подскажите, пожалуйста, срок сдачи второй главы и нужен ли конспект к защите.\n25.10.2026 14:11:06  Инфо: Яндекс ID  — Вы создали пароль для приложения\n25.10.2026 14:11:07  Ответить: 1 (Иванов И. И.)\n25.10.2026 14:11:07  Прочее: 1"),
    ];
}
