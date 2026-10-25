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
    public TaskCardViewModel(TaskStatusRow row, Action<string> run, ActivityStateEnum activity = ActivityStateEnum.None, DateTimeOffset activitySince = default)
    {
        Activity = activity;
        ActivitySince = activitySince;
        Name = row.Name;
        Headline = row.Headline;
        Detail = row.Detail;
        Tone = row.Tone;
        Muted = row.Muted;
        CanRun = row.CanRun;
        RunCommand = new RelayCommand(() => run(row.Id), () => row.CanRun);
    }

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
}
