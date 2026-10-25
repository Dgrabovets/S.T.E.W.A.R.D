using Steward.Controls;

namespace Steward.Models;

/// <summary>
/// Следит, какие задачи выполняются и какие только что закончились, чтобы показать вращение или галочку
/// </summary>
public sealed class ActivityTracker
{
    /// <summary>
    /// Задачи, которые выполнялись при прошлом обновлении
    /// </summary>
    private HashSet<string> _working = [];

    /// <summary>
    /// Когда закончились задачи, которым ещё показывается галочка
    /// </summary>
    private readonly Dictionary<string, DateTimeOffset> _finished = [];

    /// <summary>
    /// Замечает задачи, которые перестали выполняться с прошлого обновления, и запоминает, когда это случилось. Сбойные не отмечаются: им галочка не положена
    /// </summary>
    /// <param name="rows">Строки задач</param>
    /// <param name="now">Текущий момент</param>
    public void Track(IReadOnlyList<TaskStatusRow> rows, DateTimeOffset now)
    {
        foreach (var row in rows.Where(row => !row.Working && row.Tone != PanelToneEnum.Problem && _working.Contains(row.Id)))
            _finished[row.Id] = now;

        foreach (var id in _finished.Where(pair => (now - pair.Value).TotalSeconds >= ActivityOrb.DONE_SHOW_SECONDS).Select(pair => pair.Key).ToList())
            _finished.Remove(id);

        _working = [.. rows.Where(row => row.Working).Select(row => row.Id)];
    }

    /// <summary>
    /// Что показывать у задачи: вращение, галочку или обычную точку
    /// </summary>
    /// <param name="row">Строка задачи</param>
    /// <returns>Состояние индикатора</returns>
    public ActivityStateEnum StateOf(TaskStatusRow row) =>
        row.Working ? ActivityStateEnum.Working : _finished.ContainsKey(row.Id) ? ActivityStateEnum.Done : ActivityStateEnum.None;

    /// <summary>
    /// Когда закончилась задача, если ей показывается галочка
    /// </summary>
    /// <param name="id">Идентификатор задачи</param>
    /// <returns>Момент окончания или значение по умолчанию, если галочки нет</returns>
    public DateTimeOffset FinishedAt(string id) => _finished.GetValueOrDefault(id);
}