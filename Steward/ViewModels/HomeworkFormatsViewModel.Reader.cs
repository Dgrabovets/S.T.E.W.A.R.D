using System.IO;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Models;
using Steward.Mvvm;
using Steward.Preview;

namespace Steward.ViewModels;

/// <summary>
/// Читалка расшифровки: разбор встречи, вкладки, место, где сказано задание, открытие и закрытие занятия
/// </summary>
public sealed partial class HomeworkFormatsViewModel
{
    /// <summary>
    /// Место в расшифровке, где сказано задание, из которого открыта читалка. Пусто, если читалка открыта не из карточки задания
    /// </summary>
    private QuoteViewModel? _quote;

    /// <summary>
    /// Показана вкладка «Где сказано»
    /// </summary>
    public bool IsQuoteTab => ActiveReaderTab == ReaderTabEnum.Quote && HasReaderTabs;

    /// <summary>
    /// Место в расшифровке, где сказано задание
    /// </summary>
    public QuoteViewModel? Quote => _quote;

    /// <summary>
    /// Тема открытой встречи
    /// </summary>
    public string ReadingTopic => _reading.Topic;

    /// <summary>
    /// Что было на занятии, связным текстом
    /// </summary>
    public string ReadingOverview => _reading.Overview;

    /// <summary>
    /// Ключевые моменты: что пройдено на занятии
    /// </summary>
    public IReadOnlyList<string> ReadingPoints => _reading.Points;

    /// <summary>
    /// Принятые решения
    /// </summary>
    public IReadOnlyList<string> ReadingDecisions => _reading.Decisions;

    /// <summary>
    /// Задачи, которые прозвучали на занятии
    /// </summary>
    public IReadOnlyList<string> ReadingTasks => _reading.Tasks;

    /// <summary>
    /// Разобранные темы с подробностями
    /// </summary>
    public IReadOnlyList<AnalysisTopic> ReadingTopics => _reading.Topics;

    /// <summary>
    /// Важные формулировки, которые прозвучали на занятии
    /// </summary>
    public IReadOnlyList<string> ReadingFormulations => _reading.Formulations;

    /// <summary>
    /// Есть разобранные темы
    /// </summary>
    public bool HasReadingTopics => _reading.Topics.Count > 0;

    /// <summary>
    /// Есть формулировки
    /// </summary>
    public bool HasReadingFormulations => _reading.Formulations.Count > 0;

    /// <summary>
    /// Подробного разбора ещё нет: показано только то, что было в резюме MTS Link
    /// </summary>
    public bool ReadingBrief => !_reading.Detailed && !ReadingEmpty;

    /// <summary>
    /// Есть принятые решения
    /// </summary>
    public bool HasReadingDecisions => _reading.Decisions.Count > 0;

    /// <summary>
    /// Есть задачи, которые нужно показать в разделе решений. У подробного разбора их нет: те же задания показывает вкладка «Задания», где к ним есть слова преподавателя и кнопки
    /// </summary>
    public bool HasReadingTasks => !_reading.Detailed && _reading.Tasks.Count > 0;

    /// <summary>
    /// Кто сколько говорил, в процентах от всех слов
    /// </summary>
    public IReadOnlyList<ChartShareViewModel> ReadingSpeakers => [.. _reading.Speakers.Take(6).Select(speaker => new ChartShareViewModel(speaker.Name, _reading.Words == 0 ? 0 : (int)Math.Round(100.0 * speaker.Words / _reading.Words), _reading.Speakers[0].Words == 0 ? 0 : (double)speaker.Words / _reading.Speakers[0].Words, PanelToneEnum.Idle))];

    /// <summary>
    /// Есть статистика по репликам
    /// </summary>
    public bool HasReadingSpeakers => _reading.Speakers.Count > 0;

    /// <summary>
    /// Читает настройки предмета: автоподготовка и источник заданий. Пусто: настройки по умолчанию
    /// </summary>
    public Func<string, SubjectOptions>? SubjectOptionsReader { get; set; }

    /// <summary>
    /// Записывает настройки предмета. Вызывается в фоне и может выбросить исключение. Пусто: настройки предмета не показываются
    /// </summary>
    public Action<string, SubjectOptions>? SubjectOptionsSaver { get; set; }

    /// <summary>
    /// Выбранная вкладка читалки. Если в ней нет содержимого, показывается первая из доступных
    /// </summary>
    private ReaderTabEnum _readerTab = ReaderTabEnum.Covered;

    /// <summary>
    /// Вкладки читалки, в которых для этого занятия есть что показать, в порядке показа
    /// </summary>
    public IReadOnlyList<ReaderTabViewModel> ReaderTabs
    {
        get
        {
            var shown = ActiveReaderTab;

            return [.. AvailableReaderTabs().Select(item => new ReaderTabViewModel(item.Tab, item.Title, item.Tab == shown, new RelayCommand(() => SelectReaderTab(item.Tab))))];
        }
    }

    /// <summary>
    /// У занятия есть хотя бы один раздел для вкладок
    /// </summary>
    public bool HasReaderTabs => AvailableReaderTabs().Count > 0;

    /// <summary>
    /// Показан раздел «Пройдено»
    /// </summary>
    public bool IsCoveredTab => ActiveReaderTab == ReaderTabEnum.Covered && HasReaderTabs;

    /// <summary>
    /// Показан раздел «Темы»
    /// </summary>
    public bool IsTopicsTab => ActiveReaderTab == ReaderTabEnum.Topics && HasReaderTabs;

    /// <summary>
    /// Показан раздел «Решения» («Решения и задачи» у короткого резюме)
    /// </summary>
    public bool IsActionsTab => ActiveReaderTab == ReaderTabEnum.Actions && HasReaderTabs;

    /// <summary>
    /// Показан раздел «Кто говорил»
    /// </summary>
    public bool IsSpeakersTab => ActiveReaderTab == ReaderTabEnum.Speakers && HasReaderTabs;

    /// <summary>
    /// Какая вкладка читалки показана сейчас: выбранная, а если в ней пусто, то первая с содержимым
    /// </summary>
    public ReaderTabEnum ActiveReaderTab
    {
        get
        {
            var available = AvailableReaderTabs();

            return available.Any(item => item.Tab == _readerTab) || available.Count == 0 ? _readerTab : available[0].Tab;
        }
    }

    /// <summary>
    /// Собирает вкладки, у которых есть содержимое
    /// </summary>
    /// <returns>Вкладки и их названия по порядку</returns>
    private List<(ReaderTabEnum Tab, string Title)> AvailableReaderTabs()
    {
        var tabs = new List<(ReaderTabEnum, string)>();
        if (_quote is not null)
            tabs.Add((ReaderTabEnum.Quote, "Где сказано"));

        if (HasReadingPoints || HasReadingOverview)
            tabs.Add((ReaderTabEnum.Covered, "Пройдено"));

        if (HasAssignments)
            tabs.Add((ReaderTabEnum.Assignments, "Задания"));

        if (HasReadingTopics || HasReadingFormulations)
            tabs.Add((ReaderTabEnum.Topics, "Темы"));

        if (HasReadingDecisions || HasReadingTasks)
            tabs.Add((ReaderTabEnum.Actions, _reading.Detailed ? "Решения" : "Решения и задачи"));

        if (HasReadingSpeakers)
            tabs.Add((ReaderTabEnum.Speakers, "Кто говорил"));

        return tabs;
    }

    /// <summary>
    /// Показывает раздел выбранной вкладки
    /// </summary>
    /// <param name="tab">Вкладка</param>
    private void SelectReaderTab(ReaderTabEnum tab)
    {
        _readerTab = tab;
        RaiseReaderTabs();
    }

    /// <summary>
    /// Сообщает окну, что вкладки читалки или выбранная вкладка изменились
    /// </summary>
    private void RaiseReaderTabs()
    {
        Raise(nameof(ReaderTabs));
        Raise(nameof(HasReaderTabs));
        Raise(nameof(IsQuoteTab));
        Raise(nameof(Quote));
        Raise(nameof(IsCoveredTab));
        Raise(nameof(IsTopicsTab));
        Raise(nameof(IsActionsTab));
        Raise(nameof(IsAssignmentsTab));
        Raise(nameof(Assignments));
        Raise(nameof(IsSpeakersTab));
    }

    /// <summary>
    /// Показатели занятия: дата, длительность, слов, участников
    /// </summary>
    public IReadOnlyList<StatusTileViewModel> ReadingStats => OpenLesson is null ? [] :
    [
        new StatusTileViewModel("Дата", OpenLesson.Date, PanelToneEnum.Idle),
        new StatusTileViewModel("Длительность", OpenLesson.HasDuration ? OpenLesson.Duration : "не указана", PanelToneEnum.Idle),
        new StatusTileViewModel("Пройдено", _reading.Points.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), PanelToneEnum.Idle),
        new StatusTileViewModel("Участников", _reading.Speakers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), PanelToneEnum.Idle),
        new StatusTileViewModel("Слов", _reading.Words.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")), PanelToneEnum.Idle),
    ];

    /// <summary>
    /// Сколько пунктов пройдено на занятии
    /// </summary>
    public string ReadingCount => _reading.Points.Count == 0 ? "Пунктов нет" : $"Пройдено пунктов: {_reading.Points.Count}";

    /// <summary>
    /// У встречи есть тема
    /// </summary>
    public bool HasReadingTopic => _reading.Topic.Length > 0;

    /// <summary>
    /// Есть ключевые моменты
    /// </summary>
    public bool HasReadingPoints => _reading.Points.Count > 0;

    /// <summary>
    /// Есть краткое содержание
    /// </summary>
    public bool HasReadingOverview => _reading.Overview.Length > 0;

    /// <summary>
    /// Разбор расшифровки пустой: резюме в файле нет
    /// </summary>
    public bool ReadingEmpty => _reading.Topic.Length == 0 && _reading.Overview.Length == 0 && _reading.Points.Count == 0;

    /// <summary>
    /// Показывает расшифровку занятия: резюме и полный текст
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void OpenTranscript(LessonFormatViewModel lesson)
    {
        foreach (var other in Lessons)
            other.IsOpen = other == lesson;

        _reading = TranscriptText.Parse(TranscriptReader?.Invoke(lesson.Id) ?? string.Empty, AnalysisReader?.Invoke(lesson.Id));
        _quote = null;
        _assignments = BuildAssignments(lesson);
        _readerTab = ReaderTabEnum.Covered;
        OpenLesson = lesson;
        RaiseReading();
    }

    /// <summary>
    /// Закрывает расшифровку
    /// </summary>
    private void CloseLesson()
    {
        foreach (var lesson in Lessons)
            lesson.IsOpen = false;

        OpenLesson = null;
        _reading = TranscriptText.Empty;
        RaiseReading();
    }

    /// <summary>
    /// Сообщает окну, что разбор расшифровки изменился
    /// </summary>
    private void RaiseReading()
    {
        Raise(nameof(ReadingTopic));
        Raise(nameof(ReadingOverview));
        Raise(nameof(ReadingPoints));
        Raise(nameof(ReadingCount));
        Raise(nameof(ReadingEmpty));
        Raise(nameof(HasReadingTopic));
        Raise(nameof(HasReadingPoints));
        Raise(nameof(HasReadingOverview));
        Raise(nameof(ReadingDecisions));
        Raise(nameof(ReadingTasks));
        Raise(nameof(HasReadingDecisions));
        Raise(nameof(ReadingTopics));
        Raise(nameof(ReadingFormulations));
        Raise(nameof(HasReadingTopics));
        Raise(nameof(HasReadingFormulations));
        Raise(nameof(ReadingBrief));
        Raise(nameof(HasReadingTasks));
        Raise(nameof(ReadingSpeakers));
        Raise(nameof(HasReadingSpeakers));
        Raise(nameof(ReadingStats));
        RaiseReaderTabs();
    }
}
