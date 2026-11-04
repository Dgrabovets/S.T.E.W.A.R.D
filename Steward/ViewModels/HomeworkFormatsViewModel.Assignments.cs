using System.IO;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Models;
using Steward.Mvvm;
using Steward.Preview;

namespace Steward.ViewModels;

/// <summary>
/// Задания из разбора занятия на вкладке «Задания»: добавление в список, Singularity, «Убрать» с откатом и запись при выходе
/// </summary>
public sealed partial class HomeworkFormatsViewModel
{
    /// <summary>
    /// Задания занятия с отрывками расшифровки для вкладки «Задания»
    /// </summary>
    private IReadOnlyList<AssignmentExcerptViewModel> _assignments = [];

    /// <summary>
    /// У занятия есть задания с отрывками: вкладка «Задания» показывается только тогда
    /// </summary>
    public bool HasAssignments => _assignments.Count > 0;

    /// <summary>
    /// Показана вкладка «Задания»
    /// </summary>
    public bool IsAssignmentsTab => ActiveReaderTab == ReaderTabEnum.Assignments && HasReaderTabs;

    /// <summary>
    /// Задания занятия: слова преподавателя, реплики до и после, кнопка «Добавить в задание»
    /// </summary>
    public IReadOnlyList<AssignmentExcerptViewModel> Assignments => _assignments;

    /// <summary>
    /// Собирает задания открытого занятия из разбора. Какие из них уже лежат в списке заданий, определяется по названию и цитате
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Задания по порядку; пусто, если на занятии заданий не было</returns>
    private IReadOnlyList<AssignmentExcerptViewModel> BuildAssignments(LessonFormatViewModel lesson)
    {
        var tasks = AnalysisReader?.Invoke(lesson.Id)?.Tasks ?? [];
        var listed = ItemsReader?.Invoke(lesson.Id) ?? [];
        var states = AssignmentStateReader?.Invoke(lesson.Id) ?? [];

        // Номер задания остаётся номером в разборе, даже если соседнее убрано: по нему заводится задача в Singularity
        return [.. tasks
            .Select((task, index) => (Task: task, Index: index, State: index < states.Count ? states[index] : default))
            .Where(row => !row.State.Dismissed && !IsPendingDismiss(lesson.Id, row.Task.Title))
            .Select(row => new AssignmentExcerptViewModel(
                row.Index + 1,
                row.Task,
                row.State.Listed || listed.Any(item => TaskMatch.Same(item.Item.Title, item.Item.Evidence, row.Task.Title, row.Task.Quote)),
                AssignmentAdder is null ? null : item => Forget(AddAssignmentAsync(lesson, item), lesson),
                AssignmentToSingularity is null ? null : item => Forget(AssignmentToSingularityAsync(lesson, item), lesson),
                row.State.InSingularity,
                AssignmentDismisser is null ? null : item => Forget(DismissAssignmentAsync(lesson, item), lesson)))];
    }

    /// <summary>
    /// Задания, которые убраны с вкладки, но ещё ждут конца окошка «Задание убрано»: ключ «занятие + название». Пока окошко не закончилось, задание только спрятано и его можно откатить; файл меняется в конце
    /// </summary>
    private readonly Dictionary<string, (string LessonId, string Title, string Quote)> _pendingDismiss = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ключ задания в наборе ожидающих
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="title">Название задания в разборе</param>
    /// <returns>Ключ</returns>
    private static string PendingKey(string lessonId, string title) => lessonId + "|" + title.Trim();

    /// <summary>
    /// Задание убрано с вкладки и ждёт конца окошка
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="title">Название задания в разборе</param>
    /// <returns><see langword="true"/>, если задание спрятано, но ещё не записано в файл</returns>
    private bool IsPendingDismiss(string lessonId, string title)
    {
        lock (_pendingDismiss)
            return _pendingDismiss.ContainsKey(PendingKey(lessonId, title));
    }

    /// <summary>
    /// Прячет задание сразу и показывает окошко «Задание убрано» с откатом. Окончательно задание убирается, когда окошко закончилось: время вышло, его закрыли, показали другое или закрылось приложение
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="row">Задание на вкладке</param>
    /// <returns>Задача, которая завершается, когда задание спрятано и окошко показано</returns>
    private Task DismissAssignmentAsync(LessonFormatViewModel lesson, AssignmentExcerptViewModel row)
    {
        lock (_pendingDismiss)
            _pendingDismiss[PendingKey(lesson.Id, row.Title)] = (lesson.Id, row.Title, row.Task.Quote);

        _assignments = [.. _assignments.Where(item => item != row)];
        RaiseReaderTabs();
        Toast.Show("Задание убрано", () => Forget(UndoDismissAsync(lesson, row.Title), lesson), () => Forget(CommitDismissAsync(lesson, row.Title, row.Task.Quote), lesson));

        return RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Записывает в файл все убранные задания, которые ещё ждут конца окошка, и ждёт записи. Вызывается при выходе из приложения: фоновая запись не успела бы до закрытия. Ошибка записи пишется в журнал и не мешает выходу
    /// </summary>
    public void CommitPendingDismissals()
    {
        (string LessonId, string Title, string Quote)[] pending;
        lock (_pendingDismiss)
        {
            pending = [.. _pendingDismiss.Values];
            _pendingDismiss.Clear();
        }

        foreach (var (lessonId, title, quote) in pending)
        {
            try
            {
                AssignmentDismisser?.Invoke(lessonId, title, quote, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceError($"Убранное задание {lessonId} не записалось при выходе: {exception}");
            }
        }
    }

    /// <summary>
    /// Откатывает «Убрать»: задание снова на вкладке, файл не менялся
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="title">Название задания в разборе</param>
    /// <returns>Задача, которая завершается, когда задание возвращено</returns>
    private Task UndoDismissAsync(LessonFormatViewModel lesson, string title)
    {
        lock (_pendingDismiss)
            _pendingDismiss.Remove(PendingKey(lesson.Id, title));

        if (OpenLesson == lesson)
        {
            _assignments = BuildAssignments(lesson);
            RaiseReaderTabs();
        }

        return RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Записывает уход задания в файл, когда окошко закончилось. Если записать не вышло, задание возвращается на вкладку, а причина видна в карточке занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="title">Название задания в разборе</param>
    /// <param name="quote">Слова преподавателя из разбора</param>
    /// <returns>Задача, которая завершается, когда файл записан</returns>
    private async Task CommitDismissAsync(LessonFormatViewModel lesson, string title, string quote)
    {
        try
        {
            await Task.Run(() => AssignmentDismisser!(lesson.Id, title, quote, true));
        }
        catch (Exception exception)
        {
            Fail(lesson, "Не удалось убрать задание: ", exception);
        }
        finally
        {
            lock (_pendingDismiss)
                _pendingDismiss.Remove(PendingKey(lesson.Id, title));
        }

        if (OpenLesson == lesson)
        {
            _assignments = BuildAssignments(lesson);
            RaiseReaderTabs();
        }

        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Заводит задачу в Singularity по заданию из отрывка в фоновой задаче, не добавляя задание в список заданий занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="row">Задание на вкладке</param>
    /// <returns>Задача, которая завершается, когда Singularity ответил и итог показан</returns>
    private async Task AssignmentToSingularityAsync(LessonFormatViewModel lesson, AssignmentExcerptViewModel row)
    {
        row.ReportSingularity("Завожу задачу в Singularity…", created: false);
        var (message, failed) = await AssignmentToSingularity!(lesson.Id, row.Number);
        row.ReportSingularity(message, created: !failed);
        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Добавляет задание из отрывка в список заданий занятия в фоновой задаче и сообщает итог на кнопке
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="row">Задание на вкладке</param>
    /// <returns>Задача, которая завершается, когда задание записано и итог показан</returns>
    private async Task AddAssignmentAsync(LessonFormatViewModel lesson, AssignmentExcerptViewModel row)
    {
        try
        {
            var added = await Task.Run(() => AssignmentAdder!(lesson.Id, row.Title, row.Task.Quote, row.Due));
            var hidden = _stateOf?.Invoke(lesson.Title) is { } state && state is not (LessonStateEnum.Fetch or LessonStateEnum.LmsOnly);
            row.Report(!added ? "Такое задание уже есть в списке." : hidden ? "Добавлено. Оно появится на вкладке «Домашние задания», когда у предмета выбрано «Расшифровывать»." : "Добавлено: задание на вкладке «Домашние задания».", added: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            row.Report("Не удалось добавить: " + exception.Message, added: false);

            return;
        }

        await RefreshItemsAsync(lesson);
    }
}
