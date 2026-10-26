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
    public SettingsViewModel()
    {
        SaveAndRestartCommand = new RelayCommand(SaveAndRestart);
        RestartCommand = new RelayCommand(Restart);
        OpenFormatsCommand = new RelayCommand(() => FormatsRequested?.Invoke(this, EventArgs.Empty));
        LoadSettings();
    }

    /// <summary>
    /// Просит пересоздать контейнер
    /// </summary>
    public event EventHandler? RestartRequested;

    /// <summary>
    /// Просит открыть окно выбора форматов домашних заданий
    /// </summary>
    public event EventHandler? FormatsRequested;

    /// <summary>
    /// Записывает поля и просит перезапуск
    /// </summary>
    public RelayCommand SaveAndRestartCommand { get; }

    /// <summary>
    /// Просит перезапуск без записи файла
    /// </summary>
    public RelayCommand RestartCommand { get; }

    /// <summary>
    /// Открывает окно форматов домашних заданий
    /// </summary>
    public RelayCommand OpenFormatsCommand { get; }
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
