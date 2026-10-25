using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Steward.Ideas.Models;
using Steward.Monitoring.Models;
using Steward.Status.Registry;
using Steward.Status.Rows;
using Steward.Status.Running;
using Steward.ViewModels;
using Steward.Views;

namespace Steward.Shell;

/// <summary>
/// Рисует окна приложения в PNG на примере выдуманных данных. Нужен, чтобы проверить вид, не открывая окно на рабочем столе
/// Запуск: <c>Steward.exe --snapshot файл.png [popup | design | main[:status|ideas|homework|journal]]</c>
/// </summary>
public static class SnapshotRenderer
{
    /// <summary>
    /// Рисует окно и сохраняет картинку
    /// </summary>
    /// <param name="path">Куда сохранить PNG</param>
    /// <param name="which">
    /// Какое окно: <c>main</c>, <c>popup</c> или <c>design</c> (страница-образец дизайн-системы)
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если картинка записана
    /// </returns>
    public static bool Render(string path, string which)
    {
        var now = DateTimeOffset.Now;
        var snapshot = SampleSnapshot(now);
        var jobs = SampleJobs();

        var parts = which.Split(':');
        Window window;
        if (string.Equals(parts[0], "design", StringComparison.OrdinalIgnoreCase))
        {
            window = new DesignWindow();
        }
        else if (string.Equals(parts[0], "popup", StringComparison.OrdinalIgnoreCase))
        {
            var popup = new StatusWindow();
            popup.Apply(snapshot, jobs, scrollToProblem: false);
            window = popup;
        }
        else
        {
            var main = new MainWindow();
            main.Apply(snapshot, jobs, SampleRecords(now), ["Задача «reels»: путь содержит {repo}, а корень репозитория не найден"], SampleIdeas(now));
            if (parts.Length > 1 && Enum.TryParse<AppPageEnum>(parts[1], ignoreCase: true, out var page))
                main.Model.Page = page;

            window = main;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.ShowInTaskbar = false;
        window.Show();
        window.UpdateLayout();

        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.FindResource("ShadowColor")), null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, width, height));
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
            new LogEntry(now.AddMinutes(-3), LogToneEnum.Quiet, "Всё совпало", "Новых правок в таблице нет", 4),
            new LogEntry(now.AddMinutes(-40), LogToneEnum.Update, "Появились правки", "Убрано из таблицы 30.10 в 20:10\nОбновлено «Гейм-дизайн»", 1),
        ],
    };

    /// <summary>
    /// Выдуманные задачи: по одной на каждый цвет
    /// </summary>
    /// <returns>Строки задач</returns>
    private static IReadOnlyList<JobRow> SampleJobs() =>
    [
        new JobRow("mail", "Почта", "Выполнено сегодня в 09:00", "Новых важных писем нет. Следующий запуск сегодня в 19:00", RowToneEnum.Ok, 0, true),
        new JobRow("reels", "Рилсы", "Ждёт тебя", "Разобрано 3 рилса. Ждут решения: 3", RowToneEnum.Attention, 3, true),
        new JobRow("mts", "MTS Link", "Не удалось", "Нет доступа к LMS: войдите заново (вчера в 19:00)", RowToneEnum.Problem, 0, true),
        new JobRow("telegram", "Telegram-бот", "Выполняется", "Запущена сегодня в 12:00", RowToneEnum.Working, 0, true),
        new JobRow("homework", "Домашние задания", "Отключена", "Включается полем enabled в config/jobs.json", RowToneEnum.Idle, 0, false),
    ];

    /// <summary>
    /// Выдуманные идеи из рилсов
    /// </summary>
    /// <param name="now">Текущий момент</param>
    /// <returns>Идеи от свежих к старым</returns>
    private static IReadOnlyList<Idea> SampleIdeas(DateTimeOffset now) =>
    [
        new Idea { Id = "r1-1", CreatedAt = now.AddMinutes(-12), SourceUrl = "https://www.instagram.com/reel/sample/", Title = "Хранить конспекты лекций в одном поиске", Author = "@study.tools", Transcript = "Собери все конспекты в одну папку, прогони через расшифровку и ищи по ключевым словам вместо того, чтобы листать файлы. Я так готовлюсь к экзаменам уже второй семестр и экономлю кучу времени перед сессией." },
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
        new RunRecord("mail", "Почта", "manual", now.AddHours(-3), now.AddHours(-3).AddSeconds(8), 0, RunOutcomeEnum.Ok, null, "Ответить: 1 (Иванов И. И.)", "25.10.2026 14:11:06  Ответить: Иванов Иван — Вопрос → задача «Пришёл ответ от Иванов И. И.»\n25.10.2026 14:11:06  Инфо: Яндекс ID  — Вы создали пароль для приложения\n25.10.2026 14:11:07  Ответить: 1 (Иванов И. И.)\n25.10.2026 14:11:07  Прочее: 1"),
    ];
}
