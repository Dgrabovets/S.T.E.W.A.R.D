using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Карточка задачи в разделе «Статус»
/// </summary>
public sealed class TaskCardViewModel
{
    /// <summary>
    /// Создаёт карточку
    /// </summary>
    /// <param name="row">Строка задачи</param>
    /// <param name="run">Действие кнопки «Запустить»</param>
    /// <param name="activity">Что показывает индикатор процесса</param>
    /// <param name="activitySince">Когда процесс закончился, если индикатор показывает галочку</param>
    /// <param name="metrics">Показатели под текстом карточки: Docker, контейнер и так далее</param>
    /// <param name="setEnabled">Действие тумблера: идентификатор задачи и включена ли она. Пусто, если у карточки нет тумблера</param>
    /// <param name="progress">Что сейчас выполняется: шаги и счётчик. Пусто, если процесс не работает или не сообщает прогресс</param>
    /// <param name="openSettings">Действие шестерёнки: открыть страницу процесса с настройками. Пусто, если у процесса нет страницы</param>
    public TaskCardViewModel(TaskStatusRow row, Action<string> run, ActivityStateEnum activity = ActivityStateEnum.None, DateTimeOffset activitySince = default, Action<string>? openSettings = null, IReadOnlyList<StatusTileViewModel>? metrics = null, ProgressCardViewModel? progress = null, Action<string, bool>? setEnabled = null)
    {
        Id = row.Id;
        Kind = ActivityKinds.Of(row);
        Activity = activity;
        ActivitySince = activitySince;
        Name = row.Name;
        Headline = row.Headline;
        Detail = row.Detail;
        DetailLines = row.DetailLines;
        Tone = row.Tone;
        Muted = row.Muted;
        CanRun = row.CanRun;
        RunCommand = new RelayCommand(() => run(row.Id), () => row.CanRun);
        Metrics = metrics ?? [];
        CanSwitch = setEnabled is not null;
        IsOn = !row.Muted;
        SwitchCommand = new RelayCommand(() => setEnabled?.Invoke(row.Id, !IsOn));
        Progress = progress;
        HasSettings = openSettings is not null;
        SettingsCommand = new RelayCommand(() => openSettings?.Invoke(row.Id));
    }

    /// <summary>
    /// Идентификатор задачи
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Вид сферы: по делу задачи, пока она выполняется, иначе по её состоянию
    /// </summary>
    public ActivityKindEnum Kind { get; }

    /// <summary>
    /// Что показывает индикатор процесса
    /// </summary>
    public ActivityStateEnum Activity { get; }

    /// <summary>
    /// Когда процесс закончился: от этого момента рисуется галочка
    /// </summary>
    public DateTimeOffset ActivitySince { get; }

    /// <summary>
    /// Название задачи
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Короткий статус
    /// </summary>
    public string Headline { get; }

    /// <summary>
    /// Подробности
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Подробности по пунктам: каждая строка отдельно
    /// </summary>
    public IReadOnlyList<string> DetailLines { get; }

    /// <summary>
    /// Цвет точки
    /// </summary>
    public PanelToneEnum Tone { get; }

    /// <summary>
    /// Задача выключена
    /// </summary>
    public bool Muted { get; }

    /// <summary>
    /// У карточки есть кнопка запуска
    /// </summary>
    public bool CanRun { get; }

    /// <summary>
    /// Запускает задачу сейчас
    /// </summary>
    public RelayCommand RunCommand { get; }

    /// <summary>
    /// Показатели под текстом карточки
    /// </summary>
    public IReadOnlyList<StatusTileViewModel> Metrics { get; }

    /// <summary>
    /// У карточки есть показатели
    /// </summary>
    public bool HasMetrics => Metrics.Count > 0;

    /// <summary>
    /// У карточки есть тумблер «включить или выключить процесс»
    /// </summary>
    public bool CanSwitch { get; }

    /// <summary>
    /// Процесс включён и запускается по расписанию
    /// </summary>
    public bool IsOn { get; }

    /// <summary>
    /// Переключает процесс. Идущий запуск при выключении не прерывается
    /// </summary>
    public RelayCommand SwitchCommand { get; }

    /// <summary>
    /// Что сейчас выполняется: шаги и счётчик
    /// </summary>
    public ProgressCardViewModel? Progress { get; }

    /// <summary>
    /// Процесс сообщает, что именно он сейчас делает
    /// </summary>
    public bool HasProgress => Progress is not null;

    /// <summary>
    /// В карточке есть рамка с прогрессом или показателями
    /// </summary>
    public bool HasFrame => HasProgress || HasMetrics;

    /// <summary>
    /// У процесса есть страница с настройками
    /// </summary>
    public bool HasSettings { get; }

    /// <summary>
    /// Открывает страницу процесса сразу с настройками
    /// </summary>
    public RelayCommand SettingsCommand { get; }
}
