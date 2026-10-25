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
    public TaskCardViewModel(TaskStatusRow row, Action<string> run)
    {
        Name = row.Name;
        Headline = row.Headline;
        Detail = row.Detail;
        Tone = row.Tone;
        Muted = row.Muted;
        CanRun = row.CanRun;
        RunCommand = new RelayCommand(() => run(row.Id), () => row.CanRun);
    }

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
