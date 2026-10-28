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
    /// <param name="prepare">Ищет задания в расшифровке занятия и готовит их в выбранном формате: возвращает число заданий и текст ошибки</param>
    /// <param name="fetchRequested">Вызывается по кнопке «Расшифровать»: просит забрать расшифровку занятия, даже если предмет не отмечен. Пусто: кнопки нет</param>
    public LessonFormatViewModel(Lesson lesson, HomeworkFormatEnum format, Action<string, HomeworkFormatEnum> chosen, bool hasTranscript, Func<Lesson, PrepareRequest, Task<(int Count, string Error)>> prepare, Action<Lesson>? fetchRequested = null)
    {
        Id = lesson.Id;
        _hasTranscript = hasTranscript;
        Lesson = lesson;
        Format = format;
        _chosen = chosen;
        _prepare = prepare;
        PrepareCommand = new RelayCommand(() => PrepareRequested?.Invoke(this), () => Activity != ActivityStateEnum.Working);
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
    }

    /// <summary>
    /// Запоминает формат занятия
    /// </summary>
    private readonly Action<string, HomeworkFormatEnum> _chosen;

    /// <summary>
    /// Ищет задания и готовит работы
    /// </summary>
    private readonly Func<Lesson, PrepareRequest, Task<(int Count, string Error)>> _prepare;

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
    /// Формат, в котором готовили или будут готовить задания занятия
    /// </summary>
    public HomeworkFormatEnum Format { get; private set; }

    /// <summary>
    /// Вызывается, когда нужно открыть окно «Подготовить задание» для этого занятия
    /// </summary>
    public Action<LessonFormatViewModel>? PrepareRequested { get; set; }

    /// <summary>
    /// Отмечает в меню формат по умолчанию, когда его сменили в карточке предмета
    /// </summary>
    /// <param name="format">Новый формат по умолчанию</param>
    public void SetDefaultFormat(HomeworkFormatEnum format) => Format = format;

    /// <summary>
    /// Запоминает выбранный в окне формат и запускает подготовку заданий
    /// </summary>
    /// <param name="request">Формат, дополнительное указание и материалы, которые нужно учесть</param>
    /// <returns>Задача, которая завершается, когда подготовка закончена</returns>
    public Task PrepareAsync(PrepareRequest request)
    {
        _chosen(Lesson.Id, request.Format);
        Format = request.Format;

        return RunPrepareAsync(Lesson, request);
    }

    /// <summary>
    /// Ищет задания в расшифровке, готовит работы и показывает ход подготовки
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="request">Что выбрано в окне</param>
    /// <returns>Задача, которая завершается, когда подготовка закончена</returns>
    private async Task RunPrepareAsync(Lesson lesson, PrepareRequest request)
    {
        Failed = false;
        Status = "Ищу задания и готовлю работы…";
        Activity = ActivityStateEnum.Working;

        (int Count, string Error) result;
        try
        {
            result = await _prepare(lesson, request);
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
