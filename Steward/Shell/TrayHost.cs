using System.Diagnostics;
using Steward.Ideas.Models;
using Steward.Status.Running;
using Steward.Status.Common;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Steward.Homework.Models;
using Steward.Hosting;
using Steward.Models;
using Steward.Monitoring.Implementations;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.ViewModels;
using Steward.Views;

namespace Steward.Shell;

/// <summary>
/// Иконка в области уведомлений и опрос контейнера
/// </summary>
public sealed class TrayHost : IDisposable
{
    /// <summary>
    /// Как часто опрашивать Docker
    /// </summary>
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Сколько миллисекунд держать всплывающее сообщение об ошибке
    /// </summary>
    private const int BALLOON_MS = 12000;

    /// <summary>
    /// Сколько символов помещается в текст всплывающего сообщения
    /// </summary>
    private const int BALLOON_TEXT_LIMIT = 180;

    /// <summary>
    /// Сколько символов помещается в подсказку иконки
    /// </summary>
    private const int TOOLTIP_LIMIT = 63;

    /// <summary>
    /// Опрос контейнера
    /// </summary>
    private readonly DockerSyncProbe _probe = new(DataLocation.RepoRootOrStart());

    /// <summary>
    /// Список занятий с выбором формата ДЗ: общий для окна и страницы, чтобы идущая подготовка заданий не сбрасывалась
    /// </summary>
    private HomeworkFormatsViewModel? _formats;

    /// <summary>
    /// Меню иконки, пока оно открыто
    /// </summary>
    private TrayMenuPopup? _trayMenu;

    /// <summary>
    /// Иконка трея
    /// </summary>
    private readonly NotifyIcon _icon;

    /// <summary>
    /// Повтор опроса
    /// </summary>
    private readonly DispatcherTimer _timer = new() { Interval = POLL_INTERVAL };

    /// <summary>
    /// Текущая иконка: её нужно освободить, когда рисуется новая
    /// </summary>
    private Icon? _current;

    /// <summary>
    /// Перерисовывает иконку, пока что-то выполняется: сфера на ней вращается
    /// </summary>
    private readonly DispatcherTimer _iconTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

    /// <summary>
    /// Вид и цвет сферы, которая сейчас на иконке
    /// </summary>
    private (ActivityKindEnum Kind, string Brush) _orb = (ActivityKindEnum.Dormant, "IdleBrush");

    /// <summary>
    /// Последний снимок
    /// </summary>
    private SyncSnapshot _snapshot = new();

    /// <summary>
    /// Текст ошибки, о которой уже показано уведомление
    /// </summary>
    private string? _notifiedError;

    /// <summary>
    /// Запускает задачи из реестра и отдаёт их статусы
    /// </summary>
    private readonly JobHost _jobs = new();

    /// <summary>
    /// Главное окно. Создаётся при первом открытии
    /// </summary>
    private MainWindow? _main;

    /// <summary>
    /// Последние строки задач
    /// </summary>
    private IReadOnlyList<JobRow> _jobRows = [];

    /// <summary>
    /// Проблемы задач, о которых уже показано уведомление
    /// </summary>
    private readonly HashSet<string> _notifiedProblems = new(StringComparer.Ordinal);

    /// <summary>
    /// Момент самой свежей правки расписания, о которой уже показано уведомление. Переживает перезапуск иконки
    /// </summary>
    private DateTimeOffset _notifiedChangeAt = ChangeStamp.Load();

    /// <summary>
    /// Не даёт двум опросам идти одновременно
    /// </summary>
    private bool _refreshing;

    /// <summary>
    /// Процесс уже завершается
    /// </summary>
    private bool _exiting;

    #region Lifetime

    /// <summary>
    /// Создаёт иконку, меню и первый опрос
    /// </summary>
    public TrayHost()
    {
        _icon = new NotifyIcon
        {
            Visible = true,
            Text = "Расписание",
        };

        // Левая кнопка открывает единственное окно приложения, правая даёт короткое меню в стилистике приложения
        _icon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                ShowMain();
            else if (args.Button == MouseButtons.Right)
                ShowTrayMenu();
        };

        _icon.BalloonTipClicked += (_, _) => ShowMain();

        _iconTimer.Tick += (_, _) => RenderIcon();
        RenderIcon();

        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();

        _jobs.Changed += (_, _) => ApplyAll();
        _jobs.Start();
    }

    /// <summary>
    /// Освобождает иконку
    /// </summary>
    public void Dispose()
    {
        if (_exiting)
            return;

        _exiting = true;
        _timer.Stop();
        _jobs.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _main?.CloseForExit();
        _iconTimer.Stop();
        _current?.Dispose();
    }

    #endregion Lifetime

    #region Icon

    /// <summary>
    /// Рисует на иконке сферу текущего вида и цвета. Старая иконка освобождается
    /// </summary>
    private void RenderIcon()
    {
        if (_exiting)
            return;

        var brush = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[_orb.Brush];
        var next = TrayOrbIcon.Render(_orb.Kind, brush);
        _icon.Icon = next;
        _current?.Dispose();
        _current = next;
    }

    /// <summary>
    /// Выбирает вид и цвет сферы по состоянию: сбой, выполнение, ожидание решения, работа или остановка. Пока что-то выполняется, сфера движется
    /// </summary>
    /// <param name="snapshot">Текущее состояние контейнера</param>
    private void ChooseOrb(SyncSnapshot snapshot)
    {
        var enabled = _jobRows.Where(row => row.Enabled).ToList();
        var next = snapshot.CurrentError is not null || enabled.Any(row => row.Tone == RowToneEnum.Problem)
            ? (ActivityKindEnum.Failed, "DangerBrush")
            : enabled.Any(row => row.Tone == RowToneEnum.Working)
                ? (ActivityKindEnum.Working, "WorkingBrush")
                : enabled.Any(row => row.Tone == RowToneEnum.Attention)
                    ? (ActivityKindEnum.Attention, "AttentionBrush")
                    : snapshot.ContainerRunning ? (ActivityKindEnum.Alive, "AccentTextBrush") : (ActivityKindEnum.Dormant, "IdleBrush");

        if (next != _orb)
        {
            _orb = next;
            RenderIcon();
        }

        if (_orb.Kind == ActivityKindEnum.Working)
            _iconTimer.Start();
        else
            _iconTimer.Stop();
    }

    #endregion Icon

    #region Polling

    /// <summary>
    /// Обновляет иконку и при новой ошибке показывает уведомление
    /// </summary>
    /// <returns>Задача одного опроса</returns>
    private async Task RefreshAsync()
    {
        if (_refreshing || _exiting)
            return;

        _refreshing = true;
        try
        {
            var probed = await _probe.ReadAsync(CancellationToken.None);
            _snapshot = probed with { Entries = ScheduleHistory.Update(probed.Entries) };
            if (_exiting)
                return;

            ApplyAll();

            if (_snapshot.CurrentError is null)
            {
                _notifiedError = null;
                NotifyChanges(_snapshot);
                return;
            }

            if (string.Equals(_notifiedError, _snapshot.CurrentError, StringComparison.Ordinal))
                return;

            _notifiedError = _snapshot.CurrentError;
            var tip = _snapshot.CurrentError.Replace('\n', ' ');
            if (tip.Length > BALLOON_TEXT_LIMIT)
                tip = tip[..BALLOON_TEXT_LIMIT];
            _icon.ShowBalloonTip(BALLOON_MS, "Расписание", tip, ToolTipIcon.None);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Обновляет иконку и открытые окна свежими статусами контейнера и задач
    /// </summary>
    private void ApplyAll()
    {
        if (_exiting)
            return;

        _jobRows = _jobs.Rows();
        ApplyIcon(_snapshot);
        if (_main is { IsVisible: true })
            ApplyMain();

        NotifyJobProblems(_jobRows);
    }

    /// <summary>
    /// Один раз сообщает о каждой новой проблеме включённой задачи. Когда проблема уходит, о ней можно будет сообщить снова
    /// </summary>
    /// <param name="rows">Строки задач</param>
    private void NotifyJobProblems(IReadOnlyList<JobRow> rows)
    {
        var current = rows
            .Where(row => row.Enabled && row.Tone == RowToneEnum.Problem)
            .ToDictionary(row => $"{row.Id}|{row.Headline}|{row.Detail}", row => row, StringComparer.Ordinal);

        _notifiedProblems.RemoveWhere(key => !current.ContainsKey(key));
        foreach (var (key, row) in current)
        {
            if (!_notifiedProblems.Add(key))
                continue;

            var tip = $"{row.Headline}. {row.Detail.Replace('\n', ' ')}";
            if (tip.Length > BALLOON_TEXT_LIMIT)
                tip = tip[..BALLOON_TEXT_LIMIT];
            _icon.ShowBalloonTip(BALLOON_MS, row.Title, tip, ToolTipIcon.None);
        }
    }

    /// <summary>
    /// Один раз сообщает об отмене, переносе или правке занятия, которые появились после прошлого уведомления
    /// </summary>
    /// <param name="snapshot">Текущее состояние</param>
    private void NotifyChanges(SyncSnapshot snapshot)
    {
        var text = ChangeNotice.Build(snapshot.Entries, _notifiedChangeAt, out var newest);
        if (newest <= _notifiedChangeAt)
            return;

        _notifiedChangeAt = newest;
        ChangeStamp.Save(newest);
        if (text is null)
            return;

        if (text.Length > BALLOON_TEXT_LIMIT)
            text = text[..BALLOON_TEXT_LIMIT];
        _icon.ShowBalloonTip(BALLOON_MS, "Расписание изменилось", text, ToolTipIcon.None);
    }

    /// <summary>
    /// Красит иконку и пишет короткую подсказку
    /// </summary>
    /// <param name="snapshot">Текущее состояние</param>
    private void ApplyIcon(SyncSnapshot snapshot)
    {
        var failed = _jobRows.FirstOrDefault(row => row.Enabled && row.Tone == RowToneEnum.Problem);
        ChooseOrb(snapshot);

        var text = snapshot.CurrentError is not null
                     ? "Расписание: не удалось обновить"
                     : failed is not null
                         ? $"{failed.Title}: {failed.Headline}"
                     : snapshot.ContainerRunning
                         ? snapshot.LastSuccess is null
                             ? "Расписание работает"
                             : $"Расписание, обновлено в {snapshot.LastSuccess:HH:mm}"
                         : snapshot.DockerAvailable
                             ? "Расписание остановлено"
                             : "Расписание: Docker не запущен";

        _icon.Text = text.Length <= TOOLTIP_LIMIT ? text : text[..TOOLTIP_LIMIT];
    }

    #endregion Polling

    #region Window

    /// <summary>
    /// Открывает файл, папку или ссылку в программе по умолчанию
    /// </summary>
    /// <param name="path">Путь или адрес</param>
    private void OpenPath(string path)
    {
        if (path.Length == 0)
            return;

        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            OpenLink(path);

            return;
        }

        var error = _jobs.OpenFile(path);
        if (error.Length > 0)
            _icon.ShowBalloonTip(BALLOON_MS, "Файл", "Не удалось открыть: " + error, ToolTipIcon.None);
    }

    /// <summary>
    /// Запускает вход в MTS Link: окно браузера, в котором нужно войти один раз
    /// </summary>
    private void LoginToMts()
    {
        var program = System.IO.Path.Combine(DataLocation.RepoRootOrStart(), "Steward.Lms", "publish", "Steward.Lms.exe");
        if (!System.IO.File.Exists(program))
        {
            _icon.ShowBalloonTip(BALLOON_MS, "MTS Link", "Не найден Steward.Lms.exe: собери проект Steward.Lms в publish", ToolTipIcon.None);

            return;
        }

        Process.Start(new ProcessStartInfo(program, "login") { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(program)! });
    }

    /// <summary>
    /// Открывает файл настроек процесса в программе по умолчанию
    /// </summary>
    /// <param name="kind">Какой файл: «mail», «reels» или «lms»</param>
    private void OpenSettingsFile(string kind)
    {
        var root = DataLocation.RepoRootOrStart();
        var path = kind switch
        {
            "mail" => SettingsProbe.MailFile(root),
            "reels" => SettingsProbe.ReelsFile(root),
            _ => SettingsProbe.LmsFile(root),
        };

        if (System.IO.File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else
            _icon.ShowBalloonTip(BALLOON_MS, "Настройки", "Файл настроек не найден: " + path, ToolTipIcon.None);
    }

    /// <summary>
    /// Показывает меню иконки у курсора. Если оно уже открыто, второе не появляется
    /// </summary>
    private void ShowTrayMenu()
    {
        if (_exiting || _trayMenu is { IsVisible: true })
            return;

        _trayMenu = new TrayMenuPopup(() => ShowMain(), Exit);
        _trayMenu.Closed += (_, _) => _trayMenu = null;
        _trayMenu.ShowAtCursor();
    }

    /// <summary>
    /// Открывает главное окно приложения с самыми свежими данными
    /// </summary>
    /// <param name="page">Раздел, который нужно открыть, или <see langword="null"/>, чтобы оставить прежний</param>
    public void ShowMain(AppPageEnum? page = null)
    {
        if (_exiting)
            return;

        if (_main is null)
        {
            _main = new MainWindow();
            _main.Model.RunRequested += id => _jobs.RunNow(id);
            _main.Model.FixRequested += OpenFix;
            _main.Model.ClearJournalRequested += (_, _) => ClearJournal();
            _main.Model.ClearGroupRequested += ClearGroup;
            _main.Model.RecordRemoveRequested += record => _jobs.RemoveRecord(record);
            _main.Model.CheckRemoveRequested += RemoveCheck;
            _main.Model.IdeaApproved += ApproveIdea;
            _main.Model.IdeaRejected += idea => _jobs.DeleteIdea(idea.Id);
            _main.Model.LinkRequested += OpenLink;
            _main.Model.FileRequested += OpenSettingsFile;
            _main.Model.MtsLoginRequested += LoginToMts;
            _main.Model.Settings.LinkRequested += OpenLink;
            _main.Model.Settings.RestartRequested += async (_, _) => await RestartContainerAsync();
            _main.Model.ExitRequested += (_, _) => Exit();
        }

        _jobRows = _jobs.Rows();
        ApplyMain();
        if (page is { } selected)
            _main.Model.Page = selected;
        else if (!_main.IsVisible)
            _main.Model.Page = AppPageEnum.Status;

        _main.ShowAndActivate();
    }

    /// <summary>
    /// Стирает журнал запусков вместе с историей сверок расписания
    /// </summary>
    private void ClearJournal()
    {
        _jobs.ClearRecords();
        ScheduleHistory.Clear();
        _snapshot = _snapshot with { Entries = [] };
        ApplyAll();
    }

    /// <summary>
    /// Удаляет одну сверку расписания из журнала
    /// </summary>
    /// <param name="entry">Запись сверки</param>
    private void RemoveCheck(LogEntry entry)
    {
        ScheduleHistory.Remove(entry);
        _snapshot = _snapshot with { Entries = [.. _snapshot.Entries.Where(stored => stored.Time != entry.Time)] };
        ApplyAll();
    }

    /// <summary>
    /// Стирает записи одного раздела журнала
    /// </summary>
    /// <param name="id">Идентификатор задачи или «schedule» для сверок расписания</param>
    private void ClearGroup(string id)
    {
        if (id == "schedule")
        {
            ScheduleHistory.Clear();
            _snapshot = _snapshot with { Entries = [] };
            ApplyAll();

            return;
        }

        _jobs.ClearRecords(id);
    }

    /// <summary>
    /// Открывает Claude с просьбой починить неудавшийся запуск. Если приложение не открылось, говорит об этом уведомлением
    /// </summary>
    /// <param name="record">Неудавшийся запуск из журнала</param>
    private void OpenFix(RunRecord record)
    {
        var error = ClaudeFixLauncher.Open(record);
        if (error.Length > 0)
            _icon.ShowBalloonTip(BALLOON_MS, "Починка", error, ToolTipIcon.None);
    }

    /// <summary>
    /// Открывает Claude с просьбой разобрать идею. Идея считается принятой, только если приложение открылось
    /// </summary>
    /// <param name="idea">Идея, которую решено делать</param>
    private void ApproveIdea(Idea idea)
    {
        var error = ClaudeLauncher.Open(IdeaPrompt.Build(idea, DataLocation.RepoRootOrStart(), _jobs.IdeaPath(idea.Id)));
        if (error.Length > 0)
        {
            _icon.ShowBalloonTip(BALLOON_MS, "Идея", error, ToolTipIcon.None);

            return;
        }

        _jobs.SetIdeaState(idea.Id, IdeaStateEnum.Approved);
    }

    /// <summary>
    /// Открывает ссылку в браузере, если это обычный адрес сайта
    /// </summary>
    /// <param name="link">Адрес рилса</param>
    private static void OpenLink(string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    /// <summary>
    /// Показывает в главном окне свежие статусы, журнал, идеи и домашние задания
    /// </summary>
    private void ApplyMain()
    {
        _main!.Apply(_snapshot, _jobRows, _jobs.Records(), _jobs.RegistryErrors, _jobs.Ideas());
        _main.ApplyHomework(_jobs.HomeworkRows());
        var root = DataLocation.RepoRootOrStart();
        _main.Model.AttachSettings(root, ApplyMain);
        _main.Model.SetSettingsHome(_jobs.SettingsHomeIds());
        _main.Model.ApplyChips(SettingsProbe.Mail(root), SettingsProbe.Reels(root), SettingsProbe.Transcripts(root));
        _main.Model.Formats = Formats();
    }

    /// <summary>
    /// Общий список занятий с форматами: создаётся один раз и каждый раз обновляется свежими данными
    /// </summary>
    /// <returns>Модель списка</returns>
    private HomeworkFormatsViewModel Formats()
    {
        _formats ??= new HomeworkFormatsViewModel(_jobs.SetHomeworkFormat, _jobs.HasTranscript, (lesson, request) => _jobs.PrepareHomeworkAsync(lesson, request, CancellationToken.None), _jobs.SetSubjectState, _jobs.SubjectState, _jobs.RequestTranscript, _jobs.SetSubjectFormat);
        _formats.TranscriptReader = _jobs.ReadTranscript;
        _formats.DraftLister = _jobs.DraftFiles;
        _formats.AnalysisReader = _jobs.ReadAnalysis;
        _formats.ItemsReader = _jobs.Items;
        _formats.ItemArchived = _jobs.ArchiveItem;
        _formats.ItemToSingularity = _jobs.AddItemToSingularityAsync;
        _formats.DraftsDeleted = _jobs.DeleteDrafts;
        _formats.GuideLister = _jobs.Guides;
        _formats.GuideFilesAdded = _jobs.AddGuideFiles;
        _formats.GuideFolderAdded = _jobs.AddGuideFolder;
        _formats.GuideLinkAdded = _jobs.AddGuideLinkAsync;
        _formats.GuideEnabledChanged = _jobs.SetGuideEnabled;
        _formats.GuideRemoved = _jobs.RemoveGuide;
        _formats.PathOpened = OpenPath;
        _formats.GuideFolder = System.IO.Path.Combine(DataLocation.DataDirectory(), "homework", "guides");
        _formats.Update(_jobs.Lessons());
        _formats.ApplyActivities(_jobs.Activities());
        _formats.ApplyHomework(_jobs.HomeworkRows());

        return _formats;
    }

    /// <summary>
    /// Пересоздаёт контейнер расписания и пишет итог на странице «Расписание»
    /// </summary>
    /// <returns>Задача, которая завершается, когда перезапуск закончился</returns>
    private async Task RestartContainerAsync()
    {
        var restarted = await _probe.RestartAsync(CancellationToken.None);
        if (_exiting)
            return;

        if (_main is not null)
            _main.Model.Settings.SettingsMessage = restarted ? "Контейнер перезапущен" : "Не удалось перезапустить контейнер";

        if (restarted)
            await RefreshAsync();
    }

    /// <summary>
    /// Убирает иконку и завершает процесс
    /// </summary>
    private void Exit()
    {
        if (_exiting)
            return;

        if (!ConfirmWindow.Ask("Выйти из Steward?", "Пока приложение закрыто, почта, рилсы и расписание не проверяются. Запусти его снова, чтобы проверки продолжились.", "Выйти", "Остаться"))
            return;

        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    #endregion Window
}
