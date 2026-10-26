using System.Globalization;
using System.IO;
using Steward.Models;
using Steward.Mvvm;
using Steward.Status.Common;

namespace Steward.ViewModels;

/// <summary>
/// Поля файла настроек расписания и команды окна настроек
/// </summary>
public sealed class SettingsViewModel : Bindable
{
    /// <summary>
    /// Путь к файлу настроек
    /// </summary>
    private readonly string _envPath = Path.Combine(DataLocation.RepoRootOrStart(), ".env");

    /// <summary>
    /// Интервал в минутах
    /// </summary>
    private int _interval = SyncSettings.DEFAULT_INTERVAL;

    /// <summary>
    /// Текст поля с числом минут
    /// </summary>
    private string _minutesText = SyncSettings.DEFAULT_INTERVAL.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Сообщение над полями настроек
    /// </summary>
    private string _settingsMessage = string.Empty;

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
    /// Ссылка на таблицу, из которой берётся идентификатор
    /// </summary>
    private string _spreadsheetLink = string.Empty;

    /// <summary>
    /// Ссылка на вкладку, из которой берётся номер листа
    /// </summary>
    private string _sheetLink = string.Empty;

    /// <summary>
    /// Итог определения идентификатора таблицы
    /// </summary>
    private string _spreadsheetNote = string.Empty;

    /// <summary>
    /// Итог определения номера листа
    /// </summary>
    private string _sheetNote = string.Empty;

    /// <summary>
    /// Создаёт модель и сразу читает файл настроек
    /// </summary>
    public SettingsViewModel()
    {
        SaveAndRestartCommand = new RelayCommand(SaveAndRestart);
        RestartCommand = new RelayCommand(Restart);
        DetectSpreadsheetCommand = new RelayCommand(DetectSpreadsheet);
        DetectSheetCommand = new RelayCommand(DetectSheet);
        OpenTokenPageCommand = new RelayCommand(() => LinkRequested?.Invoke(TOKEN_PAGE));
        LoadSettings();
        TimeZones = TimeZoneChoices.All(TimeZone);
    }

    /// <summary>
    /// Страница с описанием API Singularity, где рассказано, как выпустить токен
    /// </summary>
    public const string TOKEN_PAGE = "https://api.singularity-app.com/";

    /// <summary>
    /// Просит пересоздать контейнер
    /// </summary>
    public event EventHandler? RestartRequested;

    /// <summary>
    /// Просит открыть адрес в браузере: параметр — адрес
    /// </summary>
    public event Action<string>? LinkRequested;

    /// <summary>
    /// Достаёт идентификатор таблицы из ссылки
    /// </summary>
    public RelayCommand DetectSpreadsheetCommand { get; }

    /// <summary>
    /// Достаёт номер листа из ссылки на вкладку
    /// </summary>
    public RelayCommand DetectSheetCommand { get; }

    /// <summary>
    /// Открывает страницу, где выпускают токен
    /// </summary>
    public RelayCommand OpenTokenPageCommand { get; }

    /// <summary>
    /// Пояса, из которых выбирается пояс таблицы
    /// </summary>
    public IReadOnlyList<TimeZoneChoice> TimeZones { get; }

    /// <summary>
    /// Ссылка на таблицу, в которую вставляют адрес из браузера
    /// </summary>
    public string SpreadsheetLink
    {
        get => _spreadsheetLink;
        set => Set(ref _spreadsheetLink, value);
    }

    /// <summary>
    /// Ссылка на вкладку, в которую вставляют адрес из браузера
    /// </summary>
    public string SheetLinkText
    {
        get => _sheetLink;
        set => Set(ref _sheetLink, value);
    }

    /// <summary>
    /// Итог определения идентификатора таблицы
    /// </summary>
    public string SpreadsheetNote
    {
        get => _spreadsheetNote;
        private set => Set(ref _spreadsheetNote, value);
    }

    /// <summary>
    /// Итог определения номера листа
    /// </summary>
    public string SheetNote
    {
        get => _sheetNote;
        private set => Set(ref _sheetNote, value);
    }

    /// <summary>
    /// Сколько сейчас времени в поясе таблицы и на этом компьютере
    /// </summary>
    public string TimeZoneNote => TimeZoneChoices.Compare(TimeZone, DateTimeOffset.UtcNow);

    /// <summary>
    /// Над полями есть сообщение
    /// </summary>
    public bool HasSettingsMessage => SettingsMessage.Length > 0;

    /// <summary>
    /// Записывает поля и просит перезапуск
    /// </summary>
    public RelayCommand SaveAndRestartCommand { get; }

    /// <summary>
    /// Просит перезапуск без записи файла
    /// </summary>
    public RelayCommand RestartCommand { get; }

    /// <summary>
    /// Короткий итог сохранения или перезапуска
    /// </summary>
    public string SettingsMessage
    {
        get => _settingsMessage;
        set
        {
            if (Set(ref _settingsMessage, value))
                Raise(nameof(HasSettingsMessage));
        }
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
        set
        {
            if (Set(ref _timeZone, value))
                Raise(nameof(TimeZoneNote));
        }
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

    /// <summary>
    /// Находит идентификатор в ссылке на таблицу и вставляет его в поле
    /// </summary>
    private void DetectSpreadsheet()
    {
        if (SheetLink.TryId(SpreadsheetLink, out var id))
        {
            SpreadsheetId = id;
            SpreadsheetNote = "Идентификатор найден и вставлен в поле выше";
        }
        else
            SpreadsheetNote = "В ссылке нет идентификатора. Скопируйте адрес таблицы целиком из строки браузера";
    }

    /// <summary>
    /// Находит номер листа в ссылке на вкладку и вставляет его в поле
    /// </summary>
    private void DetectSheet()
    {
        if (SheetLink.TryGid(SheetLinkText, out var gid))
        {
            SheetGid = gid;
            SheetNote = "Номер листа найден и вставлен в поле выше";
        }
        else
            SheetNote = "В ссылке нет номера листа. Откройте нужную вкладку и скопируйте адрес: в нём есть «gid=…»";
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
}
