using System.Globalization;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Карточка домашнего задания, которое нашли в расшифровке занятия: название, срок, требования, строка шагов конвейера, одна главная кнопка по шагу, вопрос «Нужно подтвердить», блок «Откуда это задание» и меню «…»
/// </summary>
public sealed class LessonItemViewModel
{
    /// <summary>
    /// Самая длинная часть требований на карточке в знаках
    /// </summary>
    private const int REQUIREMENTS_LIMIT = 220;

    /// <summary>
    /// Сколько дней после правки условия в LMS на карточке остаётся пометка
    /// </summary>
    private const int CHANGED_DAYS = 14;

    /// <summary>
    /// Создаёт строку задания
    /// </summary>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="item">Задание</param>
    /// <param name="inSingularity">Задача по заданию уже заведена в SingularityApp</param>
    /// <param name="archive">Убирает задание в архив</param>
    /// <param name="addToSingularity">Заводит задачу в SingularityApp</param>
    /// <param name="openInLms">Открывает страницу задания в LMS</param>
    /// <param name="toggleSubmitted">Отмечает задание сданным или снимает отметку</param>
    /// <param name="copyCondition">Копирует условие задания в буфер обмена</param>
    /// <param name="pipeline">Шаг конвейера, файл работы, оценка и источник задания; <see langword="null"/>, если их не читали: тогда шаг выводится из отметки «сдано»</param>
    /// <param name="actions">Действия главной кнопки, вопроса, блока источника и меню «…»; <see langword="null"/>, если их нет</param>
    /// <param name="viewed">Работу уже открывали: главная кнопка предлагает отметить сданным</param>
    public LessonItemViewModel(int number, HomeworkItem item, bool inSingularity, Action<LessonItemViewModel> archive, Action<LessonItemViewModel> addToSingularity, Action<LessonItemViewModel>? openInLms = null, Action<LessonItemViewModel>? toggleSubmitted = null, Action<LessonItemViewModel>? copyCondition = null, ItemPipelineInfo? pipeline = null, ItemCardActions? actions = null, bool viewed = false)
    {
        var act = actions ?? new ItemCardActions();
        FullRequirements = item.Requirements;
        Step = pipeline?.Step ?? (item.Done ? PipelineStepEnum.Submitted : PipelineStepEnum.Found);
        WorkPath = pipeline?.WorkPath ?? string.Empty;
        Viewed = viewed;
        // Пока шаги не прочитаны, строка шагов нейтральная: ни один шаг не выделен, чтобы не показать неверный
        PipelineKnown = pipeline is not null;
        Steps = [.. StepTitles.Select((title, index) => new PipelineStepViewModel(title, PipelineKnown && index == (int)Step, PipelineKnown && index < (int)Step, index == 0))];
        Hint = pipeline?.Hint ?? string.Empty;
        HintIsProblem = pipeline?.HintIsProblem ?? false;
        var assessment = pipeline?.Assessment;
        NeedsConfirmation = Step == PipelineStepEnum.Found && assessment?.Readiness == ItemReadinessEnum.NeedsConfirmation;
        Question = NeedsConfirmation ? assessment!.Question : string.Empty;
        Note = assessment?.Readiness == ItemReadinessEnum.ReadyWithNote && Step != PipelineStepEnum.Submitted ? NoteText(assessment.Question) : string.Empty;
        var evidence = item.Evidence.Trim().Trim('"', '«', '»', '„', '“', '”').Trim();
        Quote = evidence.Length > 0 ? $"«{evidence}»" : string.Empty;
        var verified = pipeline?.EvidenceVerified ?? false;
        var linked = pipeline?.LinkedToLms ?? false;
        (Reliability, ReliabilityWarning) = (verified, linked, evidence.Length > 0) switch
        {
            _ when !PipelineKnown => (string.Empty, false),
            (true, true, _) => ("подтверждено цитатой и LMS", false),
            (true, false, _) => ("только расшифровка", false),
            (false, _, true) => ("цитата не найдена", true),
            (false, true, false) => ("только LMS", false),
            _ => ("расшифровка без цитаты", false),
        };
        IsConfirmedSource = verified && linked;
        LinkedToLms = linked;
        HasLmsPage = linked || item.Link.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        var hasWork = WorkPath.Length > 0;
        MainAction = Step switch
        {
            PipelineStepEnum.Found when toggleSubmitted is not null && !item.Done => ItemMainActionEnum.MarkSubmitted,
            PipelineStepEnum.Ready or PipelineStepEnum.Checked when hasWork && !viewed && act.ViewWork is not null => ItemMainActionEnum.ViewWork,
            PipelineStepEnum.Ready or PipelineStepEnum.Checked when toggleSubmitted is not null && !item.Done => ItemMainActionEnum.MarkSubmitted,
            PipelineStepEnum.Submitted when item.Submitted && toggleSubmitted is not null => ItemMainActionEnum.ReturnToWork,
            _ => ItemMainActionEnum.None,
        };
        MainLabel = MainAction switch
        {
            ItemMainActionEnum.ViewWork => "Посмотреть работу",
            ItemMainActionEnum.MarkSubmitted => "Отметить сданным",
            ItemMainActionEnum.ReturnToWork => "Вернуть в работу",
            _ => string.Empty,
        };
        MainIsQuiet = MainAction == ItemMainActionEnum.ReturnToWork || (MainAction == ItemMainActionEnum.MarkSubmitted && Step == PipelineStepEnum.Found);
        MainCommand = new RelayCommand(
            () =>
            {
                switch (MainAction)
                {
                    case ItemMainActionEnum.ViewWork:
                        act.ViewWork?.Invoke(this);
                        break;
                    case ItemMainActionEnum.MarkSubmitted or ItemMainActionEnum.ReturnToWork:
                        toggleSubmitted?.Invoke(this);
                        break;
                }
            },
            () => HasMain);
        OpenWorkCommand = new RelayCommand(() => act.ViewWork?.Invoke(this), () => hasWork && act.ViewWork is not null);
        ConfirmCommand = new RelayCommand(() => act.Confirm?.Invoke(this), () => NeedsConfirmation && act.Confirm is not null);
        FixConditionCommand = new RelayCommand(() => act.FixCondition?.Invoke(this), () => act.FixCondition is not null);
        RecheckCommand = new RelayCommand(() => act.Recheck?.Invoke(this), () => hasWork && act.Recheck is not null);
        OpenCheckCommand = new RelayCommand(() => act.OpenCheck?.Invoke(this), () => Step == PipelineStepEnum.Checked && act.OpenCheck is not null);
        RedoCommand = new RelayCommand(() => act.Redo?.Invoke(this), () => hasWork && act.Redo is not null);
        AttachWorkCommand = new RelayCommand(() => act.AttachWork?.Invoke(this), () => act.AttachWork is not null);
        SubmittedFileCommand = new RelayCommand(() => act.SubmittedFile?.Invoke(this), () => Step == PipelineStepEnum.Submitted && act.SubmittedFile is not null);
        DeleteDraftsCommand = new RelayCommand(() => act.DeleteDrafts?.Invoke(this), () => hasWork && act.DeleteDrafts is not null);
        OpenTranscriptCommand = new RelayCommand(() => act.OpenTranscript?.Invoke(this), () => act.OpenTranscript is not null);

        Number = number;
        Title = item.Title;
        IsDone = item.Done;
        InSingularity = inSingularity;
        Requirements = item.Requirements.Length > REQUIREMENTS_LIMIT ? item.Requirements[..REQUIREMENTS_LIMIT].TrimEnd() + "…" : item.Requirements;
        Meta = string.Join("  ·  ", new[]
        {
            item.Due is { } due ? $"Срок: {due.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : "Срок не назван",
            item.Done ? "уже сдано" : string.Empty,
            inSingularity ? "задача в Singularity" : string.Empty,
            item.Changed is { } changed && changed >= DateOnly.FromDateTime(DateTime.Now).AddDays(-CHANGED_DAYS) ? $"условие в LMS изменено {changed.ToString("dd.MM", CultureInfo.InvariantCulture)}" : string.Empty,
        }.Where(part => part.Length > 0));
        ArchiveCommand = new RelayCommand(() => archive(this));
        AddToSingularityCommand = new RelayCommand(() => addToSingularity(this), () => !InSingularity);
        Submitted = item.Submitted;
        OpenInLmsCommand = new RelayCommand(() => openInLms?.Invoke(this), () => openInLms is not null);
        ToggleSubmittedCommand = new RelayCommand(() => toggleSubmitted?.Invoke(this), () => toggleSubmitted is not null && (!IsDone || Submitted));
        CopyConditionCommand = new RelayCommand(() => copyCondition?.Invoke(this), () => copyCondition is not null);
    }

    /// <summary>
    /// Номер задания в занятии, с единицы
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Что нужно сделать
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Срок и пометки
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Требования к работе, начало
    /// </summary>
    public string Requirements { get; }

    /// <summary>
    /// У задания есть требования
    /// </summary>
    public bool HasRequirements => Requirements.Length > 0;

    /// <summary>
    /// По данным LMS задание уже сдано
    /// </summary>
    public bool IsDone { get; }

    /// <summary>
    /// Задача по заданию уже заведена в SingularityApp
    /// </summary>
    public bool InSingularity { get; }

    /// <summary>
    /// Задание отмечено сданным вручную: отметку можно снять
    /// </summary>
    public bool Submitted { get; }

    /// <summary>
    /// Подпись кнопки сдачи: поставить или снять отметку
    /// </summary>
    public string SubmitLabel => Submitted ? "Вернуть в работу" : "Отметить сданным";

    /// <summary>
    /// Кнопка сдачи нужна: задание ещё не сдано или отмечено сданным вручную. Сдачу по данным LMS отменить нельзя
    /// </summary>
    public bool CanToggleSubmitted => !IsDone || Submitted;

    /// <summary>
    /// Убирает задание в архив
    /// </summary>
    public RelayCommand ArchiveCommand { get; }

    /// <summary>
    /// Открывает страницу задания в LMS
    /// </summary>
    public RelayCommand OpenInLmsCommand { get; }

    /// <summary>
    /// Отмечает задание сданным или снимает отметку
    /// </summary>
    public RelayCommand ToggleSubmittedCommand { get; }

    /// <summary>
    /// Копирует условие задания в буфер обмена
    /// </summary>
    public RelayCommand CopyConditionCommand { get; }

    /// <summary>
    /// Заводит задачу в SingularityApp
    /// </summary>
    public RelayCommand AddToSingularityCommand { get; }

    /// <summary>
    /// Названия шагов конвейера в порядке <see cref="PipelineStepEnum"/>
    /// </summary>
    private static readonly string[] StepTitles = ["Найдено", "Готово", "Проверено", "Сдано"];

    /// <summary>
    /// Начало пометки о средней уверенности
    /// </summary>
    private const string NOTE_PREFIX = "Проверь формулировку";

    /// <summary>
    /// Текст пометки «проверь формулировку» с сомнением агента
    /// </summary>
    /// <param name="doubt">Сомнение или готовая пометка из оценки</param>
    /// <returns>Пометка одной строкой</returns>
    private static string NoteText(string doubt) =>
        doubt.Trim().Length == 0 || doubt.StartsWith(NOTE_PREFIX, StringComparison.OrdinalIgnoreCase) ? NOTE_PREFIX : $"{NOTE_PREFIX}: {doubt.Trim()}";

    /// <summary>
    /// Требования к работе целиком: ими заполняется окно «Поправить условие»
    /// </summary>
    public string FullRequirements { get; }

    /// <summary>
    /// Шаг конвейера задания
    /// </summary>
    public PipelineStepEnum Step { get; }

    /// <summary>
    /// Строка шагов «Найдено → Готово → Проверено → Сдано» с выделенным текущим
    /// </summary>
    public IReadOnlyList<PipelineStepViewModel> Steps { get; }

    /// <summary>
    /// Готовый файл задания или пусто, если работы нет
    /// </summary>
    public string WorkPath { get; }

    /// <summary>
    /// Работу уже открывали в этом сеансе
    /// </summary>
    public bool Viewed { get; }

    /// <summary>
    /// Подпись главной кнопки по шагу: «Посмотреть работу», «Отметить сданным», «Вернуть в работу» или пусто, если кнопки нет
    /// </summary>
    public string MainLabel { get; }

    /// <summary>
    /// У задания есть главная кнопка
    /// </summary>
    public bool HasMain => MainLabel.Length > 0;

    /// <summary>
    /// Главная кнопка: открывает работу, отмечает сданным или снимает отметку
    /// </summary>
    public RelayCommand MainCommand { get; }

    /// <summary>
    /// Одна строка о том, что ждёт задание или что пошло не так
    /// </summary>
    public string Hint { get; }

    /// <summary>
    /// Есть подсказка
    /// </summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>
    /// Подсказка говорит об ошибке
    /// </summary>
    public bool HintIsProblem { get; }

    /// <summary>
    /// Задание ждёт подтверждения владельца и само не готовится
    /// </summary>
    public bool NeedsConfirmation { get; }

    /// <summary>
    /// Вопрос владельцу, например «Не уверен, что это задание именно по Гейм-дизайну»
    /// </summary>
    public string Question { get; }

    /// <summary>
    /// Пометка средней уверенности: «Проверь формулировку: …»
    /// </summary>
    public string Note { get; }

    /// <summary>
    /// Есть пометка «проверь формулировку»
    /// </summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// Цитата из расшифровки в кавычках-ёлочках или пусто
    /// </summary>
    public string Quote { get; }

    /// <summary>
    /// У задания есть цитата
    /// </summary>
    public bool HasEvidence => Quote.Length > 0;

    /// <summary>
    /// Значок надёжности: «подтверждено цитатой и LMS», «только расшифровка» (цитата найдена, работы LMS нет), «только LMS» (работа LMS, цитаты нет), «цитата не найдена» или «расшифровка без цитаты» (старые данные)
    /// </summary>
    public string Reliability { get; }

    /// <summary>
    /// Значок предупреждает: цитата названа, но в расшифровке её нет
    /// </summary>
    public bool ReliabilityWarning { get; }

    /// <summary>
    /// Значок надёжности известен: шаги и проверка цитаты уже прочитаны
    /// </summary>
    public bool HasReliability => Reliability.Length > 0;

    /// <summary>
    /// Шаг конвейера, проверка цитаты и связь с LMS прочитаны. Пока нет, карточка нейтральная: без выделенного шага и значка
    /// </summary>
    public bool PipelineKnown { get; }

    /// <summary>
    /// Что делает главная кнопка
    /// </summary>
    public ItemMainActionEnum MainAction { get; }

    /// <summary>
    /// Главная кнопка спокойная, без акцента: «Отметить сданным» у задания без работы и «Вернуть в работу»
    /// </summary>
    public bool MainIsQuiet { get; }

    /// <summary>
    /// Открывает готовую работу задания в любом шаге, где она есть: пункт меню «Открыть работу»
    /// </summary>
    public RelayCommand OpenWorkCommand { get; }

    /// <summary>
    /// У задания есть страница в LMS: номер работы или ссылка
    /// </summary>
    public bool HasLmsPage { get; }

    /// <summary>
    /// Источник подтверждён и цитатой, и LMS: значок зелёный
    /// </summary>
    public bool IsConfirmedSource { get; }

    /// <summary>
    /// Задание связано с работой LMS по её номеру
    /// </summary>
    public bool LinkedToLms { get; }

    /// <summary>
    /// Подтверждает задание
    /// </summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>
    /// Открывает окно правки условия
    /// </summary>
    public RelayCommand FixConditionCommand { get; }

    /// <summary>
    /// Проверяет готовую работу заново
    /// </summary>
    public RelayCommand RecheckCommand { get; }

    /// <summary>
    /// Открывает отчёт о проверке
    /// </summary>
    public RelayCommand OpenCheckCommand { get; }

    /// <summary>
    /// Открывает окно «Переделать»
    /// </summary>
    public RelayCommand RedoCommand { get; }

    /// <summary>
    /// Прикрепляет свою готовую работу
    /// </summary>
    public RelayCommand AttachWorkCommand { get; }

    /// <summary>
    /// Указывает файл сданной работы: он становится образцом
    /// </summary>
    public RelayCommand SubmittedFileCommand { get; }

    /// <summary>
    /// Удаляет готовые файлы занятия
    /// </summary>
    public RelayCommand DeleteDraftsCommand { get; }

    /// <summary>
    /// Открывает читалку на расшифровке занятия
    /// </summary>
    public RelayCommand OpenTranscriptCommand { get; }

    /// <summary>
    /// Всё, что видно на карточке, одной строкой: по ней список понимает, что строку нужно пересоздать
    /// </summary>
    public string StateKey => string.Join("|", Number, Title, Meta, Requirements, IsDone, Submitted, InSingularity, Step, WorkPath, Viewed, MainLabel, Hint, Question, Note, Quote, Reliability, PipelineKnown);
}
