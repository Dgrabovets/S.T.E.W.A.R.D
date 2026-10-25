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
    /// Панель с подробностями
    /// </summary>
    private readonly StatusWindow _window = new();

    /// <summary>
    /// Иконка трея
    /// </summary>
    private readonly NotifyIcon _icon;

    /// <summary>
    /// Повтор опроса
    /// </summary>
    private readonly DispatcherTimer _timer = new() { Interval = POLL_INTERVAL };

    /// <summary>
    /// Значок работающего процесса
    /// </summary>
    private readonly Icon _runningIcon = CreateIcon(TrayPalette.Ok);

    /// <summary>
    /// Значок остановленного процесса
    /// </summary>
    private readonly Icon _stoppedIcon = CreateIcon(TrayPalette.Idle);

    /// <summary>
    /// Значок ошибки
    /// </summary>
    private readonly Icon _errorIcon = CreateIcon(TrayPalette.Danger);

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
        var menu = new ContextMenuStrip
        {
            Renderer = new MenuRenderer(),
            Font = new Font("Segoe UI", 10F),
            BackColor = TrayPalette.Surface,
            ForeColor = TrayPalette.Text,
            ShowImageMargin = false,
            Padding = new Padding(4, 6, 4, 6),
        };

        menu.Items.Add(Item("Открыть приложение", (_, _) => ShowMain()));
        menu.Items.Add(Item("Быстрый статус", (_, _) => ShowWindow()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Выход", (_, _) => Exit()));

        _icon = new NotifyIcon
        {
            Icon = _stoppedIcon,
            Visible = true,
            Text = "Расписание",
            ContextMenuStrip = menu,
        };

        _icon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                ShowWindow();
        };

        _icon.DoubleClick += (_, _) => ShowMain();
        _icon.BalloonTipClicked += (_, _) => ShowMain();
        _window.ExitRequested += (_, _) => Exit();
        _window.OpenAppRequested += (_, _) =>
        {
            _window.Hide();
            ShowMain();
        };

        _window.RestartRequested += async (_, _) =>
        {
            var restarted = await _probe.RestartAsync(CancellationToken.None);
            if (_exiting)
                return;

            _window.SetSettingsStatus(restarted
                ? "Контейнер перезапущен"
                : "Не удалось перезапустить контейнер");

            if (restarted)
                await RefreshAsync();
        };

        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();

        _jobs.Changed += (_, _) => ApplyAll();
        _jobs.Start();
    }

    /// <summary>
    /// Удаляет значок, созданный через <c>GetHicon</c>
    /// </summary>
    /// <param name="handle">Дескриптор значка</param>
    /// <returns>
    /// <see langword="true"/>, если значок удалён
    /// </returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

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
        _window.Close();
        _main?.CloseForExit();
        _runningIcon.Dispose();
        _stoppedIcon.Dispose();
        _errorIcon.Dispose();
    }

    #endregion Lifetime

    #region Icon

    /// <summary>
    /// Рисует скруглённый значок календаря
    /// </summary>
    /// <param name="color">Цвет фона значка</param>
    /// <returns>Иконка 32 на 32</returns>
    private static Icon CreateIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using (var shape = Rounded(new Rectangle(1, 1, 30, 30), 8))
        using (var brush = new SolidBrush(color))
            graphics.FillPath(brush, shape);

        using var white = new SolidBrush(Color.White);
        graphics.FillRectangle(white, 9, 12, 14, 12);
        using var band = new SolidBrush(color);
        graphics.FillRectangle(band, 9, 12, 14, 4);
        graphics.FillRectangle(white, 12, 8, 2, 6);
        graphics.FillRectangle(white, 18, 8, 2, 6);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    /// <summary>
    /// Строит скруглённый контур значка
    /// </summary>
    /// <param name="bounds">Прямоугольник контура</param>
    /// <param name="radius">Радиус угла в пикселях</param>
    /// <returns>Контур, который нужно освободить</returns>
    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
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
            _snapshot = await _probe.ReadAsync(CancellationToken.None);
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
        if (_window.IsVisible)
            _window.Apply(_snapshot, _jobRows, scrollToProblem: false);
        if (_main is { IsVisible: true })
            _main.Apply(_snapshot, _jobRows, _jobs.Records(), _jobs.RegistryErrors);

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

            var tip = $"{row.Headline}. {row.Detail}";
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
        _icon.Icon = snapshot.CurrentError is not null || failed is not null
            ? _errorIcon
            : snapshot.ContainerRunning ? _runningIcon : _stoppedIcon;

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
    /// Открывает или прячет панель у курсора
    /// </summary>
    private void ShowWindow()
    {
        if (!_window.IsVisible)
            _window.Apply(_snapshot, _jobRows, scrollToProblem: _snapshot.CurrentError is not null);

        _window.ToggleFromIcon();
    }

    /// <summary>
    /// Открывает главное окно приложения с самыми свежими данными
    /// </summary>
    private void ShowMain()
    {
        if (_exiting)
            return;

        if (_main is null)
        {
            _main = new MainWindow();
            _main.Model.RunRequested += id => _jobs.RunNow(id);
            _main.Model.FixRequested += OpenFix;
            _main.Model.ClearJournalRequested += (_, _) => _jobs.ClearRecords();
            _main.Model.FormatsRequested += (_, _) => ShowFormats();
            _main.Model.ExitRequested += (_, _) => Exit();
        }

        _jobRows = _jobs.Rows();
        _main.Apply(_snapshot, _jobRows, _jobs.Records(), _jobs.RegistryErrors);
        _main.ShowAndActivate();
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
    /// Открывает окно, где для каждого занятия выбирается формат ДЗ
    /// </summary>
    private void ShowFormats() =>
        new HomeworkFormatsWindow(new HomeworkFormatsViewModel(_jobs.Lessons(), _jobs.SetHomeworkFormat, _jobs.HasTranscript, lesson => _jobs.PrepareHomeworkAsync(lesson, CancellationToken.None))) { Owner = _main }.ShowDialog();

    /// <summary>
    /// Убирает иконку и завершает процесс
    /// </summary>
    private void Exit()
    {
        if (_exiting)
            return;

        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    #endregion Window

    #region Menu

    /// <summary>
    /// Пункт меню в цветах панели
    /// </summary>
    /// <param name="text">Подпись</param>
    /// <param name="onClick">Действие</param>
    /// <returns>Пункт меню</returns>
    private static ToolStripMenuItem Item(string text, EventHandler onClick)
    {
        return new ToolStripMenuItem(text, null, onClick)
        {
            ForeColor = TrayPalette.Text,
            Padding = new Padding(8, 4, 8, 4),
        };
    }

    /// <summary>
    /// Рисует меню в цветах панели
    /// </summary>
    private sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        /// <summary>
        /// Создаёт отрисовщик с тёмной палитрой
        /// </summary>
        public MenuRenderer()
            : base(new MenuColors())
        {
            RoundedEdges = false;
        }

        /// <summary>
        /// Пишет текст пункта светлым цветом
        /// </summary>
        /// <param name="e">Аргументы текста</param>
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TrayPalette.Text : TrayPalette.Muted;
            base.OnRenderItemText(e);
        }

        /// <summary>
        /// Убирает рамку выделения
        /// </summary>
        /// <param name="e">Аргументы рамки</param>
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var bounds = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
            var color = e.Item.Selected ? TrayPalette.SurfaceRaised : TrayPalette.Surface;

            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, bounds);
        }
    }

    #endregion Menu
}
