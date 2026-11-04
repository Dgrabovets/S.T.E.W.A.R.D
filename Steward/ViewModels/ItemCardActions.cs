namespace Steward.ViewModels;

/// <summary>
/// Действия карточки задания, которые выполняет список занятий: главная кнопка, вопрос «Нужно подтвердить», блок источника и пункты меню «…». Пустое действие делает свою кнопку недоступной
/// </summary>
public sealed class ItemCardActions
{
    /// <summary>
    /// Открывает готовую работу задания: «Посмотреть работу»
    /// </summary>
    public Action<LessonItemViewModel>? ViewWork { get; init; }

    /// <summary>
    /// Подтверждает задание: «Подтвердить»
    /// </summary>
    public Action<LessonItemViewModel>? Confirm { get; init; }

    /// <summary>
    /// Открывает окно правки условия: «Поправить условие»
    /// </summary>
    public Action<LessonItemViewModel>? FixCondition { get; init; }

    /// <summary>
    /// Проверяет готовую работу занятия заново
    /// </summary>
    public Action<LessonItemViewModel>? Recheck { get; init; }

    /// <summary>
    /// Открывает отчёт о проверке
    /// </summary>
    public Action<LessonItemViewModel>? OpenCheck { get; init; }

    /// <summary>
    /// Открывает окно «Переделать»
    /// </summary>
    public Action<LessonItemViewModel>? Redo { get; init; }

    /// <summary>
    /// Просит выбрать файл своей готовой работы и прикрепляет его
    /// </summary>
    public Action<LessonItemViewModel>? AttachWork { get; init; }

    /// <summary>
    /// Просит выбрать файл сданной работы и делает его образцом
    /// </summary>
    public Action<LessonItemViewModel>? SubmittedFile { get; init; }

    /// <summary>
    /// Спрашивает и удаляет готовые файлы занятия
    /// </summary>
    public Action<LessonItemViewModel>? DeleteDrafts { get; init; }

    /// <summary>
    /// Открывает читалку на расшифровке занятия
    /// </summary>
    public Action<LessonItemViewModel>? OpenTranscript { get; init; }
}
