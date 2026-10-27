using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Steward.Homework.Models;
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
/// Запуск: <c>dotnet run --project Steward.DevTools -- snapshot файл.png [appicon | logo | social | settings | tray | design | main[:status|ideas|homework|journal]]</c>
/// </summary>
public static class SnapshotRenderer
{
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
        Action? afterShow = null;
        var snapshot = SampleSnapshot(now);
        var jobs = SampleJobs();

        var parts = which.Split(':');
        Window window;
        if (string.Equals(parts[0], "design", StringComparison.OrdinalIgnoreCase))
        {
            window = new DesignWindow { SizeToContent = SizeToContent.Manual, Height = 1560 };
        }
        else if (string.Equals(parts[0], "tray", StringComparison.OrdinalIgnoreCase))
        {
            var menu = new TrayMenuWindow();
            menu.Apply(snapshot, jobs);
            menu.SetItems(
            [
                new GooeyItem("\uE80F", "Статусы", () => { }),
                new GooeyItem("\uEA80", "Идеи", () => { }),
                new GooeyItem("\uE7BE", "Домашние задания", () => { }),
                new GooeyItem("\uE81C", "Журнал", () => { }),
                new GooeyItem("\uE713", "Настройки", () => { }, DividerBefore: true),
                new GooeyItem("\uE7E8", "Выход", () => { }),
            ]);
            window = menu;
            afterShow = () => menu.Expand(animate: false);
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
        else if (string.Equals(parts[0], "settings", StringComparison.OrdinalIgnoreCase))
        {
            var settings = new SettingsWindow();
            settings.Model.Formats = new HomeworkFormatsViewModel(SampleLessons(), (_, _) => { }, lesson => lesson.Id != "en-1020", _ => Task.FromResult((0, string.Empty)), (_, _) => { }, title => title == "Гейм-дизайн" ? LessonStateEnum.Fetch : LessonStateEnum.Undecided, _ => { }, (_, _) => { });

            // «settings:formats»: раскрыт блок «Форматы заданий»
            if (parts.Length > 1 && string.Equals(parts[1], "formats", StringComparison.OrdinalIgnoreCase))
            {
                settings.FormatsToggle.IsChecked = true;
                settings.ScheduleToggle.IsChecked = false;
                settleSeconds = 1.0;
            }

            window = settings;
        }
        else
        {
            var main = new MainWindow();
            main.Apply(snapshot, jobs, SampleRecords(now), ["Задача «reels»: путь содержит {repo}, а корень репозитория не найден"], SampleIdeas(now));
            main.ApplyHomework(SampleHomework());
            if (parts.Length > 1 && string.Equals(parts[1], "homework", StringComparison.OrdinalIgnoreCase))
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

            // «main:done»: задача «telegram» только что закончилась, на её карточке рисуется галочка
            if (parts.Length > 1 && string.Equals(parts[1], "done", StringComparison.OrdinalIgnoreCase))
            {
                main.Apply(snapshot, [.. jobs.Select(job => job.Id == "telegram" ? job with { Tone = RowToneEnum.Ok, Headline = "Выполнено только что" } : job)], SampleRecords(now), [], SampleIdeas(now));
                settleSeconds = 1.2;
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

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.FindResource("ShadowColor")), null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(source), null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var stream = File.Create(path))
            encoder.Save(stream);

        window.Close();
        return true;
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
        new JobRow("homework", "Домашние задания", "Отключена", "Чтобы включить, поставь enabled: true в config/jobs.json", RowToneEnum.Idle, 0, false),
    ];

    /// <summary>
    /// Выдуманные занятия с выбранными форматами ДЗ
    /// </summary>
    /// <returns>Занятия от свежих к старым</returns>
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
        new RunRecord("mail", "Почта", "manual", now.AddHours(-3), now.AddHours(-3).AddSeconds(8), 0, RunOutcomeEnum.Ok, null, "Ответить: 1 (Иванов И. И.)", "25.10.2026 14:11:06  Ответить: Иванов Иван — Вопрос → задача «Пришёл ответ от Иванов И. И.»\n25.10.2026 14:11:06  Инфо: Яндекс ID  — Вы создали пароль для приложения\n25.10.2026 14:11:07  Ответить: 1 (Иванов И. И.)\n25.10.2026 14:11:07  Прочее: 1"),
    ];
}
