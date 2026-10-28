using System.Globalization;
using System.Windows.Input;
using Steward.Homework.Models;
using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Строка занятия внутри карточки предмета: дата, подпись, кнопка «Расшифровать» для занятий без текста и кнопка «Подготовить задания» с выбором формата для занятий с текстом
/// </summary>
public sealed class LessonFormatViewModel : Bindable
{
    /// <summary>
    /// Что показывает индикатор подготовки
    /// </summary>
    private ActivityStateEnum _activity;

    /// <summary>
    /// Когда подготовка закончилась: от этого момента рисуется галочка
    /// </summary>
    private DateTimeOffset _activitySince;

    /// <summary>
    /// Текст статуса: идёт поиск, найдено столько-то, ошибка
    /// </summary>
    private string _status = string.Empty;

    /// <summary>
    /// Подготовка закончилась ошибкой
    /// </summary>
    private bool _failed;

    /// <summary>
    /// У занятия уже есть расшифровка
    /// </summary>
    private bool _hasTranscript;

    /// <summary>
    /// Меню выбора формата раскрыто
    /// </summary>
    private bool _isMenuOpen;

    /// <summary>
    /// Расшифровка открыта на странице
    /// </summary>
    private bool _isOpen;

    /// <summary>
    /// Готовые файлы
    /// </summary>
    private IReadOnlyList<DraftFileViewModel> _drafts = [];

    /// <summary>
    /// Расшифровку этого занятия уже попросили забрать
    /// </summary>
    private bool _fetchRequested;

    /// <summary>
    /// Создаёт строку занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="format">Формат заданий по умолчанию: его отмечает меню, но выбрать можно любой</param>
    /// <param name="chosen">Запоминает формат, в котором подготовлено занятие: ключ занятия и формат</param>
    /// <param name="hasTranscript">Расшифровка уже лежит на диске</param>
    /// <param name="prepare">Ищет задания в расшифровке занятия: возвращает их число и текст ошибки</param>
    /// <param name="fetchRequested">Вызывается по кнопке «Расшифровать»: просит забрать расшифровку занятия, даже если предмет не отмечен. Пусто: кнопки нет</param>
    public LessonFormatViewModel(Lesson lesson, HomeworkFormatEnum format, Action<string, HomeworkFormatEnum> chosen, bool hasTranscript, Func<Lesson, Task<(int Count, string Error)>> prepare, Action<Lesson>? fetchRequested = null)
    {
        Id = lesson.Id;
        _hasTranscript = hasTranscript;
        Lesson = lesson;
        PrepareCommand = new RelayCommand(() => IsMenuOpen = !IsMenuOpen, () => Activity != ActivityStateEnum.Working);
        OpenCommand = new RelayCommand(() => Opened?.Invoke(this), () => HasTranscript);
        FetchCommand = new RelayCommand(
            () =>
            {
                FetchRequested = true;
                Failed = false;
                Status = "Расшифровка заказана: она появится после ближайшей выгрузки, это несколько минут";
                fetchRequested?.Invoke(lesson);
            },
            () => fetchRequested is not null && !FetchRequested);
        Title = lesson.Title;
        Date = lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        Duration = lesson.Minutes > 0 ? $"{lesson.Minutes / 60} ч {lesson.Minutes % 60:00} мин" : string.Empty;
        Headline = string.Join("  ·  ", new[] { Date, lesson.Label }.Where(part => part.Length > 0));
        Meta = string.Join("  ·  ", new[] { Headline, Duration }.Where(part => part.Length > 0));
        var options = new List<FormatOptionViewModel>();
        foreach (var option in HomeworkFormats.All)
        {
            options.Add(new FormatOptionViewModel(option, option == format, async picked =>
            {
                IsMenuOpen = false;
                chosen(lesson.Id, picked);
                await PrepareAsync(lesson, prepare);
            }));
        }

        Options = options;
    }

    /// <summary>
    /// Ключ занятия
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Занятие
    /// </summary>
    public Lesson Lesson { get; }

    /// <summary>
    /// Вызывается, когда нужно показать расшифровку этого занятия
    /// </summary>
    public Action<LessonFormatViewModel>? Opened { get; set; }

    /// <summary>
    /// Открывает расшифровку занятия на странице
    /// </summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>
    /// Расшифровка этого занятия открыта на странице
    /// </summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>
    /// Готовые файлы с заданием: они показываются на вкладке «Домашние задания»
    /// </summary>
    public IReadOnlyList<DraftFileViewModel> Drafts
    {
        get => _drafts;
        set
        {
            if (Set(ref _drafts, value))
                Raise(nameof(HasDrafts));
        }
    }

    /// <summary>
    /// У занятия есть готовые файлы
    /// </summary>
    public bool HasDrafts => Drafts.Count > 0;

    /// <summary>
    /// У занятия уже есть расшифровка: тогда можно подготовить задания
    /// </summary>
    public bool HasTranscript
    {
        get => _hasTranscript;
        set
        {
            if (!Set(ref _hasTranscript, value))
                return;

            Raise(nameof(CanFetch));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Расшифровки ещё нет: можно заказать
    /// </summary>
    public bool CanFetch => !HasTranscript && FetchCommand.CanExecute(null);

    /// <summary>
    /// Расшифровку уже заказали
    /// </summary>
    public bool FetchRequested
    {
        get => _fetchRequested;
        private set
        {
            if (!Set(ref _fetchRequested, value))
                return;

            Raise(nameof(CanFetch));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Меню выбора формата раскрыто
    /// </summary>
    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set => Set(ref _isMenuOpen, value);
    }

    /// <summary>
    /// Вид сферы в индикаторе подготовки
    /// </summary>
    public ActivityKindEnum Kind => ActivityKindEnum.Searching;

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
    /// Когда подготовка закончилась
    /// </summary>
    public DateTimeOffset ActivitySince
    {
        get => _activitySince;
        private set => Set(ref _activitySince, value);
    }

    /// <summary>
    /// Текст статуса
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
    /// Подготовка закончилась ошибкой
    /// </summary>
    public bool Failed
    {
        get => _failed;
        private set => Set(ref _failed, value);
    }

    /// <summary>
    /// Есть что сказать о ходе подготовки
    /// </summary>
    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// Цвет сферы: нейтральный
    /// </summary>
    public PanelToneEnum Tone => PanelToneEnum.Idle;

    /// <summary>
    /// Раскрывает меню выбора формата. Сами задания ищутся после выбора формата в меню
    /// </summary>
    public RelayCommand PrepareCommand { get; }

    /// <summary>
    /// Заказывает расшифровку занятия
    /// </summary>
    public RelayCommand FetchCommand { get; }

    /// <summary>
    /// Название предмета
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Дата занятия
    /// </summary>
    public string Date { get; }

    /// <summary>
    /// Дата, подпись занятия и длительность записи одной строкой
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Название занятия для заголовка читалки: предмет и подпись, например «Гейм-дизайн, Лекция 3»
    /// </summary>
    public string Caption => Lesson.Label.Length > 0 ? $"{Lesson.Title}, {Lesson.Label}" : Lesson.Title;

    /// <summary>
    /// Первая строка карточки: дата и название занятия
    /// </summary>
    public string Headline { get; }

    /// <summary>
    /// Вторая строка карточки: длительность записи
    /// </summary>
    public string Duration { get; }

    /// <summary>
    /// У записи известна длительность
    /// </summary>
    public bool HasDuration => Duration.Length > 0;

    /// <summary>
    /// Форматы в меню подготовки. Нажатие выбирает формат и сразу запускает поиск заданий. Формат по умолчанию отмечен
    /// </summary>
    public IReadOnlyList<FormatOptionViewModel> Options { get; }

    /// <summary>
    /// Отмечает в меню формат по умолчанию, когда его сменили в карточке предмета
    /// </summary>
    /// <param name="format">Новый формат по умолчанию</param>
    public void SetDefaultFormat(HomeworkFormatEnum format)
    {
        foreach (var option in Options)
            option.IsSelected = option.Format == format;
    }

    /// <summary>
    /// Ищет задания в расшифровке и показывает ход поиска
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="prepare">Ищет задания: возвращает их число и текст ошибки</param>
    /// <returns>Задача, которая завершается, когда поиск закончен</returns>
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
