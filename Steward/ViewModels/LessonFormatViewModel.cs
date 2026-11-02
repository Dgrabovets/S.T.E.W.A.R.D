using System.Globalization;
using System.Windows.Input;
using Steward.Homework.Models;
using Steward.Homework.Services;
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
    /// Найденные задания
    /// </summary>
    private IReadOnlyList<LessonItemViewModel> _items = [];

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
        RedoCommand = new RelayCommand(() => RedoRequested?.Invoke(this), () => Activity != ActivityStateEnum.Working);
        DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(this), () => Activity != ActivityStateEnum.Working);
        CheckCommand = new RelayCommand(() => CheckRequested?.Invoke(this), () => Activity != ActivityStateEnum.Working);
        OpenCheckCommand = new RelayCommand(() => CheckOpened?.Invoke(this), () => HasCheck);
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
    /// Есть отчёт о проверке готовой работы
    /// </summary>
    private bool _hasCheck;

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
    /// Занятие свежее: последнее у предмета. Остальные уже прошли, и по ним всё должно быть разобрано
    /// </summary>
    private bool _isCurrent = true;

    /// <summary>
    /// Сколько заданий из разбора ещё без реакции владельца
    /// </summary>
    private int _openAssignments;

    /// <summary>
    /// Занятие свежее: последнее у предмета, оно не выделяется цветом
    /// </summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (!Set(ref _isCurrent, value))
                return;

            Raise(nameof(IsPast));
            Raise(nameof(Age));
        }
    }

    /// <summary>
    /// Занятие уже прошло: на плашке оно приглушено и подписано, сколько времени назад было
    /// </summary>
    public bool IsPast => !_isCurrent;

    /// <summary>
    /// Сколько прошло с занятия: «вчера», «5 дн. назад». Пусто у свежего занятия
    /// </summary>
    public string Age => _isCurrent ? string.Empty : AgeOf(Lesson.Date, DateOnly.FromDateTime(DateTime.Today));

    /// <summary>
    /// Описывает, сколько прошло со дня занятия
    /// </summary>
    /// <param name="date">День занятия</param>
    /// <param name="today">Сегодняшний день</param>
    /// <returns>«сегодня», «вчера», «5 дн. назад», «3 нед. назад» или «2 мес. назад»</returns>
    public static string AgeOf(DateOnly date, DateOnly today)
    {
        var days = today.DayNumber - date.DayNumber;

        return days switch
        {
            <= 0 => "сегодня",
            1 => "вчера",
            < 14 => $"{days} дн. назад",
            < 60 => $"{days / 7} нед. назад",
            _ => $"{days / 30} мес. назад",
        };
    }

    /// <summary>
    /// Сколько заданий из разбора ещё без реакции владельца: их нет в списке заданий и в Singularity, и они не убраны
    /// </summary>
    public int OpenAssignments
    {
        get => _openAssignments;
        set
        {
            if (!Set(ref _openAssignments, value))
                return;

            Raise(nameof(HasOpenAssignments));
            Raise(nameof(OpenAssignmentsText));
        }
    }

    /// <summary>
    /// В занятии есть задания, на которые владелец ещё не отреагировал
    /// </summary>
    public bool HasOpenAssignments => _openAssignments > 0;

    /// <summary>
    /// Плашка «Не разобрано: 2»
    /// </summary>
    public string OpenAssignmentsText => $"Не разобрано: {_openAssignments}";

    /// <summary>
    /// Готовые файлы с заданием: они показываются на вкладке «Домашние задания»
    /// </summary>
    public IReadOnlyList<DraftFileViewModel> Drafts
    {
        get => _drafts;
        set
        {
            if (!Set(ref _drafts, value))
                return;

            Raise(nameof(HasDrafts));
            Raise(nameof(CanPrepare));
            Raise(nameof(HasDraftsWithoutItems));
            Raise(nameof(OtherDrafts));
        }
    }

    /// <summary>
    /// Готовые файлы, которые не относятся ни к одному заданию: их кнопки «Посмотреть задание» остаются в шапке занятия, а работы заданий открываются главной кнопкой карточки
    /// </summary>
    public IReadOnlyList<DraftFileViewModel> OtherDrafts =>
        [.. Drafts.Where(draft => !Items.Any(item => string.Equals(item.WorkPath, draft.Path, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>
    /// Готовые файлы есть, а найденных заданий нет: тогда проверка, переделка и удаление показываются кнопками занятия, ведь меню «…» у заданий нет
    /// </summary>
    public bool HasDraftsWithoutItems => HasDrafts && !HasItems;

    /// <summary>
    /// У занятия есть готовые файлы
    /// </summary>
    public bool HasDrafts => Drafts.Count > 0;

    /// <summary>
    /// Кнопка «Подготовить задание» нужна: готовых файлов ещё нет. Когда они есть, вместо неё кнопки «Посмотреть задание»
    /// </summary>
    public bool CanPrepare => !HasDrafts && PendingCount > 0;

    /// <summary>
    /// Найденные в расшифровке задания, кроме убранных в архив. <see langword="null"/>, пока задания не искали
    /// </summary>
    public IReadOnlyList<LessonItemViewModel> Items
    {
        get => _items;
        private set
        {
            if (!Set(ref _items, value))
                return;

            Raise(nameof(HasItems));
            Raise(nameof(HasDraftsWithoutItems));
            Raise(nameof(OtherDrafts));
            Raise(nameof(PendingCount));
            Raise(nameof(AllSubmitted));
            Raise(nameof(CanPrepare));
            Raise(nameof(ItemsHint));
            Raise(nameof(HasItemsHint));
        }
    }

    /// <summary>
    /// Есть найденные задания
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Сколько заданий ещё нужно сделать: не сданы и не в архиве
    /// </summary>
    public int PendingCount => Items.Count(item => !item.IsDone);

    /// <summary>
    /// Все задания занятия сданы или в архиве: строку списка приглушают
    /// </summary>
    public bool AllSubmitted => PipelineStepper.LessonGroup(Items.Count, PendingCount) == 2;

    /// <summary>
    /// Подсказка, когда заданий нет: из расшифровки задание добавляется только вручную
    /// </summary>
    public string ItemsHint => HasItems ? string.Empty : "Заданий пока нет. Если преподаватель дал задание, открой расшифровку и добавь его на вкладке «Задания»";

    /// <summary>
    /// Есть подсказка про задания
    /// </summary>
    public bool HasItemsHint => ItemsHint.Length > 0;

    /// <summary>
    /// Показывает итог действия над заданием: «Задача заведена» или причину ошибки
    /// </summary>
    /// <param name="text">Текст итога</param>
    /// <param name="failed">Это ошибка</param>
    public void ShowResult(string text, bool failed)
    {
        Failed = failed;
        Status = text;
    }

    /// <summary>
    /// Обновляет список найденных заданий
    /// </summary>
    /// <param name="items">Строки заданий</param>
    public void SetItems(IReadOnlyList<LessonItemViewModel> items)
    {
        // Сравнивается всё, что видно на карточке: шаг, вопрос, подсказка, файл работы. Иначе после «Подтвердить» вопрос остался бы на экране
        if (!items.Select(item => item.StateKey).SequenceEqual(Items.Select(item => item.StateKey), StringComparer.Ordinal))
            Items = items;
    }

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
    /// Вызывается, когда нужно открыть окно «Переделать» для этого занятия
    /// </summary>
    public Action<LessonFormatViewModel>? RedoRequested { get; set; }

    /// <summary>
    /// Вызывается, когда нужно удалить готовое задание этого занятия
    /// </summary>
    public Action<LessonFormatViewModel>? DeleteRequested { get; set; }

    /// <summary>
    /// Просит открыть окно, где записывают, что не понравилось, и переделать работу
    /// </summary>
    public RelayCommand RedoCommand { get; }

    /// <summary>
    /// Удаляет готовое задание
    /// </summary>
    public RelayCommand DeleteCommand { get; }

    /// <summary>
    /// Вызывается, когда нужно проверить готовую работу
    /// </summary>
    public Action<LessonFormatViewModel>? CheckRequested { get; set; }

    /// <summary>
    /// Вызывается, когда нужно открыть отчёт о проверке
    /// </summary>
    public Action<LessonFormatViewModel>? CheckOpened { get; set; }

    /// <summary>
    /// Просит проверить готовую работу: сверить с условием и указаниями
    /// </summary>
    public RelayCommand CheckCommand { get; }

    /// <summary>
    /// Открывает отчёт о проверке
    /// </summary>
    public RelayCommand OpenCheckCommand { get; }

    /// <summary>
    /// У занятия есть отчёт о проверке готовой работы
    /// </summary>
    public bool HasCheck
    {
        get => _hasCheck;
        set => Set(ref _hasCheck, value);
    }

    /// <summary>
    /// Запускает проверку готовой работы и показывает ход
    /// </summary>
    /// <param name="check">Проверяет работу и возвращает пустую строку или причину ошибки</param>
    /// <returns>Задача, которая завершается, когда проверка закончена</returns>
    public async Task RunCheckAsync(Func<Lesson, Task<string>> check)
    {
        Failed = false;
        Status = "Проверяю работу: сверяю с условием и указаниями…";
        Activity = ActivityStateEnum.Working;

        string error;
        try
        {
            error = await check(Lesson);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        Failed = error.Length > 0;
        Status = Failed ? error : "Проверка готова: отчёт можно открыть кнопкой «Результат проверки»";
        ActivitySince = DateTimeOffset.UtcNow;
        Activity = Failed ? ActivityStateEnum.None : ActivityStateEnum.Done;
        HasCheck = !Failed;
    }

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
