using System.Globalization;
using System.IO;
using Steward.Status;
using Steward.Models;
using Steward.Monitoring.Implementations;
using Steward.Monitoring.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Состояние панели: журнал, плашка и поля файла настроек
/// </summary>
public sealed class StatusViewModel : Bindable
{
    /// <summary>
    /// Путь к файлу настроек
    /// </summary>
    private readonly string _envPath = Path.Combine(DockerSyncProbe.FindRepoRoot(), ".env");

    /// <summary>
    /// Интервал в минутах
    /// </summary>
    private int _interval = SyncSettings.DEFAULT_INTERVAL;

    /// <summary>
    /// Текст поля с числом минут
    /// </summary>
    private string _minutesText = SyncSettings.DEFAULT_INTERVAL.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Подпись под заголовком
    /// </summary>
    private string _subtitle = "Проверяю задачи";

    /// <summary>
    /// Строки списка задач
    /// </summary>
    private IReadOnlyList<TaskStatusRow> _tasks = [];

    /// <summary>
    /// Короткий текст плашки
    /// </summary>
    private string _statusLabel = "Остановлено";

    /// <summary>
    /// Цвет плашки
    /// </summary>
    private PanelTone _tone = PanelTone.Idle;

    /// <summary>
    /// Карточки журнала
    /// </summary>
    private IReadOnlyList<JournalCard> _cards = [];

    /// <summary>
    /// Текст, если карточек нет
    /// </summary>
    private string _emptyText = "Журнал пока пуст";

    /// <summary>
    /// Открыт ли экран настроек
    /// </summary>
    private bool _isSettingsOpen;

    /// <summary>
    /// Сообщение над полями настроек
    /// </summary>
    private string _settingsMessage = "Здесь параметры из файла настроек. Они уже подставлены";

    /// <summary>
    /// Название проекта
    /// </summary>
    private string _projectTitle = string.Empty;

    /// <summary>
    /// Колонка группы
    /// </summary>
    private string _groupColumn = string.Empty;

    /// <summary>
    /// Часовой пояс
    /// </summary>
    private string _timeZone = string.Empty;

    /// <summary>
    /// Идентификатор таблицы
    /// </summary>
    private string _spreadsheetId = string.Empty;

    /// <summary>
    /// Номер листа
    /// </summary>
    private string _sheetGid = string.Empty;

    /// <summary>
    /// Токен Singularity
    /// </summary>
    private string _token = string.Empty;

    /// <summary>
    /// Создаёт модель и сразу читает файл настроек
    /// </summary>
    public StatusViewModel()
    {
        ToggleSettingsCommand = new RelayCommand(ToggleSettings);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        OpenAppCommand = new RelayCommand(() => OpenAppRequested?.Invoke(this, EventArgs.Empty));
        SaveAndRestartCommand = new RelayCommand(SaveAndRestart);
        RestartCommand = new RelayCommand(Restart);
        LoadSettings();
    }

    #region Commands

    /// <summary>
    /// Просит закрыть приложение
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Просит пересоздать контейнер
    /// </summary>
    public event EventHandler? RestartRequested;

    /// <summary>
    /// Просит открыть главное окно приложения
    /// </summary>
    public event EventHandler? OpenAppRequested;

    /// <summary>
    /// Открывает главное окно приложения
    /// </summary>
    public RelayCommand OpenAppCommand { get; }

    /// <summary>
    /// Переключает журнал и настройки
    /// </summary>
    public RelayCommand ToggleSettingsCommand { get; }

    /// <summary>
    /// Закрывает приложение
    /// </summary>
    public RelayCommand ExitCommand { get; }

    /// <summary>
    /// Записывает поля и просит перезапуск
    /// </summary>
    public RelayCommand SaveAndRestartCommand { get; }

    /// <summary>
    /// Просит перезапуск без записи файла
    /// </summary>
    public RelayCommand RestartCommand { get; }

    #endregion Commands

    #region State

    /// <summary>
    /// Строка под заголовком «Задачи»: всё ли в порядке или что требует внимания
    /// </summary>
    public string Subtitle
    {
        get => _subtitle;
        private set => Set(ref _subtitle, value);
    }

    /// <summary>
    /// Задачи со статусом: расписание и проверка почты
    /// </summary>
    public IReadOnlyList<TaskStatusRow> Tasks
    {
        get => _tasks;
        private set => Set(ref _tasks, value);
    }

    /// <summary>
    /// Текст цветной плашки справа
    /// </summary>
    public string StatusLabel
    {
        get => _statusLabel;
        private set => Set(ref _statusLabel, value);
    }

    /// <summary>
    /// Какой цвет взять для плашки
    /// </summary>
    public PanelTone Tone
    {
        get => _tone;
        private set
        {
            if (!Set(ref _tone, value))
                return;

            Raise(nameof(IsRunning));
        }
    }

    /// <summary>
    /// Контейнер работает и сбоя нет. От этого зависит свечение значка
    /// </summary>
    public bool IsRunning => Tone == PanelTone.Running;

    /// <summary>
    /// Карточки журнала от новых к старым
    /// </summary>
    public IReadOnlyList<JournalCard> Cards
    {
        get => _cards;
        private set
        {
            if (!Set(ref _cards, value))
                return;

            Raise(nameof(IsJournalEmpty));
        }
    }

    /// <summary>
    /// Что показать, если журнал пуст
    /// </summary>
    public string EmptyText
    {
        get => _emptyText;
        private set => Set(ref _emptyText, value);
    }

    /// <summary>
    /// Журнал пуст, вместо карточек нужна поясняющая строка
    /// </summary>
    public bool IsJournalEmpty => Cards.Count == 0;

    /// <summary>
    /// На экране настройки, а не журнал
    /// </summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set
        {
            if (!Set(ref _isSettingsOpen, value))
                return;

            Raise(nameof(ModeCaption));
        }
    }

    /// <summary>
    /// Подпись кнопки, которая переключает экран
    /// </summary>
    public string ModeCaption => IsSettingsOpen ? "Журнал" : "Настройки";

    /// <summary>
    /// Короткий итог сохранения или перезапуска
    /// </summary>
    public string SettingsMessage
    {
        get => _settingsMessage;
        set => Set(ref _settingsMessage, value);
    }

    /// <summary>
    /// Положение ползунка, в минутах
    /// </summary>
    public double IntervalMinutes
    {
        get => _interval;
        set
        {
            var minutes = Math.Clamp((int)Math.Round(value), SyncSettings.MIN_INTERVAL, SyncSettings.MAX_INTERVAL);
            if (_interval == minutes)
                return;

            _interval = minutes;
            Raise(nameof(IntervalMinutes));
            var text = minutes.ToString(CultureInfo.InvariantCulture);
            if (_minutesText == text)
                return;

            _minutesText = text;
            Raise(nameof(MinutesText));
        }
    }

    /// <summary>
    /// Число минут, как его видит текстовое поле
    /// </summary>
    public string MinutesText
    {
        get => _minutesText;
        set
        {
            if (!Set(ref _minutesText, value))
                return;

            if (!SyncSettings.TryInterval(value, out var minutes) || _interval == minutes)
                return;

            _interval = minutes;
            Raise(nameof(IntervalMinutes));
        }
    }

    /// <summary>
    /// Имя проекта в Singularity
    /// </summary>
    public string ProjectTitle
    {
        get => _projectTitle;
        set => Set(ref _projectTitle, value);
    }

    /// <summary>
    /// Заголовок колонки группы на листе
    /// </summary>
    public string GroupColumn
    {
        get => _groupColumn;
        set => Set(ref _groupColumn, value);
    }

    /// <summary>
    /// Пояс, в котором в таблице указано время пар
    /// </summary>
    public string TimeZone
    {
        get => _timeZone;
        set => Set(ref _timeZone, value);
    }

    /// <summary>
    /// Идентификатор Google Таблицы
    /// </summary>
    public string SpreadsheetId
    {
        get => _spreadsheetId;
        set => Set(ref _spreadsheetId, value);
    }

    /// <summary>
    /// Номер вкладки внутри таблицы
    /// </summary>
    public string SheetGid
    {
        get => _sheetGid;
        set => Set(ref _sheetGid, value);
    }

    /// <summary>
    /// Ключ доступа к аккаунту Singularity
    /// </summary>
    public string Token
    {
        get => _token;
        set => Set(ref _token, value);
    }

    #endregion State

    #region Snapshot

    /// <summary>
    /// Показывает свежий снимок журнала и статусы задач
    /// </summary>
    /// <param name="snapshot">Текущее состояние синхронизации</param>
    /// <param name="jobs">Строки задач из реестра</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs)
    {
        var panel = PanelStatus.From(snapshot);
        Tasks = TaskRows.Build(snapshot, jobs);

        var summary = TaskSummary.Of(Tasks);
        Tone = summary.Tone;
        StatusLabel = summary.Label;
        Subtitle = summary.Subtitle;
        EmptyText = panel.EmptyText;
        Cards = panel.Cards;
    }

    #endregion Snapshot

    #region Settings

    /// <summary>
    /// Открывает настройки и заново читает файл, либо возвращает журнал
    /// </summary>
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (!IsSettingsOpen)
            return;

        LoadSettings();
        SettingsMessage = "Значения взяты из файла настроек. Сохранение записывает их и перезапускает контейнер";
    }

    /// <summary>
    /// Записывает поля и просит перезапуск, если они заполнены
    /// </summary>
    private void SaveAndRestart()
    {
        if (!TrySave())
            return;

        SettingsMessage = "Настройки записаны, перезапускаю";
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Просит перезапуск контейнера с текущим файлом
    /// </summary>
    private void Restart()
    {
        SettingsMessage = "Перезапускаю";
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Читает пары из файла в свойства
    /// </summary>
    private void LoadSettings()
    {
        var settings = SyncSettings.Load(_envPath);
        MinutesText = settings.IntervalText;
        if (SyncSettings.TryInterval(settings.IntervalText, out var minutes))
            IntervalMinutes = minutes;
        ProjectTitle = settings.ProjectTitle;
        GroupColumn = settings.GroupColumn;
        TimeZone = settings.TimeZone;
        SpreadsheetId = settings.SpreadsheetId;
        SheetGid = settings.SheetGid;
        Token = settings.Token;
    }

    /// <summary>
    /// Записывает свойства в файл
    /// </summary>
    /// <returns>
    /// <see langword="false"/>, если обязательное поле пустое или интервал не число
    /// </returns>
    private bool TrySave()
    {
        if (!SyncSettings.TryInterval(MinutesText, out var minutes))
        {
            SettingsMessage = "Интервал — целое число минут от 1 до 180";
            return false;
        }

        var settings = new SyncSettings
        {
            IntervalMinutes = minutes,
            ProjectTitle = ProjectTitle,
            GroupColumn = GroupColumn,
            TimeZone = TimeZone,
            SpreadsheetId = SpreadsheetId,
            SheetGid = SheetGid,
            Token = Token,
        };
        var rejection = settings.Rejection();
        if (rejection is not null)
        {
            SettingsMessage = rejection;
            return false;
        }

        settings.Save(_envPath);
        return true;
    }

    #endregion Settings
}
