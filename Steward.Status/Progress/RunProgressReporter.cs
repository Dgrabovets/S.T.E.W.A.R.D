namespace Steward.Status.Progress;

/// <summary>
/// Ведёт прогресс одного запуска процесса: отмечает, на каком шаге он и сколько сделано внутри шага. Прогресс вспомогательный, поэтому сбой записи никогда не ломает сам процесс
/// </summary>
public sealed class RunProgressReporter
{
    /// <summary>
    /// Куда пишется прогресс
    /// </summary>
    private readonly RunProgressStore _store;

    /// <summary>
    /// Идентификатор процесса
    /// </summary>
    private readonly string _id;

    /// <summary>
    /// Названия шагов запуска по порядку
    /// </summary>
    private readonly IReadOnlyList<string> _stages;

    /// <summary>
    /// Источник текущего времени
    /// </summary>
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Когда начался запуск
    /// </summary>
    private readonly DateTimeOffset _started;

    /// <summary>
    /// Номер текущего шага
    /// </summary>
    private int _current;

    /// <summary>
    /// Сколько всего единиц работы в текущем шаге
    /// </summary>
    private int _total;

    /// <summary>
    /// Создаёт отчёт о запуске и сразу записывает первый шаг
    /// </summary>
    /// <param name="store">Куда писать прогресс</param>
    /// <param name="id">Идентификатор процесса</param>
    /// <param name="stages">Названия шагов запуска по порядку</param>
    /// <param name="clock">Источник времени: в тестах подменяется. Пусто: системные часы</param>
    public RunProgressReporter(RunProgressStore store, string id, IReadOnlyList<string> stages, Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _id = id;
        _stages = stages;
        _clock = clock ?? (() => DateTimeOffset.Now);
        _started = _clock();
        Write(0, 0, 0, string.Empty);
    }

    /// <summary>
    /// Переходит к шагу
    /// </summary>
    /// <param name="stage">Номер шага, с нуля</param>
    /// <param name="total">Сколько единиц работы будет в шаге. Ноль: счётчика нет</param>
    /// <param name="detail">Что обрабатывается сейчас</param>
    public void Enter(int stage, int total = 0, string detail = "") => Write(stage, 0, total, detail);

    /// <summary>
    /// Отмечает, сколько сделано внутри текущего шага
    /// </summary>
    /// <param name="done">Сколько единиц уже сделано</param>
    /// <param name="detail">Что обрабатывается сейчас</param>
    public void Step(int done, string detail = "") => Write(_current, done, _total, detail);

    /// <summary>
    /// Запуск закончился: файл прогресса убирается
    /// </summary>
    public void Finish() => _store.Clear(_id);

    /// <summary>
    /// Записывает текущее состояние. Ошибка записи подавляется: сам процесс из-за неё не должен падать
    /// </summary>
    /// <param name="stage">Номер шага</param>
    /// <param name="done">Сколько сделано</param>
    /// <param name="total">Сколько всего</param>
    /// <param name="detail">Что обрабатывается</param>
    private void Write(int stage, int done, int total, string detail)
    {
        _current = stage;
        _total = total;
        try
        {
            _store.Write(_id, new RunProgress(_stages, stage, done, total, detail, _started, _clock()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Прогресс нужен только для показа: не записался сейчас, запишется на следующем шаге
        }
    }
}
