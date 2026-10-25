using System.Globalization;
using System.Windows.Input;
using Steward.Homework.Models;
using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Строка окна форматов: занятие, выбор формата ДЗ и подготовка заданий по расшифровке
/// </summary>
public sealed class LessonFormatViewModel : Bindable
{
    /// <summary>
    /// Что показывает индикатор подготовки
    /// </summary>
    private ActivityStateEnum _activity;

    /// <summary>
    /// Когда подготовка закончилась
    /// </summary>
    private DateTimeOffset _activitySince;

    /// <summary>
    /// Текст рядом с индикатором: что происходит или что получилось
    /// </summary>
    private string _status = string.Empty;

    /// <summary>
    /// Подготовка закончилась ошибкой
    /// </summary>
    private bool _failed;

    /// <summary>
    /// Создаёт строку
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="format">Выбранный сейчас формат</param>
    /// <param name="chosen">Вызывается, когда выбран другой формат: параметры — ключ занятия и формат</param>
    /// <param name="hasTranscript">Есть ли у занятия расшифровка, с которой можно начинать</param>
    /// <param name="prepare">Ищет задания в расшифровке: возвращает их число и текст ошибки, если не получилось</param>
    public LessonFormatViewModel(Lesson lesson, HomeworkFormatEnum format, Action<string, HomeworkFormatEnum> chosen, bool hasTranscript, Func<Lesson, Task<(int Count, string Error)>> prepare)
    {
        HasTranscript = hasTranscript;
        PrepareCommand = new RelayCommand(async () => await PrepareAsync(lesson, prepare), () => Activity != ActivityStateEnum.Working);
        Title = lesson.Title;
        Date = lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var options = new List<FormatOptionViewModel>();
        foreach (var option in HomeworkFormats.All)
        {
            options.Add(new FormatOptionViewModel(option, option == format, picked =>
            {
                foreach (var other in options)
                    other.IsSelected = other.Format == picked;

                chosen(lesson.Id, picked);
            }));
        }

        Options = options;
    }

    /// <summary>
    /// Есть ли у занятия расшифровка: без неё готовить задания не из чего
    /// </summary>
    public bool HasTranscript { get; }

    /// <summary>
    /// Что показывает индикатор подготовки
    /// </summary>
    public ActivityStateEnum Activity
    {
        get => _activity;
        private set
        {
            if (!Set(ref _activity, value))
                return;

            Raise(nameof(HasStatus));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Когда подготовка закончилась: от этого момента рисуется галочка
    /// </summary>
    public DateTimeOffset ActivitySince
    {
        get => _activitySince;
        private set => Set(ref _activitySince, value);
    }

    /// <summary>
    /// Текст рядом с индикатором
    /// </summary>
    public string Status
    {
        get => _status;
        private set
        {
            if (!Set(ref _status, value))
                return;

            Raise(nameof(HasStatus));
        }
    }

    /// <summary>
    /// Подготовка закончилась ошибкой: текст показывается красным
    /// </summary>
    public bool Failed
    {
        get => _failed;
        private set => Set(ref _failed, value);
    }

    /// <summary>
    /// Есть что показать рядом с индикатором
    /// </summary>
    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// Цвет точки для стиля индикатора: в покое серый
    /// </summary>
    public PanelToneEnum Tone => PanelToneEnum.Idle;

    /// <summary>
    /// Запускает поиск заданий в фоне, пока Claude работает, показывает вращение, потом галочку или ошибку
    /// </summary>
    public RelayCommand PrepareCommand { get; }

    /// <summary>
    /// Название предмета и темы
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// День занятия
    /// </summary>
    public string Date { get; }

    /// <summary>
    /// Форматы на выбор
    /// </summary>
    public IReadOnlyList<FormatOptionViewModel> Options { get; }

    /// <summary>
    /// Выполняет подготовку и меняет состояние индикатора по её итогу
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="prepare">Поиск заданий</param>
    /// <returns>Задача, которая завершается, когда подготовка закончилась</returns>
    private async Task PrepareAsync(Lesson lesson, Func<Lesson, Task<(int Count, string Error)>> prepare)
    {
        Failed = false;
        Status = "Ищу задания…";
        Activity = ActivityStateEnum.Working;

        (int Count, string Error) result;
        try
        {
            result = await prepare(lesson);
        }
        catch (Exception exception)
        {
            result = (0, exception.Message);
        }

        Failed = result.Error.Length > 0;
        Status = Failed ? result.Error : result.Count == 0 ? "Заданий в расшифровке нет" : $"Найдено заданий: {result.Count}";
        ActivitySince = DateTimeOffset.UtcNow;
        Activity = Failed ? ActivityStateEnum.None : ActivityStateEnum.Done;
    }
}